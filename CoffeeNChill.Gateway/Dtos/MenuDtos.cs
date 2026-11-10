using System.ComponentModel.DataAnnotations;

namespace CoffeeNChill.Gateway.Dtos;

/// Body for adding a new item to the menu.
public class CreateMenuItemRequest
{
    /// Menu category (this becomes the table PartitionKey).
    /// <example>Hot Drinks
    [Required, StringLength(50)]
    public string Category { get; set; } = string.Empty;

    /// Unique item code inside the category (this becomes the table RowKey).
    /// <example>COF-001
    [Required, StringLength(20)]
    public string Sku { get; set; } = string.Empty;

    /// Item name shown to students.
    /// <example>Espresso
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// Short description (optional).
    /// <example>Double shot of rich espresso</example>
    [StringLength(250)]
    public string? Description { get; set; }

    /// Price in rand. Must be between 0 and 10000.
    /// <example>28.50
    [Range(0.0, 10000.0)]
    public decimal Price { get; set; }

    /// Whether the item can currently be ordered.
    /// <example>true
    public bool IsAvailable { get; set; } = true;
}

/// Body for changing an existing item. Send one field or both.
public class UpdateMenuItemRequest
{
    /// New price (optional).
    /// <example>32.00
    [Range(0.0, 10000.0)]
    public decimal? Price { get; set; }

    /// New availability (optional).
    /// <example>false
    public bool? IsAvailable { get; set; }
}

/// A menu item as returned to clients.
public class MenuItemResponse
{
    /// <example>Hot Drinks
    public string Category { get; set; } = string.Empty;

    /// <example>COF-001
    public string Sku { get; set; } = string.Empty;

    /// <example>Espresso
    public string Name { get; set; } = string.Empty;

    /// <example>Double shot of rich espresso
    public string Description { get; set; } = string.Empty;

    /// <summary>Price in rand, rounded to 2 decimals
    /// <example>28.50
    public decimal Price { get; set; }

    /// <example>true
    public bool IsAvailable { get; set; }
}
