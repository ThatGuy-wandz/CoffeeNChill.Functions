using Azure.Data.Tables;
using Azure.Storage.Blobs;
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

// Staff docs storage.

{
    var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage")
        ?? "UseDevelopmentStorage=true";

    var blobServiceClient = new BlobServiceClient(connectionString);
    var containerClient = blobServiceClient.GetBlobContainerClient("staff-docs");
    containerClient.CreateIfNotExists();

    return containerClient;
});

builder.Build().Run();