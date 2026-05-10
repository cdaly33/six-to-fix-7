using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using SixToFix.Application.Services;
using ApplicationSearchResult = SixToFix.Application.Models.SearchResult;

namespace SixToFix.Infrastructure.ExternalClients;

public sealed class AzureSearchClient : ISearchClient
{
    private readonly DefaultAzureCredential _credential;
    private readonly Uri _endpoint;

    public AzureSearchClient(IConfiguration configuration)
    {
        var endpoint = configuration["Search:Endpoint"]
            ?? throw new InvalidOperationException("Search:Endpoint is not configured.");

        _endpoint = new Uri(endpoint);
        _credential = new DefaultAzureCredential();
    }

    public async Task IndexDocumentAsync(
        string indexName,
        string documentId,
        IDictionary<string, object> fields,
        CancellationToken ct = default)
    {
        var client = new SearchClient(_endpoint, indexName, _credential);
        var document = new SearchDocument();
        foreach (var field in fields)
        {
            document[field.Key] = field.Value;
        }

        document["id"] = documentId;
        await client.MergeOrUploadDocumentsAsync([document], cancellationToken: ct);
    }

    public async Task<ApplicationSearchResult> SearchAsync(
        string indexName,
        string query,
        string tenantId,
        CancellationToken ct = default)
    {
        var client = new SearchClient(_endpoint, indexName, _credential);
        var options = new SearchOptions
        {
            Filter = $"tenantId eq '{tenantId}'",
            IncludeTotalCount = true
        };

        var response = await client.SearchAsync<SearchDocument>(query, options, ct);
        var documents = new List<IDictionary<string, object>>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            documents.Add(new Dictionary<string, object>(result.Document));
        }

        return new ApplicationSearchResult(documents.AsReadOnly(), response.Value.TotalCount ?? documents.Count);
    }

    public async Task DeleteDocumentAsync(string indexName, string documentId, CancellationToken ct = default)
    {
        var client = new SearchClient(_endpoint, indexName, _credential);
        await client.DeleteDocumentsAsync("id", [documentId], cancellationToken: ct);
    }
}
