namespace SixToFix.Application.Services;

public interface IAuditOrchestrator
{
    Task<AuditRun> CreateAuditRunAsync(Guid clientId, Guid createdByUserId, CancellationToken ct = default);
    Task StartAuditRunAsync(Guid auditRunId, CancellationToken ct = default);
    Task<AuditRun> GetAuditRunAsync(Guid auditRunId, CancellationToken ct = default);
    Task<IReadOnlyList<AuditRun>> GetAuditRunsForClientAsync(Guid clientId, CancellationToken ct = default);
    Task MarkAuditRunFailedAsync(Guid auditRunId, string failureReason, CancellationToken ct = default);
}
