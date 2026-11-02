using Azure;
using Azure.Data.Tables;
namespace CoffeeNChill.Functions.Models
{
    // One row in the "FailedOrders" table. ProcessOrderQueue writes a row here when a
    // message has failed its final attempt, so the REASON for the failure is kept.
    // (The poison queue only keeps the message text, not why it failed.)
    // PartitionKey = date of failure (yyyy-MM-dd), RowKey = queue MessageId
    public class FailedOrderEntity : ITableEntity
    {
        public const string TableName = "FailedOrders";
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public string Reason { get; set; } = string.Empty;
        public string ExceptionType { get; set; } = string.Empty;
        public string RawMessage { get; set; } = string.Empty;
        public long DequeueCount { get; set; }
        public DateTimeOffset FailedAt { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
