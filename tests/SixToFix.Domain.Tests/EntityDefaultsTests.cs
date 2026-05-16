using SixToFix.Domain.Entities;
using Xunit;

namespace SixToFix.Domain.Tests;

/// <summary>
/// Verifies default values and basic property assignment for domain entities.
/// These tests drive code coverage of the POCO property accessors.
/// </summary>
public sealed class EntityDefaultsTests
{
    [Fact]
    public void Tenant_DefaultValues_AreCorrect()
    {
        var tenant = new Tenant();

        Assert.Equal(string.Empty, tenant.Name);
        Assert.Equal(string.Empty, tenant.Slug);
        Assert.Null(tenant.LogoUrl);
        Assert.True(tenant.IsActive);
        Assert.Empty(tenant.Clients);
        Assert.Empty(tenant.Audits);
    }

    [Fact]
    public void Tenant_PropertyAssignment_RoundTrips()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var tenant = new Tenant
        {
            Id = id,
            Name = "Acme Corp",
            Slug = "acme-corp",
            LogoUrl = "https://example.com/logo.png",
            IsActive = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        Assert.Equal(id, tenant.Id);
        Assert.Equal("Acme Corp", tenant.Name);
        Assert.Equal("acme-corp", tenant.Slug);
        Assert.Equal("https://example.com/logo.png", tenant.LogoUrl);
        Assert.False(tenant.IsActive);
        Assert.Equal(now, tenant.CreatedAt);
        Assert.Equal(now, tenant.UpdatedAt);
    }

    [Fact]
    public void Client_DefaultValues_AreCorrect()
    {
        var client = new Client();

        Assert.Equal(string.Empty, client.Name);
        Assert.Equal(string.Empty, client.Slug);
        Assert.Null(client.Industry);
        Assert.Null(client.HubSpotCompanyId);
        Assert.Null(client.Website);
        Assert.True(client.IsActive);
        Assert.Empty(client.Audits);
    }

    [Fact]
    public void Client_PropertyAssignment_RoundTrips()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var client = new Client
        {
            Id = id,
            TenantId = tenantId,
            Name = "Acme Client",
            Slug = "acme-client",
            Industry = "Technology",
            HubSpotCompanyId = "hs-123",
            Website = "https://acme.com",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        Assert.Equal(id, client.Id);
        Assert.Equal(tenantId, client.TenantId);
        Assert.Equal("Acme Client", client.Name);
        Assert.Equal("Technology", client.Industry);
        Assert.Equal("hs-123", client.HubSpotCompanyId);
        Assert.Equal("https://acme.com", client.Website);
        Assert.True(client.IsActive);
    }

    [Fact]
    public void Audit_DefaultValues_AreCorrect()
    {
        var audit = new Audit();

        Assert.Equal("draft", audit.Status);
        Assert.Null(audit.Title);
        Assert.Null(audit.PublishedAt);
        Assert.Empty(audit.AuditRuns);
        Assert.Empty(audit.CategoryConfigs);
    }

    [Fact]
    public void Audit_PropertyAssignment_RoundTrips()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var audit = new Audit
        {
            Id = id,
            TenantId = tenantId,
            ClientId = clientId,
            Status = "active",
            Title = "Q1 Audit",
            CreatedAt = now,
            UpdatedAt = now,
            PublishedAt = now
        };

