using Azure;
using Azure.Data.Tables;

namespace CoffeeNChill.Gateway.Models;

/// The three roles used for role-based access control.
public static class Roles
{
    public const string Public = "Public";
    public const string Staff = "Staff";
    public const string Admin = "Admin";
}

/// A user profile stored in the Users Azure Table.

/// PartitionKey is always "Users". RowKey is the lower-case username, which guarantees
/// usernames are unique. Only the BCrypt hash of the password is stored, never the password.

public class UserEntity : ITableEntity
{
    public const string Partition = "Users";

    public string PartitionKey { get; set; } = Partition;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Public;
    public DateTime CreatedAtUtc { get; set; }
}