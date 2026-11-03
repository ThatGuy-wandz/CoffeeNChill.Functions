namespace CoffeeNChill.Gateway.Dtos;

/// <summary>The one error format returned by every gateway endpoint.</summary>
public class ErrorResponse
{
    /// <summary>HTTP status code.</summary>
    /// <example>404</example>
    public int StatusCode { get; set; }

    /// <summary>Short name of the status code.</summary>
    /// <example>Not Found</example>
    public string Error { get; set; } = string.Empty;

    /// <summary>What went wrong, in plain English.</summary>
    /// <example>Order 'ORD-1' was not found.</example>
    public string Message { get; set; } = string.Empty;

    /// <summary>Field-by-field validation problems (only present for 400 validation errors).</summary>
    public Dictionary<string, string[]>? Details { get; set; }

    /// <summary>Id that lets you find this request in the gateway logs.</summary>
    /// <example>0HN7A2B3C4D5E:00000001</example>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>When the error happened (UTC).</summary>
    public DateTimeOffset Timestamp { get; set; }
}
