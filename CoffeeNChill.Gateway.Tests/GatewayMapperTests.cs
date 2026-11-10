using CoffeeNChill.Gateway.Dtos;
using CoffeeNChill.Gateway.Mapping;
using CoffeeNChill.Gateway.Services;

namespace CoffeeNChill.Gateway.Tests;

public class GatewayMapperTests
{
    [Fact]
    public void CreateMenuItemRequest_IsTrimmedAndPriceBecomesDouble()
    {
        var payload = new CreateMenuItemRequest
        {
            Category = " Drinks ", Sku = " LATTE-01 ", Name = " Latte ", Description = null, Price = 32.50m, IsAvailable = true
        }.ToPayload();

        Assert.Equal("Drinks", payload.Category);
        Assert.Equal("LATTE-01", payload.Sku);
        Assert.Equal("Latte", payload.Name);
        Assert.Equal(string.Empty, payload.Description);
        Assert.Equal(32.5, payload.Price);
    }

    [Fact]
    public void UpdateMenuItemRequest_NullPriceStaysNull()
    {
        var payload = new UpdateMenuItemRequest { Price = null, IsAvailable = false }.ToPayload();
        Assert.Null(payload.Price);
        Assert.False(payload.IsAvailable);
    }

    [Fact]
    public void FnMenuItem_PriceIsRoundedToTwoDecimals()
    {
        var response = new FnMenuItem { Sku = "A", Price = 3.456 }.ToResponse();
        Assert.Equal(3.46m, response.Price);
    }

    [Fact]
    public void FnOrderStatus_IsCompleteOnlyWhenCollected()
    {
        Assert.True(new FnOrderStatus { Status = OrderStatuses.Collected }.ToResponse().IsComplete);
        Assert.False(new FnOrderStatus { Status = OrderStatuses.Preparing }.ToResponse().IsComplete);
    }

    [Fact]
    public void FnStaffDocument_SizeIsConvertedToKilobytes()
    {
        var response = new FnStaffDocument { FileName = "menu.pdf", SizeBytes = 2048 }.ToResponse();
        Assert.Equal(2.0, response.SizeKilobytes);
    }
}