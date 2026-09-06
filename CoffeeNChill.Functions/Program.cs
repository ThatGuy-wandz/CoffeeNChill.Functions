using Azure.Data.Tables;
using Azure.Storage.Files.Shares;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddSingleton(sp =>
{
    var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage")
        ?? "UseDevelopmentStorage=true";

    var serviceClient = new TableServiceClient(connectionString);
    var tableClient = serviceClient.GetTableClient("MenuItems");
    tableClient.CreateIfNotExists();

    return tableClient;
});

// Added for Azure Files integration (staff-docs).
// Azurite does not emulate the Azure Files service, so this must point at a
// real Storage Account connection string set in local.settings.json as
// "StaffDocsStorage" — it cannot fall back to "UseDevelopmentStorage=true".
builder.Services.AddSingleton(sp =>
{
    var connectionString = Environment.GetEnvironmentVariable("StaffDocsStorage")
        ?? throw new InvalidOperationException(
        "StaffDocsStorage connection string is not set. Add it to local.settings.json " +
        "(Azurite does not support Azure File Shares, so this must point at a real Storage Account).");

    var shareClient = new ShareClient(connectionString, "staff-docs");
    shareClient.CreateIfNotExists();

    return shareClient.GetRootDirectoryClient();
});

builder.Build().Run();