using System.Net;
using Azure;
using Azure.Data.Tables;
using CoffeeNChill.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
namespace CoffeeNChill.Functions.Functions
{
    // Lets Postman (and the video) see WHY an order message failed.
    public class FailedOrderFunctions
    {
        private readonly TableClient _failedOrdersTable;
        private readonly ILogger<FailedOrderFunctions> _logger;
        public FailedOrderFunctions(
TableServiceClient tableServiceClient,
ILogger<FailedOrderFunctions> logger)
        {
            _failedOrdersTable = tableServiceClient.GetTableClient(FailedOrderEntity.TableName);
            _logger = logger;
        }
        // GET /api/orders/failed -> every message that failed its final attempt
        [Function("GetFailedOrders")]
        public async Task<HttpResponseData> GetFailedOrders(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/failed")]
HttpRequestData req)
        {
            try
            {
                await _failedOrdersTable.CreateIfNotExistsAsync();
                var failed = new List<object>();
                await foreach (var row in _failedOrdersTable.QueryAsync<FailedOrderEntity>())
                {
                    failed.Add(new
                    {
                        messageId = row.RowKey,
                        failedDate = row.PartitionKey,
                        reason = row.Reason,
                        exceptionType = row.ExceptionType,
                        attempts = row.DequeueCount,
                        failedAt = row.FailedAt,
                        rawMessage = row.RawMessage
                    });
                }
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { count = failed.Count, failedOrders = failed });
                return response;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "FailedOrders table error while listing failures.");
                var response = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
                await response.WriteAsJsonAsync(new
                {
                    error = "The FailedOrders table is currently unavailable. Try again shortly."
                });
                return response;
            }
        }
    }
}