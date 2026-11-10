using CoffeeNChill.Gateway.Dtos;
using CoffeeNChill.Gateway.Exceptions;
using CoffeeNChill.Gateway.Mapping;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.Gateway.Controllers;

/// Menu items stored in the MenuItems Azure Table (routed through the Functions app).
[ApiController]
[Route("api/menu")]
[Produces("application/json")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
public class MenuController : ControllerBase
{
    private readonly IFunctionsApiClient _functions;

    public MenuController(IFunctionsApiClient functions)
    {
        _functions = functions;
    }

    /// Get the whole menu.
    /// Public: students can view the menu without logging in.
    /// <response code="200">All menu items (an empty list if the menu is empty).
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(List<MenuItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MenuItemResponse>>> GetAll(CancellationToken ct)
    {
        var items = await _functions.GetMenuAsync(ct);
        return Ok(items.Select(item => item.ToResponse()).ToList());
    }

    /// Get the menu items in one category.
    /// <remarks>Public: students can view the menu without logging in.
    /// <param name="category">Category name, for example "Hot Drinks".
    /// <response code="200">The items in that category.
    /// <response code="404">No items exist in that category.
    [AllowAnonymous]
    [HttpGet("category/{category}")]
    [ProducesResponseType(typeof(List<MenuItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<MenuItemResponse>>> GetByCategory(string category, CancellationToken ct)
    {
        var items = await _functions.GetMenuByCategoryAsync(category, ct);
        return Ok(items.Select(item => item.ToResponse()).ToList());
    }

    /// Add a new item to the menu.
    /// Staff only (Admin or Staff role).
    /// <param name="request">The item to add.
    /// <response code="201">The item was created.
    /// <response code="400">The body is missing a required field or has an invalid value.
    /// <response code="401">No token, or the token is invalid or expired.
    /// <response code="403">The user's role is not allowed to do this.
    /// <response code="409">An item with that SKU already exists in the category.
    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
    [ProducesResponseType(typeof(MenuItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MenuItemResponse>> Create(
        [FromBody] CreateMenuItemRequest request, CancellationToken ct)
    {
        var created = (await _functions.CreateMenuItemAsync(request.ToPayload(), ct)).ToResponse();
        return CreatedAtAction(nameof(GetByCategory), new { category = created.Category }, created);
    }

    /// Change the price and/or availability of an item.
    /// Staff only (Admin or Staff role).
    /// <param name="category">Category of the item.
    /// <param name="id">SKU of the item.
    /// <param name="request">The new price and/or availability.
    /// <response code="200">The updated item.
    /// <response code="400">Neither field was supplied, or a value is invalid.
    /// <response code="401">No token, or the token is invalid or expired.
    /// <response code="403">The user's role is not allowed to do this.
    /// <response code="404">The item does not exist.
    [Authorize(Roles = "Admin,Staff")]
    [HttpPut("{category}/{id}")]
    [ProducesResponseType(typeof(MenuItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuItemResponse>> Update(
        string category, string id, [FromBody] UpdateMenuItemRequest request, CancellationToken ct)
    {
        if (request.Price is null && request.IsAvailable is null)
        {
            throw new GatewayException(System.Net.HttpStatusCode.BadRequest,
                "Provide at least one field to update: 'price' or 'isAvailable'.");
        }

        var updated = await _functions.UpdateMenuItemAsync(category, id, request.ToPayload(), ct);
        return Ok(updated.ToResponse());
    }

    /// Remove an item from the menu.
    /// Staff only (Admin or Staff role).
    /// <param name="category">Category of the item.
    /// <param name="id">SKU of the item.
    /// <response code="204">The item was deleted.
    /// <response code="401">No token, or the token is invalid or expired.
    /// <response code="403">The user's role is not allowed to do this.
    /// <response code="404">The item does not exist.
    [Authorize(Roles = "Admin,Staff")]
    [HttpDelete("{category}/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string category, string id, CancellationToken ct)
    {
        await _functions.DeleteMenuItemAsync(category, id, ct);
        return NoContent();
    }
}