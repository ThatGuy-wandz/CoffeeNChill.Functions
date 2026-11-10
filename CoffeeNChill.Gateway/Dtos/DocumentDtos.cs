using System.ComponentModel.DataAnnotations;

namespace CoffeeNChill.Gateway.Dtos;

/// A staff document stored in the staff-docs file share.
public class StaffDocumentResponse
{
    /// <example>cleaning-manual.pdf
    public string FileName { get; set; } = string.Empty;

    /// Size in bytes.
    /// <example>20480
    public long SizeBytes { get; set; }

    /// Size in kilobytes, rounded to 1 decimal.
    /// <example>20.0
    public double SizeKilobytes { get; set; }

    public DateTimeOffset? LastModified { get; set; }
}

/// Confirmation that a document was uploaded.
public class DocumentUploadResponse
{
    /// <example>cleaning-manual.pdf
    public string FileName { get; set; } = string.Empty;

    /// <example>20480
    public long SizeBytes { get; set; }
}

/// Form body for uploading a staff document (multipart/form-data).
public class UploadDocumentRequest
{
    /// The file to upload. PDF, Word, JPEG, PNG or plain text, up to 50 MB.
    [Required]
    public IFormFile File { get; set; } = default!;
}