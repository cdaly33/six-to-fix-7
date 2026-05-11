using SixToFix.Application.Models;

namespace SixToFix.Application.Services;

public interface IPublisher
{
    Task<PublishResult> PublishAuditAsync(Guid auditRunId, Guid publishedByUserId, CancellationToken ct = default);
    Task<PublishedAuditSummary> GetPublishedAuditAsync(string clientSlug, CancellationToken ct = default);
    Task<PublishedAuditSummary> GetPublishedAuditByRunIdAsync(Guid auditRunId, CancellationToken ct = default);
    Task<IReadOnlyList<PublishedAuditVersion>> GetPublishedVersionsAsync(string clientSlug, CancellationToken ct = default);
}
