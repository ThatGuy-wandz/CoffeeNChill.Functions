using System.Text.Json;
using Azure;
using CoffeeNChill.Gateway.Dtos;
using CoffeeNChill.Gateway.Exceptions;
using Microsoft.AspNetCore.WebUtilities;

namespace CoffeeNChill.Gateway.Middleware;

/// <summary>
/// Global error handling. Every exception that escapes a controller or service is caught here,
/// logged once, and returned to the client as the same JSON <see cref="ErrorResponse"/>.
/// Clients never see stack traces or internal details.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client closed the connection. Nobody is listening, so there is nothing to send.
            _logger.LogInformation("Request {Path} was cancelled by the client.", context.Request.Path);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                // Part of the response is already sent, so we cannot change it any more.
                _logger.LogError(ex, "Error after the response started for {Path}.", context.Request.Path);
                throw;
            }

            await WriteErrorAsync(context, ex);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, Exception ex)
    {
        var info = Map(ex);

        // Server-side faults (5xx) get the full exception in the log; client mistakes (4xx) do not.
        if (info.Status >= 500)
        {
            _logger.LogError(ex, "Unhandled error {Status} on {Method} {Path}. TraceId={TraceId}",
                info.Status, context.Request.Method, context.Request.Path, context.TraceIdentifier);
        }
        else
        {
            _logger.LogWarning("Request failed {Status} on {Method} {Path}: {Message}",
                info.Status, context.Request.Method, context.Request.Path, info.Message);
        }

        context.Response.StatusCode = info.Status;

        await context.Response.WriteAsJsonAsync(new ErrorResponse
        {
            StatusCode = info.Status,
            Error = ReasonPhrases.GetReasonPhrase(info.Status),
            Message = info.Message,
            TraceId = context.TraceIdentifier,
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    // Decides which status code and (safe) message the client gets for each kind of exception.
    private static ErrorInfo Map(Exception ex) => ex switch
    {
        GatewayException g => new((int)g.StatusCode, g.Message),
        BadHttpRequestException => new(StatusCodes.Status400BadRequest,
            "The request was malformed or too large."),
        JsonException => new(StatusCodes.Status502BadGateway,
            "The Functions app returned data the gateway could not read."),
        HttpRequestException => new(StatusCodes.Status502BadGateway,
            "The Functions app could not be reached. Please try again shortly."),
        RequestFailedException => new(StatusCodes.Status503ServiceUnavailable,
            "Azure Storage is currently unavailable. Please try again shortly."),
        TimeoutException or TaskCanceledException => new(StatusCodes.Status504GatewayTimeout,
            "The Functions app took too long to respond."),
        _ => new(StatusCodes.Status500InternalServerError,
            "An unexpected error occurred. Please quote the trace id when reporting it.")
    };

    private readonly record struct ErrorInfo(int Status, string Message);
}
