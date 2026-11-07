using CoffeeNChill.Gateway.Models;

namespace CoffeeNChill.Gateway.Services;

public record TokenResult(string AccessToken, DateTime ExpiresAtUtc);

public interface ITokenService
{
    TokenResult CreateToken(UserEntity user);
}