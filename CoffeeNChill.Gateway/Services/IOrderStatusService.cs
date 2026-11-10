using CoffeeNChill.Gateway.Dtos;

namespace CoffeeNChill.Gateway.Services;

/// Staff action that talks to the Orders table directly (no Function for it)
public interface IOrderStatusService
{
    Task<OrderStatusResponse> AdvanceStatusAsync(string orderId, string newStatus, CancellationToken ct);
}
