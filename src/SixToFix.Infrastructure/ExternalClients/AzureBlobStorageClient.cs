using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using SixToFix.Application.Models;
using SixToFix.Application.Services;

namespace SixToFix.Infrastructure.ExternalClients;

public sealed class AzureBlobStorageClient : IBlobStorage
{
    private readonly BlobServiceClient _serviceClient;

    public AzureBlobStorageClient(IConfiguration configuration)
    {
        var endpoint = configuration["Storage:BlobEndpoint"]
            ?? throw new InvalidOperationException("Storage:BlobEndpoint is not configured.");

        _serviceClient = new BlobServiceClient(new Uri(endpoint), new DefaultAzureCredential());
    }

    public async Task<BlobUploadResult> UploadAsync(
        string containerName,
        string blobPath,
        Stream content,
        string contentType,
        CancellationToken ct = default)
    {
        var containerClient = _serviceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: ct);

        var blobClient = containerClient.GetBlobClient(blobPath);
        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType
            }
        };

        var response = await blobClient.UploadAsync(content, uploadOptions, ct);
        return new BlobUploadResult(blobClient.Uri.ToString(), response.Value.ETag.ToString());
    }

    public async Task<Stream> DownloadAsync(string containerName, string blobPath, CancellationToken ct = default)
    {
        var blobClient = _serviceClient.GetBlobContainerClient(containerName).GetBlobClient(blobPath);
        var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
        return response.Value.Content;
    }

    public async Task DeleteAsync(string containerName, string blobPath, CancellationToken ct = default)
    {
        var blobClient = _serviceClient.GetBlobContainerClient(containerName).GetBlobClient(blobPath);
        await blobClient.DeleteIfExistsAsync(cancellationToken: ct);
    }

    public Task<Uri> GetSasUriAsync(string containerName, string blobPath, TimeSpan expiry, CancellationToken ct = default)
    {
        var blobClient = _serviceClient.GetBlobContainerClient(containerName).GetBlobClient(blobPath);
        if (!blobClient.CanGenerateSasUri)
        {
            throw new InvalidOperationException(
                "BlobServiceClient is not configured with a shared key credential for SAS generation.");
        }

        return Task.FromResult(blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(expiry)));
    }
}
