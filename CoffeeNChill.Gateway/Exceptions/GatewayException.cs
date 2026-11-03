using System.Net;

namespace CoffeeNChill.Gateway.Exceptions;

/// <summary>
/// An expected, "business" failure (not found, conflict, bad input, downstream error).
/// Code anywhere in the gateway throws it, and <c>GlobalExceptionMiddleware</c> turns it into
/// a clean JSON error with the right HTTP status code.
/// </summary>
public class GatewayException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public GatewayException(HttpStatusCode statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}
