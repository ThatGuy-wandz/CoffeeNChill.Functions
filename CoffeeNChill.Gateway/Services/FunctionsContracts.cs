namespace CoffeeNChill.Gateway.Services;

// "Contracts" = the exact JSON shapes the Azure Functions app sends and receives.
// They are internal plumbing and are NEVER returned to clients. Controllers use the DTOs in
// /Dtos, and GatewayMapper converts between the two. If the Functions app changes its JSON,
// only these classes and the mapper change, not the public API.

public class FnMenuItemCreate
{
    public string Category { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Price { get; set; }
    public bool IsAvailable { get; set; }
}

public class FnMenuItemUpdate
{
    public double? Price { get; set; }
    public bool? IsAvailable { get; set; }
}

public class FnMenuItem
{
    public string Category { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Price { get; set; }
    public bool IsAvailable { get; set; }
}

public class FnOrderRequest
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public List<string> SelectedItemSKUs { get; set; } = new();
    public decimal TotalPrice { get; set; }
}

public class FnOrderQueued
{
    public string Message { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public DateTimeOffset OrderTimestamp { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class FnOrderStatus
{
    public string OrderId { get; set; } = string.Empty;
    public string OrderDate { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public List<string> SelectedItemSKUs { get; set; } = new();
    public double TotalPrice { get; set; }
    public DateTimeOffset OrderTimestamp { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? ReceivedAt { get; set; }
    public DateTimeOffset? PreparingAt { get; set; }
    public DateTimeOffset? ReadyAt { get; set; }
    public DateTimeOffset? CollectedAt { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
}

public class FnOrderList
{
    public int Count { get; set; }
    public List<FnOrderStatus> Orders { get; set; } = new();
}

public class FnStaffDocument
{
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset? LastModified { get; set; }
}

public class FnUploadResult
{
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

/// The { "error": "..." } body every Function returns when something is wrong.
public class FnError
{
    public string? Error { get; set; }
}

/// A file streamed back from the Functions app.
public record FnDownload(Stream Content, string ContentType, string FileName);
