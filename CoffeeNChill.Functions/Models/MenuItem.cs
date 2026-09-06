using Azure;
using Azure.Data.Tables;
namespace CoffeeNChill.Functions.Models
{
    // Summary
    // Azure Table entity for a single menu item.
    // PartitionKey = Category (e.g. "Hot Drinks", "Pastries")
    // RowKey = Item SKU / ID (e.g. "COF-001")
    
    public class MenuItem : ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;
        public double Price { get; set; }
        public bool IsAvailable { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}