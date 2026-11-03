using System.ComponentModel.DataAnnotations;

namespace CoffeeNChill.Gateway.Dtos;

/// <summary>Body for adding a new item to the menu.</summary>
public class CreateMenuItemRequest
{
    /// <summary>Menu category (this becomes the table PartitionKey).</summary>
    /// <example>Hot Drinks</example>
    [Required, StringLength(50)]
    public string Category { get; set; } = string.Empty;

    /// <summary>Unique item code inside the category (this becomes the table RowKey).</summary>
    /// <example>COF-001</example>
    [Required, StringLength(20)]
    public string Sku { get; set; } = string.Empty;

    /// <summary>Item name shown to students.</summary>
    /// <example>Espresso</example>
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Short description (optional).</summary>
    /// <example>Double shot of rich espresso</example>
    [StringLength(250)]
    public string? Description { get; set; }

    /// <summary>Price in rand. Must be between 0 and 10000.</summary>
    /// <example>28.50</example>
    [Range(0.0, 10000.0)]
    public decimal Price { get; set; }

    /// <summary>Whether the item can currently be ordered.</summary>
    /// <example>true</example>
    public bool IsAvailable { get; set; } = true;
}

/// <summary>Body for changing an existing item. Send one field or both.</summary>
public class UpdateMenuItemRequest
{
    /// <summary>New price (optional).</summary>
    /// <example>32.00</example>
    [Range(0.0, 10000.0)]
    public decimal? Price { get; set; }

    /// <summary>New availability (optional).</summary>
    /// <example>false</example>
    public bool? IsAvailable { get; set; }
}

/// <summary>A menu item as returned to clients.</summary>
public class MenuItemResponse
{
    /// <example>Hot Drinks</example>
    public string Category { get; set; } = string.Empty;

    /// <example>COF-001</example>
    public string Sku { get; set; } = string.Empty;

    /// <example>Espresso</example>
    public string Name { get; set; } = string.Empty;

    /// <example>Double shot of rich espresso</example>
    public string Description { get; set; } = string.Empty;

    /// <summary>Price in rand, rounded to 2 decimals.</summary>
    /// <example>28.50</example>
    public decimal Price { get; set; }

    /// <example>true</example>
    public bool IsAvailable { get; set; }
}
