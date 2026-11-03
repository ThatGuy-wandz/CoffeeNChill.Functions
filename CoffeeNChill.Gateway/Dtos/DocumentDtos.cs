using System.ComponentModel.DataAnnotations;

namespace CoffeeNChill.Gateway.Dtos;

/// <summary>A staff document stored in the staff-docs file share.</summary>
public class StaffDocumentResponse
{
    /// <example>cleaning-manual.pdf</example>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Size in bytes.</summary>
    /// <example>20480</example>
    public long SizeBytes { get; set; }

    /// <summary>Size in kilobytes, rounded to 1 decimal.</summary>
    /// <example>20.0</example>
    public double SizeKilobytes { get; set; }

    public DateTimeOffset? LastModified { get; set; }
}

/// <summary>Confirmation that a document was uploaded.</summary>
public class DocumentUploadResponse
{
    /// <example>cleaning-manual.pdf</example>
    public string FileName { get; set; } = string.Empty;

    /// <example>20480</example>
    public long SizeBytes { get; set; }
}

/// <summary>Form body for uploading a staff document (multipart/form-data).</summary>
public class UploadDocumentRequest
{
    /// <summary>The file to upload. PDF, Word, JPEG, PNG or plain text, up to 50 MB.</summary>
    [Required]
    public IFormFile File { get; set; } = default!;
}