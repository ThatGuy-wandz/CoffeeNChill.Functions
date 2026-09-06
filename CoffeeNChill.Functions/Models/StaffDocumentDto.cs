namespace CoffeeNChill.Functions.Models
{
    // Shape returned by GET /api/documents
    public class StaffDocumentDto
    {
        public string FileName { get; set; } = default!;
        public long SizeBytes { get; set; }
        public DateTimeOffset? LastModified { get; set; }
    }
}
