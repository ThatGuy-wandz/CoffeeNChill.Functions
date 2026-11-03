using System.Reflection;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using CoffeeNChill.Gateway.Dtos;
using CoffeeNChill.Gateway.Middleware;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Mvc;

// AI USE DISCLOSURE: Claude (Anthropic) was used to help design and write this gateway.
// I reviewed, tested and understand it. Sources are listed in the README.

var builder = WebApplication.CreateBuilder(args);

// ---------- Controllers + one consistent shape for validation errors ----------
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // When a request body fails its [Required]/[Range] rules, [ApiController] would
        // return its own format. We replace it so every error looks the same (ErrorResponse).
        options.InvalidModelStateResponseFactory = context =>
        {
            var details = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors
                        .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage)
                        .ToArray());

            return new BadRequestObjectResult(new ErrorResponse
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Error = "Bad Request",
                Message = "One or more validation errors occurred.",
                Details = details,
                TraceId = context.HttpContext.TraceIdentifier,
                Timestamp = DateTimeOffset.UtcNow
            });
        };
    });

// ---------- Routing to the Azure Functions app (typed HttpClient) ----------
// Locally the Functions app is on 7120. In Docker Compose set FunctionsApi__BaseUrl to
// http://functions:80/api/ (see README).
var functionsBaseUrl = builder.Configuration["FunctionsApi:BaseUrl"] ?? "http://localhost:7120/api/";
builder.Services.AddHttpClient<IFunctionsApiClient, FunctionsApiClient>(client =>
{
    client.BaseAddress = new Uri(functionsBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromMinutes(2);
});

// ---------- Routing straight to Azure Storage (Tables + Files/Blobs) ----------
var storageConnection = builder.Configuration["Storage:ConnectionString"] ?? "UseDevelopmentStorage=true";
builder.Services.AddSingleton(new TableServiceClient(storageConnection));
builder.Services.AddSingleton(new BlobServiceClient(storageConnection));
builder.Services.AddScoped<IOrderStatusService, OrderStatusService>();
builder.Services.AddScoped<IDocumentStorageService, DocumentStorageService>();

// ---------- Swagger / OpenAPI (Swashbuckle) ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "CoffeeNChill API Gateway",
        Version = "v1",
        Description = "Single entry point for the CoffeeNChill canteen system. " +
                      "Routes menu, order and staff document requests to the Azure Functions " +
                      "app and to Azure Storage (Tables, Files, Queues)."
    });

    // Show the /// XML comments (summaries, parameters, examples, response codes) in Swagger UI
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Must be FIRST so it can catch exceptions thrown by everything after it
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "CoffeeNChill API Gateway v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "CoffeeNChill API Gateway";
});

// JWT SECTION (teammate): add these two lines here, in this order:
// app.UseAuthentication();
// app.UseAuthorization();

app.MapControllers();

// Opening the bare address goes straight to Swagger
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();
