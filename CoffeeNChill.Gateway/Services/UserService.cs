using System.Net;
using Azure;
using Azure.Data.Tables;
using CoffeeNChill.Gateway.Exceptions;
using CoffeeNChill.Gateway.Models;

namespace CoffeeNChill.Gateway.Services;

public class UserService : IUserService
{
    private static bool _tableReady;
    private readonly TableClient _table;

    public UserService(TableServiceClient tableServiceClient)
    {
        _table = tableServiceClient.GetTableClient("Users");
    }

    public async Task<UserEntity?> FindByUsernameAsync(string username, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);
        try
        {
            var response = await _table.GetEntityAsync<UserEntity>(
                UserEntity.Partition, Normalize(username), cancellationToken: ct);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<UserEntity> CreateAsync(string username, string email, string password,
        string role, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);

        var entity = new UserEntity
        {
            RowKey = Normalize(username),
            Username = username.Trim(),
            Email = email.Trim(),
            // BCrypt adds its own random salt, so two identical passwords give different hashes.
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role,
            CreatedAtUtc = DateTime.UtcNow
        };

        try
        {
            await _table.AddEntityAsync(entity, ct);
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
        {
            throw new GatewayException(HttpStatusCode.Conflict,
                $"The username '{entity.Username}' is already taken.");
        }

        return entity;
    }

    public async Task<UserEntity?> ValidateCredentialsAsync(string username, string password,
        CancellationToken ct = default)
    {
        var user = await FindByUsernameAsync(username, ct);
        if (user is null) return null;

        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash) ? user : null;
    }

    private async Task EnsureTableAsync(CancellationToken ct)
    {
        if (_tableReady) return;
        await _table.CreateIfNotExistsAsync(ct);
        _tableReady = true;
    }

    private static string Normalize(string username) => username.Trim().ToLowerInvariant();
}