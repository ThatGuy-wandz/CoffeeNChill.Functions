using System.Globalization;
using System.Net;
using CoffeeNChill.Gateway.Dtos;
using CoffeeNChill.Gateway.Exceptions;
using CoffeeNChill.Gateway.Mapping;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.Gateway.Controllers;

/// <summary>
/// Placing orders (Azure Storage Queue via the Functions app) and tracking them
/// (Orders Azure Table).
/// </summary>
[ApiController]
[Route("api/orders")]
[Produces("application/json")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
public class OrdersController : ControllerBase
{
    private readonly IFunctionsApiClient _functions;
    private readonly IOrderStatusService _orderStatus;

    public OrdersController(IFunctionsApiClient functions, IOrderStatusService orderStatus)
    {
        _functions = functions;
        _orderStatus = orderStatus;
    }

    /// <summary>Place an order.</summary>
    /// <remarks>
    /// The order is put on the order-processing-queue and the call returns straight away.
    /// A background function then creates the order record and moves it through
    /// Received, Preparing, Ready and Collected. Public: no login needed.
    /// </remarks>
    /// <param name="request">The order details.</param>
    /// <response code="202">The order was accepted and queued.</response>
    /// <response code="400">A required field is missing or invalid.</response>
    /// <response code="503">The queue is temporarily unavailable.</response>
    [AllowAnonymous]
    [HttpPost("queue")]
    [ProducesResponseType(typeof(OrderQueuedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request, CancellationToken ct)
    {
        var queued = await _functions.PlaceOrderAsync(request.ToPayload(), ct);
        return Accepted(queued.ToResponse());
    }

    /// <summary>Get the current status of one order.</summary>
    /// <remarks>Public: a student can track their own order with the order number.</remarks>
    /// <param name="orderId">The order number, for example ORD-2026-8801.</param>
    /// <response code="200">The order and its status history.</response>
    /// <response code="404">Not found. It may still be waiting in the queue.</response>
    [AllowAnonymous]
    [HttpGet("status/{orderId}")]
    [ProducesResponseType(typeof(OrderStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderStatusResponse>> GetStatus(string orderId, CancellationToken ct)
    {
        var order = await _functions.GetOrderStatusAsync(orderId, ct);
        return Ok(order.ToResponse());
    }

    /// <summary>List orders, optionally filtered by date and/or status.</summary>
    /// <remarks>Staff only (Admin or Staff role), because it shows every customer's orders.</remarks>
    /// <param name="date">Only orders placed on this day, format yyyy-MM-dd (optional).</param>
    /// <param name="status">Only orders with this status: Received, Preparing, Ready or Collected (optional).</param>
    /// <response code="200">The matching orders, newest first.</response>
    /// <response code="400">The date or status filter is not valid.</response>
    /// <response code="401">No token, or the token is invalid or expired.</response>
    /// <response code="403">The user's role is not allowed to do this.</response>
    [Authorize(Roles = "Admin,Staff")]
    [HttpGet("status")]
    [ProducesResponseType(typeof(OrderListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrderListResponse>> ListOrders(
        [FromQuery] string? date, [FromQuery] string? status, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(date) &&
            !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _))
        {
            throw new GatewayException(HttpStatusCode.BadRequest, "date must use the format yyyy-MM-dd.");
        }

        var list = await _functions.GetOrdersAsync(date, status, ct);
        return Ok(list.ToResponse());
    }

    /// <summary>Move an order to its next status (staff).</summary>
    /// <remarks>
    /// Staff only (Admin or Staff role). Updates the Orders table
    /// directly. The status can only move forward.
    /// </remarks>
    /// <param name="request">The order id and the new status.</param>
    /// <response code="200">The updated order.</response>
    /// <response code="400">The status is not one of the four lifecycle stages.</response>
    /// <response code="401">No token, or the token is invalid or expired.</response>
    /// <response code="403">The user's role is not allowed to do this.</response>
    /// <response code="404">The order does not exist.</response>
    /// <response code="409">The order is already at or past that status.</response>
    [Authorize(Roles = "Admin,Staff")]
    [HttpPut("status")]
    [ProducesResponseType(typeof(OrderStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderStatusResponse>> UpdateStatus(
        [FromBody] UpdateOrderStatusRequest request, CancellationToken ct)
    {
        var updated = await _orderStatus.AdvanceStatusAsync(request.OrderId, request.NewStatus, ct);
        return Ok(updated);
    }
}