using CoffeeNChill.Gateway.Dtos;
using CoffeeNChill.Gateway.Exceptions;
using CoffeeNChill.Gateway.Mapping;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.Gateway.Controllers;

/// <summary>Menu items stored in the MenuItems Azure Table (routed through the Functions app).</summary>
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

    /// <summary>Get the whole menu.</summary>
    /// <remarks>Public: students can view the menu without logging in.</remarks>
    /// <response code="200">All menu items (an empty list if the menu is empty).</response>
    // JWT SECTION: this endpoint stays public, so add [AllowAnonymous] here.
    [HttpGet]
    [ProducesResponseType(typeof(List<MenuItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MenuItemResponse>>> GetAll(CancellationToken ct)
    {
        var items = await _functions.GetMenuAsync(ct);
        return Ok(items.Select(item => item.ToResponse()).ToList());
    }

    /// <summary>Get the menu items in one category.</summary>
    /// <param name="category">Category name, for example "Hot Drinks".</param>
    /// <response code="200">The items in that category.</response>
    /// <response code="404">No items exist in that category.</response>
    [HttpGet("category/{category}")]
    [ProducesResponseType(typeof(List<MenuItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<MenuItemResponse>>> GetByCategory(string category, CancellationToken ct)
    {
        var items = await _functions.GetMenuByCategoryAsync(category, ct);
        return Ok(items.Select(item => item.ToResponse()).ToList());
    }

    /// <summary>Add a new item to the menu.</summary>
    /// <remarks>Staff only once JWT is switched on (Admin or Staff role).</remarks>
    /// <param name="request">The item to add.</param>
    /// <response code="201">The item was created.</response>
    /// <response code="400">The body is missing a required field or has an invalid value.</response>
    /// <response code="409">An item with that SKU already exists in the category.</response>
    // JWT SECTION: add [Authorize(Roles = "Admin,Staff")] here.
    [HttpPost]
    [ProducesResponseType(typeof(MenuItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MenuItemResponse>> Create(
        [FromBody] CreateMenuItemRequest request, CancellationToken ct)
    {
        var created = (await _functions.CreateMenuItemAsync(request.ToPayload(), ct)).ToResponse();
        return CreatedAtAction(nameof(GetByCategory), new { category = created.Category }, created);
    }

    /// <summary>Change the price and/or availability of an item.</summary>
    /// <remarks>Staff only once JWT is switched on (Admin or Staff role).</remarks>
    /// <param name="category">Category of the item.</param>
    /// <param name="id">SKU of the item.</param>
    /// <param name="request">The new price and/or availability.</param>
    /// <response code="200">The updated item.</response>
    /// <response code="400">Neither field was supplied, or a value is invalid.</response>
    /// <response code="404">The item does not exist.</response>
    // JWT SECTION: add [Authorize(Roles = "Admin,Staff")] here.
    [HttpPut("{category}/{id}")]
    [ProducesResponseType(typeof(MenuItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
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

    /// <summary>Remove an item from the menu.</summary>
    /// <remarks>Staff only once JWT is switched on (Admin or Staff role).</remarks>
    /// <param name="category">Category of the item.</param>
    /// <param name="id">SKU of the item.</param>
    /// <response code="204">The item was deleted.</response>
    /// <response code="404">The item does not exist.</response>
    // JWT SECTION: add [Authorize(Roles = "Admin,Staff")] here.
    [HttpDelete("{category}/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string category, string id, CancellationToken ct)
    {
        await _functions.DeleteMenuItemAsync(category, id, ct);
        return NoContent();
    }
}
