using SixToFix.Application.Exceptions;
using SixToFix.Application.Models;
using SixToFix.Application.Services;
using SixToFix.Infrastructure.Data;

namespace SixToFix.Infrastructure.Services;

public sealed class ReviewerWorkflow : IReviewerWorkflow
{
    private const int LockoutThreshold = 3;
    private const int LockoutWindowHours = 24;

    private readonly SixToFixDbContext _db;
    private readonly ICalibrationTracker _calibrationTracker;
    private readonly ICouncilRunner _councilRunner;
    private readonly ISkillRunner _skillRunner;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ReviewerWorkflow> _logger;

    public ReviewerWorkflow(
        SixToFixDbContext db,
        ICalibrationTracker calibrationTracker,
        ICouncilRunner councilRunner,
        ISkillRunner skillRunner,
        ITenantContext tenant,
        ILogger<ReviewerWorkflow> logger)
    {
        _db = db;
        _calibrationTracker = calibrationTracker;
        _councilRunner = councilRunner;
        _skillRunner = skillRunner;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task RejectAsync(Guid auditRunId, Guid categoryId, Guid reviewerId, string? reason, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        var lockKey = (long)(categoryId.GetHashCode() * 31L + reviewerId.GetHashCode());
        await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockKey})", ct);

        var windowStart = DateTimeOffset.UtcNow.AddHours(-LockoutWindowHours);

        var lockout = await _db.ReviewerLockouts
            .Where(rl => rl.AuditRunId == auditRunId
                      && rl.Category == categoryId.ToString()
                      && rl.ReviewerUserId == reviewerId)
            .FirstOrDefaultAsync(ct);

        if (lockout is not null && lockout.WindowStartedAt > windowStart && lockout.RejectionCount >= LockoutThreshold)
        {
            throw new ReviewerLockoutException(new ReviewerLockoutStatus(
                true,
                lockout.RejectionCount,
                lockout.WindowStartedAt.AddHours(LockoutWindowHours)));
        }

        var categoryResult = await GetCategoryResultAsync(auditRunId, categoryId, ct);

        categoryResult.Status = "rejected";
        categoryResult.ReviewedByUserId = reviewerId;
        categoryResult.ReviewedAt = DateTimeOffset.UtcNow;
        categoryResult.ReviewNotes = reason;
        categoryResult.UpdatedAt = DateTimeOffset.UtcNow;

