using Azure.Data.Tables;
using CoffeeNChill.Gateway.Dtos;
using CoffeeNChill.Gateway.Services;

namespace CoffeeNChill.Gateway.Mapping;

/// <summary>
/// All DTO mapping lives here (one place, no logic in controllers).
///   client DTO  --ToPayload()-->  Functions contract   (going in)
///   Functions contract / table row  --ToResponse()-->  client DTO   (coming out)
/// </summary>
public static class GatewayMapper
{
    // ---------- Menu ----------

    public static FnMenuItemCreate ToPayload(this CreateMenuItemRequest request) => new()
    {
        Category = request.Category.Trim(),
        Sku = request.Sku.Trim(),
        Name = request.Name.Trim(),
        Description = request.Description?.Trim() ?? string.Empty,
        Price = (double)request.Price, // the Functions/Tables store prices as double
        IsAvailable = request.IsAvailable
    };

    public static FnMenuItemUpdate ToPayload(this UpdateMenuItemRequest request) => new()
    {
        Price = request.Price is null ? null : (double)request.Price.Value,
        IsAvailable = request.IsAvailable
    };

    public static MenuItemResponse ToResponse(this FnMenuItem item) => new()
    {
        Category = item.Category,
        Sku = item.Sku,
        Name = item.Name,
        Description = item.Description,
        Price = Math.Round((decimal)item.Price, 2), // clients get a proper decimal
        IsAvailable = item.IsAvailable
    };

    // ---------- Orders ----------

    public static FnOrderRequest ToPayload(this PlaceOrderRequest request) => new()
    {
        OrderId = request.OrderId.Trim(),
        CustomerName = request.CustomerName.Trim(),
        SelectedItemSKUs = request.SelectedItemSKUs.Select(sku => sku.Trim()).ToList(),
        TotalPrice = request.TotalPrice
    };

    public static OrderQueuedResponse ToResponse(this FnOrderQueued queued) => new()
    {
        Message = queued.Message,
        OrderId = queued.OrderId,
        OrderTimestamp = queued.OrderTimestamp,
        Status = queued.Status
    };

    public static OrderStatusResponse ToResponse(this FnOrderStatus order) => new()
    {
        OrderId = order.OrderId,
        OrderDate = order.OrderDate,
        CustomerName = order.CustomerName,
        SelectedItemSKUs = order.SelectedItemSKUs,
        TotalPrice = Math.Round((decimal)order.TotalPrice, 2),
        OrderTimestamp = order.OrderTimestamp,
        Status = order.Status,
        IsComplete = order.Status == OrderStatuses.Collected,
        ReceivedAt = order.ReceivedAt,
        PreparingAt = order.PreparingAt,
        ReadyAt = order.ReadyAt,
        CollectedAt = order.CollectedAt,
        LastUpdated = order.LastUpdated
    };

    public static OrderListResponse ToResponse(this FnOrderList list) => new()
    {
        Count = list.Count,
        Orders = list.Orders.Select(o => o.ToResponse()).ToList()
    };

    // Same DTO, but built straight from an Azure Table row (used by PUT /api/orders/status,
    // which talks to the Orders table directly instead of going through a Function).
    public static OrderStatusResponse ToResponse(this TableEntity row)
    {
        var status = row.GetString("Status") ?? OrderStatuses.Received;

        return new OrderStatusResponse
        {
            OrderId = row.RowKey,
            OrderDate = row.PartitionKey,
            CustomerName = row.GetString("CustomerName") ?? string.Empty,
            // Tables cannot store a list, so the SKUs are saved as "COF-001,PAS-104"
            SelectedItemSKUs = (row.GetString("ItemSkus") ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            TotalPrice = Math.Round((decimal)(row.GetDouble("TotalPrice") ?? 0), 2),
            OrderTimestamp = row.GetDateTimeOffset("OrderTimestamp") ?? DateTimeOffset.MinValue,
            Status = status,
            IsComplete = status == OrderStatuses.Collected,
            ReceivedAt = row.GetDateTimeOffset("ReceivedAt"),
            PreparingAt = row.GetDateTimeOffset("PreparingAt"),
            ReadyAt = row.GetDateTimeOffset("ReadyAt"),
            CollectedAt = row.GetDateTimeOffset("CollectedAt"),
            LastUpdated = row.GetDateTimeOffset("LastUpdated") ?? DateTimeOffset.MinValue
        };
    }

    // ---------- Documents ----------

    public static StaffDocumentResponse ToResponse(this FnStaffDocument document) => new()
    {
        FileName = document.FileName,
        SizeBytes = document.SizeBytes,
        SizeKilobytes = Math.Round(document.SizeBytes / 1024.0, 1),
        LastModified = document.LastModified
    };

    public static DocumentUploadResponse ToResponse(this FnUploadResult result) => new()
    {
        FileName = result.FileName,
        SizeBytes = result.SizeBytes
    };
}
