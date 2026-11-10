using System.Net;
using CoffeeNChill.Gateway.Dtos;
using CoffeeNChill.Gateway.Exceptions;
using CoffeeNChill.Gateway.Mapping;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.Gateway.Controllers;


/// Staff operational documents (recipes, cleaning manuals, policies) kept in the
/// staff-docs file share. Staff only: every endpoint needs an Admin or Staff token.

[ApiController]
[Route("api/documents")]
[Produces("application/json")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
public class DocumentsController : ControllerBase
{
    private const long MaxUploadBytes = 50L * 1024 * 1024; // 50 MB, same as the Functions app

    private readonly IFunctionsApiClient _functions;
    private readonly IDocumentStorageService _storage;

    public DocumentsController(IFunctionsApiClient functions, IDocumentStorageService storage)
    {
        _functions = functions;
        _storage = storage;
    }

    /// List all staff documents.
    /// <response code="200">File name, size and last modified date of each document.
    /// <response code="401">No token, or the token is invalid or expired.
    /// <response code="403">The user's role is not allowed to do this.
    [Authorize(Roles = "Admin,Staff")]
    [HttpGet]
    [ProducesResponseType(typeof(List<StaffDocumentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<StaffDocumentResponse>>> List(CancellationToken ct)
    {
        var documents = await _functions.ListDocumentsAsync(ct);
        return Ok(documents.Select(d => d.ToResponse()).ToList());
    }

    /// Upload a staff document.
    /// 
    /// Send the file as multipart/form-data in a field called <c>file</c>.
    /// Allowed types: PDF, Word, JPEG, PNG and plain text. Maximum size 50 MB.
    
    /// <param name="request">The form containing the file
    /// <response code="201">The document was stored.
    /// <response code="400">No file was sent, or it was empty.
    /// <response code="401">No token, or the token is invalid or expired.
    /// <response code="403">The user's role is not allowed to do this.
    /// <response code="415">The file type is not allowed.
    [Authorize(Roles = "Admin,Staff")]
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType(typeof(DocumentUploadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<DocumentUploadResponse>> Upload(
        [FromForm] UploadDocumentRequest request, CancellationToken ct)
    {
        var file = request.File;
        if (file is null || file.Length == 0)
        {
            throw new GatewayException(HttpStatusCode.BadRequest,
                "No file was received. Send a non-empty file in a form field called 'file'.");
        }

        await using var stream = file.OpenReadStream();
        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;

        var result = await _functions.UploadDocumentAsync(stream, file.FileName, contentType, ct);
        var response = result.ToResponse();

        return Created($"/api/documents/download/{Uri.EscapeDataString(response.FileName)}", response);
    }

    /// Download a staff document.
    /// <param name="fileName">Exact file name, for example cleaning-manual.pdf.
    /// <response code="200">The file contents.
    /// <response code="401">No token, or the token is invalid or expired.
    /// <response code="403">The user's role is not allowed to do this.
    /// <response code="404">The document does not exist.
    [Authorize(Roles = "Admin,Staff")]
    [HttpGet("download/{fileName}")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(string fileName, CancellationToken ct)
    {
        var download = await _functions.DownloadDocumentAsync(fileName, ct);
        return File(download.Content, download.ContentType, download.FileName);
    }

    /// Delete a staff document.
    /// Deletes straight from the staff-docs container.
    /// <param name="fileName">Exact file name to delete.
    /// <response code="204">The document was deleted.
    /// <response code="401">No token, or the token is invalid or expired.
    /// <response code="403">The user's role is not allowed to do this.
    /// <response code="404">The document does not exist.
    [Authorize(Roles = "Admin,Staff")]
    [HttpDelete("{fileName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string fileName, CancellationToken ct)
    {
        await _storage.DeleteAsync(fileName, ct);
        return NoContent();
    }
}