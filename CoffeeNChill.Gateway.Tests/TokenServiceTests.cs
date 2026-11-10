using CoffeeNChill.Gateway.Models;
using CoffeeNChill.Gateway.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace CoffeeNChill.Gateway.Tests;

public class TokenServiceTests
{
    private const string Key = "unit-test-key-that-is-at-least-32-characters-long";

    private static TokenService Create(string key = Key) => new(Options.Create(new JwtSettings
    {
        Key = key, Issuer = "TestIssuer", Audience = "TestAudience", ExpiryMinutes = 30
    }));

    private static UserEntity Staff() => new()
    {
        RowKey = "barista", Username = "barista", Email = "barista@coffeenchill.ac.za", Role = Roles.Staff
    };

    [Fact]
    public void CreateToken_ContainsNameEmailAndRoleClaims()
    {
        var result = Create().CreateToken(Staff());
        var jwt = new JsonWebToken(result.AccessToken);

        Assert.Equal("barista", jwt.Claims.First(c => c.Type == "sub").Value);
        Assert.Equal("barista@coffeenchill.ac.za", jwt.Claims.First(c => c.Type == "email").Value);
        Assert.Equal(Roles.Staff, jwt.Claims.First(c => c.Type == "role").Value);
    }

    [Fact]
    public void CreateToken_ExpiresAfterConfiguredMinutes()
    {
        var result = Create().CreateToken(Staff());
        var minutes = (result.ExpiresAtUtc - DateTime.UtcNow).TotalMinutes;
        Assert.InRange(minutes, 29, 31);
    }

    [Fact]
    public async Task CreateToken_IsValidWhenCheckedWithSameKey()
    {
        var result = Create().CreateToken(Staff());
        var check = await new JsonWebTokenHandler().ValidateTokenAsync(result.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = "TestIssuer",
            ValidAudience = "TestAudience",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key))
        });
        Assert.True(check.IsValid);
    }

    [Fact]
    public async Task CreateToken_IsRejectedWithADifferentKey()
    {
        var result = Create().CreateToken(Staff());
        var check = await new JsonWebTokenHandler().ValidateTokenAsync(result.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = "TestIssuer",
            ValidAudience = "TestAudience",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a-completely-different-key-of-32-chars!!"))
        });
        Assert.False(check.IsValid);
    }

    [Fact]
    public void CreateToken_ThrowsWhenKeyIsTooShort()
    {
        Assert.Throws<InvalidOperationException>(() => Create("short").CreateToken(Staff()));
    }
}