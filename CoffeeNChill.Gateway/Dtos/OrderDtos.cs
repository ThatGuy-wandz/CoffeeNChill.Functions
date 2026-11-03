using System.ComponentModel.DataAnnotations;

namespace CoffeeNChill.Gateway.Dtos;

/// <summary>The four stages every order moves through, in order.</summary>
public static class OrderStatuses
{
    public const string Received = "Received";
    public const string Preparing = "Preparing";
    public const string Ready = "Ready";
    public const string Collected = "Collected";

    public static readonly string[] Lifecycle = { Received, Preparing, Ready, Collected };
}

/// <summary>Body for placing an order. The gateway queues it for the barista function.</summary>
public class PlaceOrderRequest
{
    /// <summary>Unique order number chosen by the client.</summary>
    /// <example>ORD-2026-8801</example>
    [Required, StringLength(40)]
    public string OrderId { get; set; } = string.Empty;

    /// <summary>Name of the customer.</summary>
    /// <example>Jane Smith</example>
    [Required, StringLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>SKUs of the menu items being ordered (at least one).</summary>
    /// <example>["COF-001","PAS-104"]</example>
    [Required, MinLength(1)]
    public List<string> SelectedItemSKUs { get; set; } = new();

    /// <summary>Order total in rand. Must be greater than zero.</summary>
    /// <example>65.00</example>
    [Range(0.01, 100000.0)]
    public decimal TotalPrice { get; set; }
}

/// <summary>Confirmation that an order was accepted onto the queue.</summary>
public class OrderQueuedResponse
{
    /// <example>Order accepted and queued for processing.</example>
    public string Message { get; set; } = string.Empty;

    /// <example>ORD-2026-8801</example>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>Time the gateway/function accepted the order (UTC).</summary>
    public DateTimeOffset OrderTimestamp { get; set; }

    /// <example>Queued</example>
    public string Status { get; set; } = string.Empty;
}

/// <summary>Where an order currently is in the Received, Preparing, Ready, Collected lifecycle.</summary>
public class OrderStatusResponse
{
    /// <example>ORD-2026-8801</example>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>The day the order was placed (yyyy-MM-dd).</summary>
    /// <example>2026-11-02</example>
    public string OrderDate { get; set; } = string.Empty;

    /// <example>Jane Smith</example>
    public string CustomerName { get; set; } = string.Empty;

    public List<string> SelectedItemSKUs { get; set; } = new();

    /// <example>65.00</example>
    public decimal TotalPrice { get; set; }

    public DateTimeOffset OrderTimestamp { get; set; }

    /// <summary>Received, Preparing, Ready or Collected.</summary>
    /// <example>Preparing</example>
    public string Status { get; set; } = string.Empty;

    /// <summary>True once the order has been collected.</summary>
    public bool IsComplete { get; set; }

    public DateTimeOffset? ReceivedAt { get; set; }
    public DateTimeOffset? PreparingAt { get; set; }
    public DateTimeOffset? ReadyAt { get; set; }
    public DateTimeOffset? CollectedAt { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
}

/// <summary>A list of orders and how many there are.</summary>
public class OrderListResponse
{
    public int Count { get; set; }
    public List<OrderStatusResponse> Orders { get; set; } = new();
}

/// <summary>Body for a staff member moving an order to its next status.</summary>
public class UpdateOrderStatusRequest
{
    /// <summary>The order to update.</summary>
    /// <example>ORD-2026-8801</example>
    [Required, StringLength(40)]
    public string OrderId { get; set; } = string.Empty;

    /// <summary>The new status. Must be later in the lifecycle than the current one.</summary>
    /// <example>Ready</example>
    [Required]
    public string NewStatus { get; set; } = string.Empty;
}
