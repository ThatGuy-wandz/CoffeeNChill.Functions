using System.Reflection;
using System.Text;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using CoffeeNChill.Gateway.Dtos;
using CoffeeNChill.Gateway.Middleware;
using CoffeeNChill.Gateway.Models;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

// AI USE DISCLOSURE: Claude (Anthropic) was used to help design and write this gateway.
// I reviewed, tested and understand it. Sources are listed in the README.

var builder = WebApplication.CreateBuilder(args);


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


var functionsBaseUrl = builder.Configuration["FunctionsApi:BaseUrl"] ?? "http://localhost:7120/api/";
builder.Services.AddHttpClient<IFunctionsApiClient, FunctionsApiClient>(client =>
{
    client.BaseAddress = new Uri(functionsBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromMinutes(2);
});

// Routing straight to Azure Storage Tables + Files/Blobs
var storageConnection = builder.Configuration["Storage:ConnectionString"] ?? "UseDevelopmentStorage=true";
builder.Services.AddSingleton(new TableServiceClient(storageConnection));
builder.Services.AddSingleton(new BlobServiceClient(storageConnection));
builder.Services.AddScoped<IOrderStatusService, OrderStatusService>();
builder.Services.AddScoped<IDocumentStorageService, DocumentStorageService>();

// JWT: users, tokens and settings 
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITokenService, TokenService>();

var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
{
    // Fail at startup with a clear message instead of failing later on the first login.
    throw new InvalidOperationException(
        "Jwt:Key is missing or shorter than 32 characters. Set it with 'dotnet user-secrets set \"Jwt:Key\" ...' " +
        "locally, or the Jwt__Key environment variable in Docker/VPS. Never commit it to Git.");
}

// Writes 401/403 responses in the same ErrorResponse shape as every other error.
static Task WriteAuthErrorAsync(HttpContext context, int statusCode, string error, string message)
{
    context.Response.StatusCode = statusCode;
    return context.Response.WriteAsJsonAsync(new ErrorResponse
    {
        StatusCode = statusCode,
        Error = error,
        Message = message,
        TraceId = context.TraceIdentifier,
        Timestamp = DateTimeOffset.UtcNow
    });
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim names exactly as written in the token ("sub", "email", "role").
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub",
            RoleClaimType = "role"
        };

        options.Events = new JwtBearerEvents
        {
            // No token, or an invalid / expired one
            OnChallenge = context =>
            {
                context.HandleResponse();
                return WriteAuthErrorAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
                    "Unauthorized",
                    "Authentication is required. Log in at /api/auth/login and send the token as 'Authorization: Bearer {token}'.");
            },
            // Valid token, but the role is not allowed
            OnForbidden = context => WriteAuthErrorAsync(context.HttpContext, StatusCodes.Status403Forbidden,
                "Forbidden", "Your role does not have permission to perform this action.")
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Every endpoint requires a logged-in user unless it is marked [AllowAnonymous].
    // This stops an endpoint being left open by accident.
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "CoffeeNChill API Gateway",
        Version = "v1",
        Description = "Single entry point for the CoffeeNChill canteen system. " +
                      "Routes menu, order and staff document requests to the Azure Functions " +
                      "app and to Azure Storage (Tables, Files, Queues). " +
                      "Log in at /api/auth/login, then use the Authorize button to send your JWT."
    });

    // JWT: adds the Authorize button so protected endpoints can be tested in Swagger UI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the accessToken from /api/auth/login (without the word Bearer)."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
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

// The usernames and passwords come from user-secrets / environment variables, never from Git.
// Public users are created through /api/auth/register.
static async Task SeedUserAsync(IServiceProvider services, IConfiguration config, ILogger logger,
    string section, string role)
{
    var username = config[$"Seed:{section}:Username"];
    var password = config[$"Seed:{section}:Password"];
    var email = config[$"Seed:{section}:Email"] ?? $"{section.ToLowerInvariant()}@coffeenchill.local";

    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
    {
        logger.LogInformation("No Seed:{Section} credentials configured, skipping.", section);
        return;
    }

    using var scope = services.CreateScope();
    var users = scope.ServiceProvider.GetRequiredService<IUserService>();

    if (await users.FindByUsernameAsync(username) is null)
    {
        await users.CreateAsync(username, email, password, role);
        logger.LogInformation("Seeded {Role} account '{Username}'.", role, username);
    }
}

try
{
    var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    await SeedUserAsync(app.Services, app.Configuration, startupLogger, "Admin", Roles.Admin);
    await SeedUserAsync(app.Services, app.Configuration, startupLogger, "Staff", Roles.Staff);
}
catch (Exception ex)
{
    // Most likely the Users table is not reachable yet (for example Azurite is still starting).
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup")
        .LogWarning(ex, "Could not seed the Admin/Staff accounts. Check storage is running and restart.");
}

// Must be FIRST so it can catch exceptions thrown by everything after it
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "CoffeeNChill API Gateway v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "CoffeeNChill API Gateway";
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Opening the bare address goes straight to Swagger
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription().AllowAnonymous();

app.Run();