using CoffeeNChill.Gateway.Models;

namespace CoffeeNChill.Gateway.Services;

public interface IUserService
{
    Task<UserEntity?> FindByUsernameAsync(string username, CancellationToken ct = default);

    Task<UserEntity> CreateAsync(string username, string email, string password, string role,
        CancellationToken ct = default);

    /// <summary>Returns the user if the password is correct, otherwise null.</summary>
    Task<UserEntity?> ValidateCredentialsAsync(string username, string password,
        CancellationToken ct = default);
}