namespace CoffeeNChill.Functions.Models
{
    // Payload for POST /api/menu
    public class MenuItemCreateDto
    {
        public string Category { get; set; } = default!;
        public string Sku { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;
        public double Price { get; set; }
        public bool IsAvailable { get; set; } = true;
    }
    //Payload for PUT /api/menu/{category}/{id}. Both fields are optional
    // So a caller can update just the price, just availability, or both.
    public class MenuItemUpdateDto
    {
        public double? Price { get; set; }
        public bool? IsAvailable { get; set; }
    }
    // Shape returned to API clients — never expose the raw table entity.
    public class MenuItemResponseDto
    {
        public string Category { get; set; } = default!;
        public string Sku { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;
        public double Price { get; set; }
        public bool IsAvailable { get; set; }
        public static MenuItemResponseDto FromEntity(MenuItem entity) => new()
        {
            Category = entity.PartitionKey,
            Sku = entity.RowKey,
            Name = entity.Name,
            Description = entity.Description,
            Price = entity.Price,
            IsAvailable = entity.IsAvailable
        };
    }
}