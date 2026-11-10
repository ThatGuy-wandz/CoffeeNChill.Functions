using System.Net;
using System.Text.Json;
using CoffeeNChill.Gateway.Exceptions;
using CoffeeNChill.Gateway.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoffeeNChill.Gateway.Tests;

public class GlobalExceptionMiddlewareTests
{
    private static async Task<(int Status, JsonElement Body)> Run(RequestDelegate next)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };
        context.Response.Body = new MemoryStream();

        await new GlobalExceptionMiddleware(next, NullLogger<GlobalExceptionMiddleware>.Instance).InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, body.RootElement.Clone());
    }

    [Fact]
    public async Task GatewayException_IsReturnedWithItsOwnStatusAndMessage()
    {
        var (status, body) = await Run(_ => throw new GatewayException(HttpStatusCode.NotFound, "Order 'X' was not found."));

        Assert.Equal(404, status);
        Assert.Equal("Order 'X' was not found.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task UnexpectedException_Returns500WithoutLeakingDetails()
    {
        var (status, body) = await Run(_ => throw new InvalidOperationException("secret internal detail"));

        Assert.Equal(500, status);
        Assert.DoesNotContain("secret internal detail", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task HttpRequestException_Returns502()
    {
        var (status, _) = await Run(_ => throw new HttpRequestException("functions down"));
        Assert.Equal(502, status);
    }
}