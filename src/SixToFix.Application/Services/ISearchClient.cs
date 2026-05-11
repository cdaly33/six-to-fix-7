using SixToFix.Application.Models;

namespace SixToFix.Application.Services;

public interface ISearchClient
{
    Task IndexDocumentAsync(string indexName, string documentId, IDictionary<string, object> fields, CancellationToken ct = default);
    Task<SearchResult> SearchAsync(string indexName, string query, string tenantId, string? additionalFilter = null, CancellationToken ct = default);
    Task DeleteDocumentAsync(string indexName, string documentId, CancellationToken ct = default);
}
