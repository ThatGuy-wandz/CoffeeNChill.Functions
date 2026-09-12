using System.Net;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CoffeeNChill.Functions.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace CoffeeNChill.Functions.Functions
{
    public class DocumentFunctions
    {
        private readonly BlobContainerClient _containerClient;
        private readonly ILogger<DocumentFunctions> _logger;

        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "image/jpeg",
            "image/png",
            "text/plain"
        };

        private const long MaxUploadSizeBytes = 50 * 1024 * 1024; // 50 MB for a single staff document

        public DocumentFunctions(BlobContainerClient containerClient, ILogger<DocumentFunctions> logger)
        {
            _containerClient = containerClient;
            _logger = logger;
        }

        // POST /api/documents/upload will validate MIME type, then streams the file straight into staff docs

        [Function("UploadStaffDocument")]
        public async Task<HttpResponseData> UploadStaffDocument(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "documents/upload")] HttpRequestData req)
        {
            _logger.LogInformation("Uploading a staff document.");

            if (!TryGetBoundary(req, out var boundary, out var boundaryError))
                return await Error(req, HttpStatusCode.BadRequest, boundaryError!);

            string? fileName = null;
            string? contentType = null;
            MultipartSection? fileSection = null;

            try
            {
                var reader = new MultipartReader(boundary!, req.Body);
                MultipartSection? section;
                while ((section = await reader.ReadNextSectionAsync()) != null)
                {
                    if (section.Headers is null ||
                        !section.Headers.TryGetValue("Content-Disposition", out var cdValues))
                        continue;

                    var contentDisposition = ContentDispositionHeaderValue.Parse(cdValues.ToString());
                    var isFilePart = contentDisposition.DispositionType.Equals("form-data")
                        && !string.IsNullOrEmpty(contentDisposition.FileName.Value);
                    if (!isFilePart)
                        continue;

                    fileName = HeaderUtilities.RemoveQuotes(contentDisposition.FileName).Value;
                    contentType = section.Headers.TryGetValue("Content-Type", out var ctValues)
                        ? ctValues.ToString()
                        : null;
                    fileSection = section;
                    break;
                }

                if (fileSection is null || string.IsNullOrWhiteSpace(fileName))
                    return await Error(req, HttpStatusCode.BadRequest,
                    "No file part was found. Send the request as multipart/form-data with a file field.");

                if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.Contains(contentType))
                {
                    _logger.LogWarning("Rejected staff document upload with unsupported content type {ContentType}.", contentType);
                    return await Error(req, HttpStatusCode.UnsupportedMediaType,
                    $"Content type '{contentType}' is not allowed. Accepted types: {string.Join(", ", AllowedContentTypes)}.");
                }

                fileName = Path.GetFileName(fileName);
                var blobClient = _containerClient.GetBlobClient(fileName);

                var uploadOptions = new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
                };

                await blobClient.UploadAsync(fileSection.Body, uploadOptions);

                var properties = await blobClient.GetPropertiesAsync();
                _logger.LogInformation("Staff document {FileName} uploaded ({SizeBytes} bytes).",
                fileName, properties.Value.ContentLength);

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(new { fileName, sizeBytes = properties.Value.ContentLength });
                return response;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Error reading multipart body while uploading staff document.");
                return await Error(req, HttpStatusCode.BadRequest, "Could not read the uploaded file.");
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure Blob Storage error while uploading staff document.");
                return await Error(req, HttpStatusCode.InternalServerError, "Failed to upload the document.");
            }
        }

        // GET /api/documents list every file in staff-docs with name, size, last modified

        [Function("ListStaffDocuments")]
        public async Task<HttpResponseData> ListStaffDocuments(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents")] HttpRequestData req)
        {
            _logger.LogInformation("Listing staff documents.");
            var items = new List<StaffDocumentDto>();

            try
            {
                await foreach (var blobItem in _containerClient.GetBlobsAsync())
                {
                    items.Add(new StaffDocumentDto
                    {
                        FileName = blobItem.Name,
                        SizeBytes = blobItem.Properties.ContentLength ?? 0,
                        LastModified = blobItem.Properties.LastModified
                    });
                }
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure Blob Storage error while listing staff documents.");
                return await Error(req, HttpStatusCode.InternalServerError, "Failed to list staff documents.");
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(items);
            return response;
        }

        //  streams the file back to the client

        [Function("DownloadStaffDocument")]
        public async Task<HttpResponseData> DownloadStaffDocument(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents/download/{fileName}")] HttpRequestData req,
        string fileName)
        {
            _logger.LogInformation("Downloading staff document {FileName}.", fileName);

            if (string.IsNullOrWhiteSpace(fileName))
                return await Error(req, HttpStatusCode.BadRequest, "File name route parameter is required.");

            var blobClient = _containerClient.GetBlobClient(fileName);
            BlobDownloadInfo download;

            try
            {
                if (!await blobClient.ExistsAsync())
                    return await Error(req, HttpStatusCode.NotFound, $"Document '{fileName}' was not found.");

                download = await blobClient.DownloadAsync();
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                return await Error(req, HttpStatusCode.NotFound, $"Document '{fileName}' was not found.");
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure Blob Storage error while downloading staff document.");
                return await Error(req, HttpStatusCode.InternalServerError, "Failed to download the document.");
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type",
            string.IsNullOrWhiteSpace(download.ContentType) ? "application/octet-stream" : download.ContentType);
            response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
            await download.Content.CopyToAsync(response.Body);
            return response;
        }

        // Helpers

        private static bool TryGetBoundary(HttpRequestData req, out string? boundary, out string? error)
        {
            boundary = null;
            error = null;

            if (!req.Headers.TryGetValues("Content-Type", out var values))
            {
                error = "Content-Type header is required and must be multipart/form-data.";
                return false;
            }

            var contentType = MediaTypeHeaderValue.Parse(values.First());
            boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).Value;

            if (string.IsNullOrWhiteSpace(boundary))
            {
                error = "Content-Type header is missing a multipart boundary.";
                return false;
            }
            return true;
        }

        private static async Task<HttpResponseData> Error(HttpRequestData req, HttpStatusCode statusCode, string message)
        {
            var response = req.CreateResponse(statusCode);
            await response.WriteAsJsonAsync(new { error = message });
            return response;
        }
    }
}