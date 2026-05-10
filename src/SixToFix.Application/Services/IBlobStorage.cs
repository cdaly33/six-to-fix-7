using System.IO;
using SixToFix.Application.Models;

namespace SixToFix.Application.Services;

public interface IBlobStorage
{
    Task<BlobUploadResult> UploadAsync(string containerName, string blobPath, Stream content, string contentType, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string containerName, string blobPath, CancellationToken ct = default);
    Task DeleteAsync(string containerName, string blobPath, CancellationToken ct = default);
    Task<Uri> GetSasUriAsync(string containerName, string blobPath, TimeSpan expiry, CancellationToken ct = default);
}
