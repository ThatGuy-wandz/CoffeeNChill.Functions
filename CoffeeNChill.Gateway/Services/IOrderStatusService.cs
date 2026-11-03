using CoffeeNChill.Gateway.Dtos;

namespace CoffeeNChill.Gateway.Services;

/// <summary>Staff action that talks to the Orders table directly (no Function for it).</summary>
public interface IOrderStatusService
{
    Task<OrderStatusResponse> AdvanceStatusAsync(string orderId, string newStatus, CancellationToken ct);
}