        _db.ReviewerActions.Add(new ReviewerAction
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            AuditRunId = auditRunId,
            CategoryId = categoryId.ToString(),
            ReviewerId = reviewerId,
            ActionType = "reject",
            CreatedAt = DateTimeOffset.UtcNow
        });

        var now = DateTimeOffset.UtcNow;
        if (lockout is null)
        {
            _db.ReviewerLockouts.Add(new ReviewerLockout
            {
                Id = Guid.NewGuid(),
                TenantId = _tenant.TenantId,
                AuditRunId = auditRunId,
                Category = categoryId.ToString(),
                ReviewerUserId = reviewerId,
                RejectionCount = 1,
                WindowStartedAt = now,
                IsLocked = false,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            lockout.RejectionCount++;
            if (lockout.RejectionCount >= LockoutThreshold)
                lockout.IsLocked = true;
            lockout.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _logger.LogInformation(
            "Category {CategoryId} rejected by {ReviewerId} for AuditRun {AuditRunId} (count now {Count})",
            categoryId, reviewerId, auditRunId,
            lockout?.RejectionCount ?? 1);
    }

    public async Task ApproveAsync(Guid auditRunId, Guid categoryId, Guid reviewerId, CancellationToken ct = default)
    {
        var categoryResult = await GetCategoryResultAsync(auditRunId, categoryId, ct);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        categoryResult.Status = "approved";
        categoryResult.ReviewedByUserId = reviewerId;
        categoryResult.ReviewedAt = DateTimeOffset.UtcNow;
        categoryResult.UpdatedAt = DateTimeOffset.UtcNow;

        var version = await BuildNextVersionAsync(categoryResult, reviewerId, "approve", ct);
        _db.CategoryResultVersions.Add(version);

        _db.ReviewerActions.Add(new ReviewerAction
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            AuditRunId = auditRunId,
            CategoryId = categoryId.ToString(),
            ReviewerId = reviewerId,
            ActionType = "approve",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _logger.LogInformation("Category {CategoryId} approved by {ReviewerId} for AuditRun {AuditRunId}", categoryId, reviewerId, auditRunId);
    }

    public async Task<CategoryResult> EditAsync(
        Guid auditRunId,
        Guid categoryId,
        Guid reviewerId,
        decimal newActivityScore,
        string? newDocumentedStrategy,
        string overrideReasonCode,
        string notes,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(overrideReasonCode))
            throw new MissingOverrideReasonException();

        if (string.IsNullOrWhiteSpace(notes))
            throw new MissingCalibrationNotesException();

        if (newActivityScore < 0 || newActivityScore > 10)
            throw new InvalidScoreRangeException(newActivityScore, 0, 10);

        await CheckLockoutAsync(auditRunId, categoryId, reviewerId, ct);

        var categoryResult = await GetCategoryResultAsync(auditRunId, categoryId, ct);
        var originalScore = (decimal)categoryResult.ActivityScore;

        await _calibrationTracker.RecordDeltaAsync(
            auditRunId,
            categoryId,
            reviewerId,
            originalScore,
            newActivityScore,
            null,
            newDocumentedStrategy,
            overrideReasonCode,
            notes,
            ct);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        categoryResult.ActivityScore = (int)Math.Round(newActivityScore);
        categoryResult.ReviewedByUserId = reviewerId;
        categoryResult.ReviewedAt = DateTimeOffset.UtcNow;
        categoryResult.ReviewNotes = notes;
        categoryResult.UpdatedAt = DateTimeOffset.UtcNow;

        var version = await BuildNextVersionAsync(categoryResult, reviewerId, "reviewer", ct);
        version.ReviewNotes = notes;
        _db.CategoryResultVersions.Add(version);

        _db.ReviewerActions.Add(new ReviewerAction
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            AuditRunId = auditRunId,
            CategoryId = categoryId.ToString(),
            ReviewerId = reviewerId,
            ActionType = "edit",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _logger.LogInformation(
            "Category {CategoryId} edited by {ReviewerId}: score {Old} -> {New}",
            categoryId,
            reviewerId,
            originalScore,
            newActivityScore);

        return categoryResult;
    }

    public async Task RerunAsync(Guid auditRunId, Guid categoryId, Guid reviewerId, CancellationToken ct = default)
    {
        await CheckLockoutAsync(auditRunId, categoryId, reviewerId, ct);

        var categoryResult = await GetCategoryResultAsync(auditRunId, categoryId, ct);
        if (categoryResult.Status == "approved")
            throw new InvalidCategoryStateException(categoryResult.Status);

        var fromSkillIndex = await _db.SkillRuns
            .Where(run => run.AuditRunId == auditRunId && run.Category == categoryResult.Category)
            .OrderByDescending(run => run.SequenceIndex)
            .Select(run => (int?)run.SequenceIndex)
            .FirstOrDefaultAsync(ct)
            ?? 0;

        await _skillRunner.MarkDownstreamSkillsStaleAsync(auditRunId, fromSkillIndex, ct);

        _db.ReviewerActions.Add(new ReviewerAction
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            AuditRunId = auditRunId,
            CategoryId = categoryId.ToString(),
            ReviewerId = reviewerId,
            ActionType = "rerun",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Rerun requested for Category {CategoryId} by {ReviewerId} in AuditRun {AuditRunId}", categoryId, reviewerId, auditRunId);
    }

    public async Task<CouncilDecisionModel> EscalateAsync(Guid auditRunId, Guid categoryId, Guid reviewerId, CancellationToken ct = default)
    {
        _ = await GetCategoryResultAsync(auditRunId, categoryId, ct);
        var councilResult = await _councilRunner.RunCouncilAsync(auditRunId, categoryId, Array.Empty<PolicyFlagModel>(), ct);

        _db.ReviewerActions.Add(new ReviewerAction
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            AuditRunId = auditRunId,
            CategoryId = categoryId.ToString(),
            ReviewerId = reviewerId,
            ActionType = "escalate",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Escalation for Category {CategoryId} completed with decision {Decision}", categoryId, councilResult.DecisionType);

        return councilResult;
    }

    public async Task<ReviewerLockoutStatus> GetLockoutStatusAsync(Guid auditRunId, Guid categoryId, Guid reviewerId, CancellationToken ct = default)
    {
        var windowStart = DateTimeOffset.UtcNow.AddHours(-LockoutWindowHours);

        var lockout = await _db.ReviewerLockouts
            .Where(rl => rl.AuditRunId == auditRunId
                      && rl.Category == categoryId.ToString()
                      && rl.ReviewerUserId == reviewerId)
            .FirstOrDefaultAsync(ct);

        if (lockout is null)
            return new ReviewerLockoutStatus(false, 0, null);

        var isLocked = lockout.IsLocked && lockout.WindowStartedAt > windowStart;
        var expiresAt = isLocked ? lockout.WindowStartedAt.AddHours(LockoutWindowHours) : (DateTimeOffset?)null;

        return new ReviewerLockoutStatus(isLocked, lockout.RejectionCount, expiresAt);
    }

    private async Task CheckLockoutAsync(Guid auditRunId, Guid categoryId, Guid reviewerId, CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        var lockKey = (long)(categoryId.GetHashCode() * 31L + reviewerId.GetHashCode());
        await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockKey})", ct);

        var windowStart = DateTimeOffset.UtcNow.AddHours(-LockoutWindowHours);

        var lockout = await _db.ReviewerLockouts
            .Where(rl => rl.AuditRunId == auditRunId
                      && rl.Category == categoryId.ToString()
                      && rl.ReviewerUserId == reviewerId)
            .FirstOrDefaultAsync(ct);

        if (lockout is not null && lockout.WindowStartedAt > windowStart && lockout.RejectionCount >= LockoutThreshold)
        {
            throw new ReviewerLockoutException(new ReviewerLockoutStatus(
                true,
                lockout.RejectionCount,
                lockout.WindowStartedAt.AddHours(LockoutWindowHours)));
        }

        await transaction.CommitAsync(ct);
    }

    private async Task<CategoryResult> GetCategoryResultAsync(Guid auditRunId, Guid categoryId, CancellationToken ct)
    {
        return await _db.CategoryResults
            .FirstOrDefaultAsync(r => r.AuditRunId == auditRunId && r.Id == categoryId, ct)
            ?? throw new CategoryResultNotFoundException(categoryId);
    }

    private async Task<CategoryResultVersion> BuildNextVersionAsync(
        CategoryResult categoryResult,
        Guid actorUserId,
        string action,
        CancellationToken ct)
    {
        var maxVersion = await _db.CategoryResultVersions
            .Where(v => v.CategoryResultId == categoryResult.Id)
            .MaxAsync(v => (int?)v.Version, ct) ?? 0;

        return new CategoryResultVersion
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            CategoryResultId = categoryResult.Id,
            Version = maxVersion + 1,
            ActivityScore = categoryResult.ActivityScore,
            Action = action,
            ActorUserId = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
