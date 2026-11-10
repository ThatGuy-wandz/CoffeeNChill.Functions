using System.Net;
using Azure.Storage.Blobs;
using CoffeeNChill.Gateway.Exceptions;

namespace CoffeeNChill.Gateway.Services;

/// Deletes staff documents from the staff-docs container. Backs DELETE /api/documents/{fileName}.
public class DocumentStorageService : IDocumentStorageService
{
    private const string ContainerName = "staff-docs";

    private readonly BlobServiceClient _blobs;
    private readonly ILogger<DocumentStorageService> _logger;

    public DocumentStorageService(BlobServiceClient blobs, ILogger<DocumentStorageService> logger)
    {
        _blobs = blobs;
        _logger = logger;
    }

    public async Task DeleteAsync(string fileName, CancellationToken ct)
    {
        // Path.GetFileName stops names like "../secret.txt" from reaching outside the container
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            throw new GatewayException(HttpStatusCode.BadRequest, "A valid file name is required.");
        }

        var container = _blobs.GetBlobContainerClient(ContainerName);
        var deleted = await container.GetBlobClient(safeName).DeleteIfExistsAsync(cancellationToken: ct);

        if (!deleted.Value)
        {
            throw new GatewayException(HttpStatusCode.NotFound, $"Document '{safeName}' was not found.");
        }

        _logger.LogInformation("Staff document {FileName} deleted.", safeName);
    }
}
