using System.Threading.Channels;
using SixToFix.Application.Exceptions;
using SixToFix.Application.Models;
using SixToFix.Application.Services;
using SixToFix.Infrastructure.Data;

namespace SixToFix.Infrastructure.Services;

public sealed class Publisher : IPublisher
{
    private static readonly string[] CategoryIds = ["brand", "customer", "offering", "communications", "sales", "management"];

    private readonly SixToFixDbContext _db;
    private readonly Channel<HubSpotEvent> _hubSpotChannel;
    private readonly ITenantContext _tenant;
    private readonly ILogger<Publisher> _logger;

    public Publisher(
        SixToFixDbContext db,
        Channel<HubSpotEvent> hubSpotChannel,
        ITenantContext tenant,
        ILogger<Publisher> logger)
    {
        _db = db;
        _hubSpotChannel = hubSpotChannel;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task<PublishResult> PublishAuditAsync(Guid auditRunId, Guid publishedByUserId, CancellationToken ct = default)
    {
        var auditRun = await _db.AuditRuns
            .FirstOrDefaultAsync(r => r.Id == auditRunId, ct)
            ?? throw new AuditRunNotFoundException(auditRunId);

        if (auditRun.Status == "published")
            throw new AuditAlreadyPublishedException(auditRunId);

        if (auditRun.Status != "awaiting_review" && auditRun.Status != "completed")
            throw new InvalidAuditRunStateException(auditRun.Status, "awaiting_review");

        var categoryResults = await _db.CategoryResults
            .Where(r => r.AuditRunId == auditRunId)
            .ToListAsync(ct);

        var approvedCount = categoryResults.Count(r => r.Status == "approved" && CategoryIds.Contains(r.Category));
        if (approvedCount < CategoryIds.Length)
            throw new NotAllCategoriesApprovedException(approvedCount, CategoryIds.Length);

        var composite = ComputeComposite(categoryResults);
        var tier = DeriveTier(composite);
        var systemsMaturityScore = await GetSkillScoreAsync(auditRunId, "systems-maturity-scoring", ct);
        var aiReadinessPct = await GetSkillScoreAsync(auditRunId, "derive-tier", ct);
        var publishedAt = DateTimeOffset.UtcNow;

        auditRun.Status = "published";
        auditRun.CompositeScore = (int)composite;
        auditRun.SystemsMaturityScore = systemsMaturityScore;
        auditRun.AiReadinessScore = aiReadinessPct;
        auditRun.Tier = tier;
        auditRun.CompletedAt = publishedAt;

        await _db.SaveChangesAsync(ct);

        var audit = await _db.Audits.FirstOrDefaultAsync(a => a.Id == auditRun.AuditId, ct);
        var client = audit is not null
            ? await _db.Clients.FirstOrDefaultAsync(c => c.Id == audit.ClientId, ct)
            : null;

        if (audit is not null)
        {
            audit.Status = "published";
            audit.PublishedAt = publishedAt;
            await _db.SaveChangesAsync(ct);
        }

        var clientSlug = client?.Slug ?? auditRunId.ToString("N");

        var auditPublishScores = BuildAuditPublishScores(categoryResults, systemsMaturityScore, (int)aiReadinessPct, publishedAt);
        await _hubSpotChannel.Writer.WriteAsync(
            new HubSpotEvent(auditRunId, clientSlug, tier, composite, publishedAt)
            {
                HubSpotCompanyId = client?.HubSpotCompanyId,
                Scores = auditPublishScores
            },
            ct);

        _logger.LogInformation(
            "AuditRun {AuditRunId} published by {PublishedByUserId} for Tenant {TenantId}: Tier={Tier}, Composite={Composite}",
            auditRunId,
            publishedByUserId,
            _tenant.TenantId,
            tier,
            composite);

        return new PublishResult(composite, systemsMaturityScore, aiReadinessPct, tier, publishedAt);
    }

    public async Task<PublishedAuditSummary> GetPublishedAuditByRunIdAsync(Guid auditRunId, CancellationToken ct = default)
    {
        var auditRun = await _db.AuditRuns
            .FirstOrDefaultAsync(r => r.Id == auditRunId && r.Status == "published", ct)
            ?? throw new AuditRunNotFoundException(auditRunId);

        var audit = await _db.Audits.FirstOrDefaultAsync(a => a.Id == auditRun.AuditId, ct);
        var client = audit is not null
            ? await _db.Clients.FirstOrDefaultAsync(c => c.Id == audit.ClientId, ct)
            : null;

        var clientSlug = client?.Slug ?? auditRunId.ToString("N");

        var categoryResults = await _db.CategoryResults
            .Where(r => r.AuditRunId == auditRunId)
            .ToListAsync(ct);

        var categoryScores = categoryResults.ToDictionary(r => r.Category, r => (decimal)r.ActivityScore);

        return new PublishedAuditSummary(
            clientSlug,
            auditRun.Tier ?? "tier_3",
            (decimal)(auditRun.CompositeScore ?? 0),
            auditRun.SystemsMaturityScore ?? 0m,
            auditRun.AiReadinessScore ?? 0m,
            auditRun.CompletedAt ?? auditRun.CreatedAt,
            categoryScores);
    }

    public async Task<PublishedAuditSummary> GetPublishedAuditAsync(string clientSlug, CancellationToken ct = default)
    {
        var auditRun = await GetLatestPublishedRunAsync(clientSlug, ct)
            ?? throw new NoPublishedAuditException(clientSlug);

        var categoryResults = await _db.CategoryResults
            .Where(r => r.AuditRunId == auditRun.Id)
            .ToListAsync(ct);

        var categoryScores = categoryResults.ToDictionary(r => r.Category, r => (decimal)r.ActivityScore);

        return new PublishedAuditSummary(
            clientSlug,
            auditRun.Tier ?? "tier_3",
            (decimal)(auditRun.CompositeScore ?? 0),
            auditRun.SystemsMaturityScore ?? 0m,
            auditRun.AiReadinessScore ?? 0m,
            auditRun.CompletedAt ?? auditRun.CreatedAt,
            categoryScores);
    }

    public async Task<IReadOnlyList<PublishedAuditVersion>> GetPublishedVersionsAsync(string clientSlug, CancellationToken ct = default)
    {
        var client = await _db.Clients
            .FirstOrDefaultAsync(c => c.Slug == clientSlug, ct)
            ?? throw new ClientNotFoundException(Guid.Empty);

        var audits = await _db.Audits
            .Where(a => a.ClientId == client.Id)
            .Select(a => a.Id)
            .ToListAsync(ct);

        var publishedRuns = await _db.AuditRuns
            .Where(r => audits.Contains(r.AuditId) && r.Status == "published")
            .OrderByDescending(r => r.CompletedAt)
            .ToListAsync(ct);

        return publishedRuns.Select(r => new PublishedAuditVersion(
            r.CompletedAt ?? r.CreatedAt,
            r.Tier ?? "tier_3",
            (decimal)(r.CompositeScore ?? 0))).ToList();
    }

    private async Task<AuditRun?> GetLatestPublishedRunAsync(string clientSlug, CancellationToken ct)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Slug == clientSlug, ct);
        if (client is null)
        {
            return null;
        }

