using System.Collections.ObjectModel;
using System.Threading;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using SixToFix.Application.Services;
using ApplicationSearchResult = SixToFix.Application.Models.SearchResult;

namespace SixToFix.Infrastructure.ExternalClients;

public sealed class AzureSearchClient : ISearchClient
{
    private readonly TokenCredential _credential;
    private readonly Uri _endpoint;
    private readonly SearchIndexClient _indexClient;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexesEnsured;

    public AzureSearchClient(IConfiguration configuration)
        : this(
            new Uri(configuration["Search:Endpoint"]
                ?? throw new InvalidOperationException("Search:Endpoint is not configured.")),
            new DefaultAzureCredential())
    {
    }

    internal AzureSearchClient(Uri endpoint, TokenCredential credential)
    {
        _endpoint = endpoint;
        _credential = credential;
        _indexClient = new SearchIndexClient(_endpoint, _credential);
    }

    internal static IReadOnlyList<string> RequiredIndexes { get; } =
        new ReadOnlyCollection<string>([
            "six-to-fix-evidence",
            "six-to-fix-skill-outputs",
            "six-to-fix-calibration"
        ]);

    public async Task IndexDocumentAsync(
        string indexName,
        string documentId,
        IDictionary<string, object> fields,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(ct);

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
        string? additionalFilter = null,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(ct);

        var client = new SearchClient(_endpoint, indexName, _credential);
        var options = new SearchOptions
        {
            Filter = BuildFilter(tenantId, additionalFilter),
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
        await EnsureIndexesAsync(ct);

        var client = new SearchClient(_endpoint, indexName, _credential);
        await client.DeleteDocumentsAsync("id", [documentId], cancellationToken: ct);
    }

    internal static string BuildFilter(string tenantId, string? additionalFilter = null)
    {
        var sanitizedTenantId = tenantId.Replace("'", "''", StringComparison.Ordinal);
        var tenantFilter = $"tenantId eq '{sanitizedTenantId}'";
        return string.IsNullOrWhiteSpace(additionalFilter)
            ? tenantFilter
            : $"{tenantFilter} and ({additionalFilter})";
    }

    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        if (_indexesEnsured)
        {
            return;
        }

        await _indexLock.WaitAsync(ct);
        try
        {
            if (_indexesEnsured)
            {
                return;
            }

            foreach (var index in BuildRequiredIndexes())
            {
                try
                {
                    await _indexClient.GetIndexAsync(index.Name, ct);
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    await _indexClient.CreateOrUpdateIndexAsync(index, cancellationToken: ct);
                }
            }

            _indexesEnsured = true;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private static IEnumerable<SearchIndex> BuildRequiredIndexes()
    {
        yield return new SearchIndex("six-to-fix-evidence", [
            new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
            new SimpleField("tenantId", SearchFieldDataType.String) { IsFilterable = true },
            new SimpleField("clientId", SearchFieldDataType.String) { IsFilterable = true },
            new SimpleField("documentId", SearchFieldDataType.String) { IsFilterable = true },
            new SimpleField("area", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
            new SearchableField("content"),
            new SearchableField("documentTitle"),
            new SimpleField("chunkIndex", SearchFieldDataType.Int32) { IsFilterable = true, IsSortable = true },
            new SimpleField("uploadedAt", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true }
        ]);

        yield return new SearchIndex("six-to-fix-skill-outputs", [
            new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
            new SimpleField("tenantId", SearchFieldDataType.String) { IsFilterable = true },
            new SimpleField("auditRunId", SearchFieldDataType.String) { IsFilterable = true },
            new SimpleField("skillRunId", SearchFieldDataType.String) { IsFilterable = true },
            new SimpleField("skillName", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
            new SimpleField("evidenceType", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
            new SearchableField("content"),
            new SimpleField("rawJsonPath", SearchFieldDataType.String),
            new SimpleField("tier", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
            new SimpleField("compositeScore", SearchFieldDataType.Int32) { IsFilterable = true, IsSortable = true },
            new SimpleField("completedAt", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true },
            new SimpleField("clientId", SearchFieldDataType.String) { IsFilterable = true }
        ]);

        yield return new SearchIndex("six-to-fix-calibration", [
            new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
            new SimpleField("tenantId", SearchFieldDataType.String) { IsFilterable = true },
            new SimpleField("auditRunId", SearchFieldDataType.String) { IsFilterable = true },
            new SimpleField("area", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
            new SimpleField("originalScore", SearchFieldDataType.Double) { IsFilterable = true, IsSortable = true },
            new SimpleField("adjustedScore", SearchFieldDataType.Double) { IsFilterable = true, IsSortable = true },
            new SimpleField("scoreDelta", SearchFieldDataType.Double) { IsFilterable = true, IsSortable = true },
            new SimpleField("overrideReasonCode", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
            new SearchableField("notes"),
            new SimpleField("recordedAt", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true }
        ]);
    }
}