        Assert.Equal(id, audit.Id);
        Assert.Equal(tenantId, audit.TenantId);
        Assert.Equal(clientId, audit.ClientId);
        Assert.Equal("active", audit.Status);
        Assert.Equal("Q1 Audit", audit.Title);
        Assert.Equal(now, audit.PublishedAt);
    }

    [Fact]
    public void AuditRun_DefaultValues_AreCorrect()
    {
        var run = new AuditRun();

        Assert.Equal("pending", run.Status);
        Assert.Null(run.CompletedAt);
        Assert.Null(run.ErrorMessage);
        Assert.Null(run.CompositeScore);
        Assert.Null(run.SystemsMaturityScore);
        Assert.Null(run.AiReadinessScore);
        Assert.Null(run.Tier);
        Assert.Empty(run.SkillRuns);
        Assert.Empty(run.CategoryResults);
    }

    [Fact]
    public void AuditRun_PropertyAssignment_RoundTrips()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var run = new AuditRun
        {
            Id = id,
            TenantId = Guid.NewGuid(),
            AuditId = Guid.NewGuid(),
            Status = "completed",
            StartedAt = now,
            CompletedAt = now,
            CompositeScore = 72,
            SystemsMaturityScore = 0.68m,
            AiReadinessScore = 0.75m,
            Tier = "tier_2",
            ErrorMessage = null,
            InitiatedByUserId = Guid.NewGuid(),
            CreatedAt = now
        };

        Assert.Equal(id, run.Id);
        Assert.Equal("completed", run.Status);
        Assert.Equal(72, run.CompositeScore);
        Assert.Equal(0.68m, run.SystemsMaturityScore);
        Assert.Equal(0.75m, run.AiReadinessScore);
        Assert.Equal("tier_2", run.Tier);
    }

    [Fact]
    public void CategoryResult_DefaultValues_AreCorrect()
    {
        var result = new CategoryResult();

        Assert.Equal(string.Empty, result.Category);
        Assert.Equal(0, result.ActivityScore);
        Assert.Equal("pending", result.Status);
        Assert.Null(result.ReviewedByUserId);
        Assert.Null(result.ReviewedAt);
        Assert.Null(result.ReviewNotes);
        Assert.Null(result.SystemsMaturityContribution);
        Assert.Empty(result.Versions);
    }

    [Fact]
    public void CategoryResult_PropertyAssignment_RoundTrips()
    {
        var now = DateTimeOffset.UtcNow;
        var reviewerId = Guid.NewGuid();
        var result = new CategoryResult
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            AuditRunId = Guid.NewGuid(),
            Category = "brand",
            ActivityScore = 85,
            SystemsMaturityContribution = 0.15m,
            Status = "approved",
            ReviewedByUserId = reviewerId,
            ReviewedAt = now,
            ReviewNotes = "LGTM",
            CreatedAt = now,
            UpdatedAt = now
        };

        Assert.Equal("brand", result.Category);
        Assert.Equal(85, result.ActivityScore);
        Assert.Equal(0.15m, result.SystemsMaturityContribution);
        Assert.Equal("approved", result.Status);
        Assert.Equal(reviewerId, result.ReviewedByUserId);
        Assert.Equal("LGTM", result.ReviewNotes);
    }

    [Fact]
    public void Policy_DefaultValues_AreCorrect()
    {
        var policy = new Policy();

        Assert.Equal(string.Empty, policy.RuleCode);
        Assert.Equal("Warning", policy.Severity);
        Assert.True(policy.IsEnabled);
        Assert.Null(policy.ConfigJson);
    }

    [Fact]
    public void Policy_PropertyAssignment_RoundTrips()
    {
        var now = DateTimeOffset.UtcNow;
        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            RuleCode = "BRAND_SCORE_LOW",
            Severity = "Error",
            IsEnabled = false,
            ConfigJson = """{"threshold":50}""",
            CreatedAt = now,
            UpdatedAt = now
        };

        Assert.Equal("BRAND_SCORE_LOW", policy.RuleCode);
        Assert.Equal("Error", policy.Severity);
        Assert.False(policy.IsEnabled);
        Assert.Equal("""{"threshold":50}""", policy.ConfigJson);
    }

    [Fact]
    public void PolicyFlag_PropertyAssignment_RoundTrips()
    {
        var now = DateTimeOffset.UtcNow;
        var flag = new PolicyFlag
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            SkillRunId = Guid.NewGuid(),
            RuleCode = "BRAND_SCORE_LOW",
            Severity = "Warning",
            Detail = "Brand score is below threshold",
            CreatedAt = now
        };

        Assert.Equal("BRAND_SCORE_LOW", flag.RuleCode);
        Assert.Equal("Warning", flag.Severity);
        Assert.Equal("Brand score is below threshold", flag.Detail);
    }

    [Fact]
    public void PolicyFlag_DefaultValues_AreCorrect()
    {
        var flag = new PolicyFlag();

        Assert.Equal(string.Empty, flag.RuleCode);
        Assert.Equal(string.Empty, flag.Severity);
        Assert.Null(flag.Detail);
    }

    [Fact]
    public void SkillRun_DefaultValues_AreCorrect()
    {
        var run = new SkillRun();

        Assert.Equal(string.Empty, run.SkillName);
        Assert.Equal(string.Empty, run.Category);
        Assert.Equal("pending", run.Status);
        Assert.Null(run.InputBlobReference);
        Assert.Null(run.OutputBlobReference);
        Assert.Null(run.ConfidenceScore);
        Assert.Null(run.ActivityScore);
        Assert.Null(run.FailureReason);
        Assert.Null(run.CompletedAt);
        Assert.Equal(0, run.SequenceIndex);
        Assert.Empty(run.PolicyFlags);
    }

    [Fact]
    public void SkillRun_PropertyAssignment_RoundTrips()
    {
        var now = DateTimeOffset.UtcNow;
        var run = new SkillRun
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            AuditRunId = Guid.NewGuid(),
            SkillName = "brand-audit",
            Category = "brand",
            SequenceIndex = 2,
            Status = "completed",
            ConfidenceScore = 0.91m,
            ActivityScore = 85,
            InputBlobReference = "input-blob-ref",
            OutputBlobReference = "output-blob-ref",
            StartedAt = now,
            CompletedAt = now,
            CreatedAt = now
        };

        Assert.Equal("brand-audit", run.SkillName);
        Assert.Equal("brand", run.Category);
        Assert.Equal("completed", run.Status);
        Assert.Equal(0.91m, run.ConfidenceScore);
        Assert.Equal(85, run.ActivityScore);
        Assert.Equal("input-blob-ref", run.InputBlobReference);
        Assert.Equal("output-blob-ref", run.OutputBlobReference);
    }

    [Fact]
    public void TelemetryEvent_DefaultValues_AreCorrect()
    {
        var evt = new TelemetryEvent();

        Assert.Equal(0, evt.SkillRunCount);
        Assert.Equal(0, evt.PolicyTriggerCount);
        Assert.Equal(0, evt.CouncilRunCount);
        Assert.Equal(0, evt.ReviewerActionCount);
        Assert.Equal(0, evt.TotalTokensUsed);
        Assert.Equal(0, evt.TotalLatencyMs);
        Assert.Null(evt.CompletedAt);
    }

    [Fact]
    public void TelemetryEvent_PropertyAssignment_RoundTrips()
    {
        var now = DateTimeOffset.UtcNow;
        var evt = new TelemetryEvent
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            AuditRunId = Guid.NewGuid(),
            SkillRunCount = 6,
            PolicyTriggerCount = 2,
            CouncilRunCount = 1,
            ReviewerActionCount = 3,
            TotalTokensUsed = 12000,
            TotalLatencyMs = 45000,
            InitializedAt = now,
            CompletedAt = now
        };

        Assert.Equal(6, evt.SkillRunCount);
        Assert.Equal(2, evt.PolicyTriggerCount);
        Assert.Equal(1, evt.CouncilRunCount);
        Assert.Equal(3, evt.ReviewerActionCount);
        Assert.Equal(12000, evt.TotalTokensUsed);
        Assert.Equal(45000, evt.TotalLatencyMs);
        Assert.Equal(now, evt.CompletedAt);
    }

    [Fact]
    public void ReviewerLockout_DefaultValues_AreCorrect()
    {
        var lockout = new ReviewerLockout();

        Assert.Equal(0, lockout.RejectionCount);
        Assert.False(lockout.IsLocked);
    }

    [Fact]
    public void ReviewerLockout_PropertyAssignment_RoundTrips()
    {
        var now = DateTimeOffset.UtcNow;
        var lockout = new ReviewerLockout
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            AuditRunId = Guid.NewGuid(),
            Category = "brand",
            ReviewerUserId = Guid.NewGuid(),
            RejectionCount = 3,
            IsLocked = true,
            WindowStartedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        Assert.Equal("brand", lockout.Category);
        Assert.Equal(3, lockout.RejectionCount);
        Assert.True(lockout.IsLocked);
        Assert.Equal(now, lockout.WindowStartedAt);
    }

    [Fact]
    public void CalibrationDelta_PropertyAssignment_RoundTrips()
    {
        var now = DateTimeOffset.UtcNow;
        var delta = new CalibrationDelta
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            AuditRunId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid().ToString(),
            ReviewerId = Guid.NewGuid(),
            OriginalActivityScore = 70.0m,
            AdjustedActivityScore = 75.5m,
            OverrideReasonCode = "REVIEWER_ADJUSTMENT",
            Notes = "Adjusted upward per client context",
            CreatedAt = now
        };

        Assert.Equal(70.0m, delta.OriginalActivityScore);
        Assert.Equal(75.5m, delta.AdjustedActivityScore);
        Assert.Equal("REVIEWER_ADJUSTMENT", delta.OverrideReasonCode);
        Assert.Equal("Adjusted upward per client context", delta.Notes);
    }

    [Fact]
    public void CalibrationDelta_DefaultValues_AreCorrect()
    {
        var delta = new CalibrationDelta();

        Assert.Equal(string.Empty, delta.CategoryId);
        Assert.Equal(string.Empty, delta.OverrideReasonCode);
        Assert.Equal(string.Empty, delta.Notes);
        Assert.Null(delta.OriginalDocumentedStrategy);
        Assert.Null(delta.AdjustedDocumentedStrategy);
    }

    [Fact]
    public void CategoryResultVersion_PropertyAssignment_RoundTrips()
    {
        var now = DateTimeOffset.UtcNow;
        var actorId = Guid.NewGuid();
        var version = new CategoryResultVersion
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CategoryResultId = Guid.NewGuid(),
            Version = 2,
            ActivityScore = 80,
            ReviewNotes = "Looks good",
            Action = "approve",
            ActorUserId = actorId,
            CreatedAt = now
        };

        Assert.Equal(2, version.Version);
        Assert.Equal(80, version.ActivityScore);
        Assert.Equal("Looks good", version.ReviewNotes);
        Assert.Equal("approve", version.Action);
        Assert.Equal(actorId, version.ActorUserId);
    }

    [Fact]
    public void CategoryResultVersion_DefaultValues_AreCorrect()
    {
        var version = new CategoryResultVersion();

        Assert.Equal(0, version.Version);
        Assert.Equal(0, version.ActivityScore);
        Assert.Null(version.ReviewNotes);
        Assert.Equal(string.Empty, version.Action);
    }

    [Fact]
    public void BlobReference_PropertyAssignment_RoundTrips()
    {
        var now = DateTimeOffset.UtcNow;
        var linkedId = Guid.NewGuid();
        var blob = new BlobReference
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            BlobName = "report-2026.pdf",
            ContainerName = "reports",
            ContentType = "application/pdf",
            SizeBytes = 204800L,
            LinkedEntityType = "AuditRun",
            LinkedEntityId = linkedId,
            CreatedAt = now
        };

        Assert.Equal("report-2026.pdf", blob.BlobName);
        Assert.Equal("reports", blob.ContainerName);
        Assert.Equal("application/pdf", blob.ContentType);
        Assert.Equal(204800L, blob.SizeBytes);
        Assert.Equal("AuditRun", blob.LinkedEntityType);
        Assert.Equal(linkedId, blob.LinkedEntityId);
    }

    [Fact]
    public void BlobReference_DefaultValues_AreCorrect()
    {
        var blob = new BlobReference();

        Assert.Equal(string.Empty, blob.ContainerName);
        Assert.Equal(string.Empty, blob.BlobName);
        Assert.Equal(string.Empty, blob.ContentType);
        Assert.Equal(0L, blob.SizeBytes);
        Assert.Null(blob.LinkedEntityType);
        Assert.Null(blob.LinkedEntityId);
    }

    [Fact]
    public void ReviewerAction_PropertyAssignment_RoundTrips()
    {
        var now = DateTimeOffset.UtcNow;
        var action = new ReviewerAction
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            AuditRunId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid().ToString(),
            ReviewerId = Guid.NewGuid(),
            ActionType = "approve",
            CreatedAt = now
        };

        Assert.Equal("approve", action.ActionType);
        Assert.Equal(now, action.CreatedAt);
    }

    [Fact]
    public void ReviewerAction_DefaultValues_AreCorrect()
    {
        var action = new ReviewerAction();

        Assert.Equal(string.Empty, action.CategoryId);
        Assert.Equal(string.Empty, action.ActionType);
    }

    [Fact]
    public void HubSpotSyncQueue_PropertyAssignment_RoundTrips()
    {
        var now = DateTimeOffset.UtcNow;
        var clientId = Guid.NewGuid();
        var item = new HubSpotSyncQueue
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ClientId = clientId,
            EventType = "deal_won",
            PayloadJson = "{}",
            Status = "pending",
            RetryCount = 0,
            CreatedAt = now
        };

        Assert.Equal("deal_won", item.EventType);
        Assert.Equal("{}", item.PayloadJson);
        Assert.Equal("pending", item.Status);
        Assert.Equal(clientId, item.ClientId);
    }

    [Fact]
    public void HubSpotSyncQueue_DefaultValues_AreCorrect()
    {
        var item = new HubSpotSyncQueue();

        Assert.Equal(string.Empty, item.EventType);
        Assert.Equal(string.Empty, item.PayloadJson);
        Assert.Equal("pending", item.Status);
        Assert.Equal(0, item.RetryCount);
        Assert.Null(item.ClientId);
        Assert.Null(item.LastErrorMessage);
        Assert.Null(item.ProcessedAt);
        Assert.Null(item.NextRetryAt);
    }

    [Fact]
    public void CouncilSession_PropertyAssignment_RoundTrips()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new CouncilSession
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            SkillRunId = Guid.NewGuid(),
            Status = "completed",
            Decision = "override",
            AdjustedScore = 78.5m,
            Rationale = "Council agreed to override",
            CompletedAt = now,
            CreatedAt = now
        };

        Assert.Equal("override", session.Decision);
        Assert.Equal(78.5m, session.AdjustedScore);
        Assert.Equal("Council agreed to override", session.Rationale);
        Assert.Equal("completed", session.Status);
    }

    [Fact]
    public void CouncilSession_DefaultValues_AreCorrect()
    {
        var session = new CouncilSession();

        Assert.Equal("pending", session.Status);
        Assert.Equal(string.Empty, session.Decision);
        Assert.Null(session.AdjustedScore);
        Assert.Null(session.Rationale);
        Assert.Null(session.AdvocateOutputJson);
        Assert.Null(session.SkepticOutputJson);
        Assert.Null(session.JudgeOutputJson);
        Assert.Null(session.CompletedAt);
    }

    [Fact]
    public void CategoryConfig_PropertyAssignment_RoundTrips()
    {
        var now = DateTimeOffset.UtcNow;
        var config = new CategoryConfig
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            AuditId = Guid.NewGuid(),
            Category = "brand",
            IsEnabled = true,
            SortOrder = 1,
            CustomPromptOverride = "Custom prompt text",
            CreatedAt = now,
            UpdatedAt = now
        };

        Assert.Equal("brand", config.Category);
        Assert.True(config.IsEnabled);
        Assert.Equal(1, config.SortOrder);
        Assert.Equal("Custom prompt text", config.CustomPromptOverride);
    }

    [Fact]
    public void CategoryConfig_DefaultValues_AreCorrect()
    {
        var config = new CategoryConfig();

        Assert.Equal(string.Empty, config.Category);
        Assert.True(config.IsEnabled);
        Assert.Equal(0, config.SortOrder);
        Assert.Null(config.CustomPromptOverride);
    }
}