        var audits = await _db.Audits
            .Where(a => a.ClientId == client.Id)
            .Select(a => a.Id)
            .ToListAsync(ct);

        return await _db.AuditRuns
            .Where(r => audits.Contains(r.AuditId) && r.Status == "published")
            .OrderByDescending(r => r.CompletedAt)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<decimal> GetSkillScoreAsync(Guid auditRunId, string skillName, CancellationToken ct)
    {
        var skillRun = await _db.SkillRuns
            .Where(s => s.AuditRunId == auditRunId && s.SkillName == skillName && s.Status == "completed")
            .OrderByDescending(s => s.CompletedAt)
            .FirstOrDefaultAsync(ct);

        // ActivityScore stores the canonical score for each skill:
        //   systems-maturity-scoring → systems_maturity_score (0–20)
        //   derive-tier              → ai_readiness (0–100)
        return (decimal)(skillRun?.ActivityScore ?? 0);
    }

    private static AuditPublishScores BuildAuditPublishScores(
        IReadOnlyList<CategoryResult> categoryResults,
        decimal systemsMaturityScore,
        int aiReadiness,
        DateTimeOffset publishedAt)
    {
        static int Score(IReadOnlyList<CategoryResult> results, string category)
            => results.FirstOrDefault(r => r.Category == category)?.ActivityScore ?? 0;

        return new AuditPublishScores(
            BrandScore: Score(categoryResults, "brand"),
            CustomerScore: Score(categoryResults, "customer"),
            OfferingScore: Score(categoryResults, "offering"),
            CommunicationsScore: Score(categoryResults, "communications"),
            SalesScore: Score(categoryResults, "sales"),
            ManagementScore: Score(categoryResults, "management"),
            SystemsMaturityScore: systemsMaturityScore,
            AiReadiness: aiReadiness,
            PublishedAt: publishedAt);
    }

    private static decimal ComputeComposite(IReadOnlyList<CategoryResult> results)
        => results.Sum(r => r.ActivityScore);

    private static string DeriveTier(decimal composite) => composite switch
    {
        >= 45 => "tier_1",
        >= 25 => "tier_2",
        _ => "tier_3"
    };
}
