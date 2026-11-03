using System.Net;
using Azure;
using Azure.Data.Tables;
using CoffeeNChill.Gateway.Dtos;
using CoffeeNChill.Gateway.Exceptions;
using CoffeeNChill.Gateway.Mapping;

namespace CoffeeNChill.Gateway.Services;

/// <summary>
/// Moves an order forward (Received, Preparing, Ready, Collected) by updating its row in the
/// Orders Azure Table directly. Backs PUT /api/orders/status.
/// </summary>
public class OrderStatusService : IOrderStatusService
{
    private const string OrdersTableName = "Orders";

    private readonly TableServiceClient _tables;
    private readonly ILogger<OrderStatusService> _logger;

    public OrderStatusService(TableServiceClient tables, ILogger<OrderStatusService> logger)
    {
        _tables = tables;
        _logger = logger;
    }

    public async Task<OrderStatusResponse> AdvanceStatusAsync(
        string orderId, string newStatus, CancellationToken ct)
    {
        // 1. The requested status must be one of the four lifecycle stages
        var target = OrderStatuses.Lifecycle.FirstOrDefault(
            s => s.Equals(newStatus.Trim(), StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            throw new GatewayException(HttpStatusCode.BadRequest,
                $"Status must be one of: {string.Join(", ", OrderStatuses.Lifecycle)}.");
        }

        // 2. Find the order (PartitionKey is the date, so search by RowKey = OrderId)
        var id = orderId.Trim();
        var table = _tables.GetTableClient(OrdersTableName);
        TableEntity? row = null;

        try
        {
            await foreach (var candidate in table.QueryAsync<TableEntity>(
                TableClient.CreateQueryFilter($"RowKey eq {id}"), cancellationToken: ct))
            {
                if (row is null || Placed(candidate) > Placed(row)) row = candidate;
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            row = null; // the Orders table does not exist yet, so the order cannot either
        }

        if (row is null)
        {
            throw new GatewayException(HttpStatusCode.NotFound,
                $"Order '{id}' was not found. It may still be waiting in the queue.");
        }

        // 3. Status can only move forward
        var current = Array.IndexOf(OrderStatuses.Lifecycle, row.GetString("Status") ?? string.Empty);
        var wanted = Array.IndexOf(OrderStatuses.Lifecycle, target);
        if (wanted <= current)
        {
            throw new GatewayException(HttpStatusCode.Conflict,
                $"Order '{id}' is already '{row.GetString("Status")}'. Status can only move forward: " +
                $"{string.Join(" -> ", OrderStatuses.Lifecycle)}.");
        }

        // 4. Save (Merge keeps every other column; the ETag stops us overwriting a newer change)
        var now = DateTimeOffset.UtcNow;
        row["Status"] = target;
        row["LastUpdated"] = now;
        row[$"{target}At"] = now; // ReceivedAt, PreparingAt, ReadyAt or CollectedAt

        try
        {
            await table.UpdateEntityAsync(row, row.ETag, TableUpdateMode.Merge, ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            throw new GatewayException(HttpStatusCode.Conflict,
                "The order was changed by someone else while you were updating it. Please try again.");
        }

        _logger.LogInformation("Order {OrderId} moved to {Status} by staff request.", id, target);
        return row.ToResponse();
    }

    private static DateTimeOffset Placed(TableEntity row) =>
        row.GetDateTimeOffset("OrderTimestamp") ?? DateTimeOffset.MinValue;
}
