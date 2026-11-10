namespace CoffeeNChill.Gateway.Dtos;

/// The one error format returned by every gateway endpoint.
public class ErrorResponse
{
    /// HTTP status code.
    /// <example>404
    public int StatusCode { get; set; }

    /// Short name of the status code.
    /// <example>Not Found
    public string Error { get; set; } = string.Empty;

    /// What went wrong, in plain English.
    /// Order 'ORD-1' was not found.
    public string Message { get; set; } = string.Empty;

    /// Field-by-field validation problems (only present for 400 validation errors).
    public Dictionary<string, string[]>? Details { get; set; }

    /// Id that lets you find this request in the gateway logs.
    /// <example>0HN7A2B3C4D5E:00000001
    public string TraceId { get; set; } = string.Empty;

    /// When the error happened (UTC).
    public DateTimeOffset Timestamp { get; set; }
}
