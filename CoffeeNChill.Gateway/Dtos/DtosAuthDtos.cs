using System.ComponentModel.DataAnnotations;

namespace CoffeeNChill.Gateway.Dtos;

//New accounts always get the Public role.
public class RegisterRequest
{
    
    [Required(ErrorMessage = "Username is required.")]
    [RegularExpression(@"^[A-Za-z0-9._-]{3,30}$",
        ErrorMessage = "Username must be 3-30 characters: letters, numbers, dot, dash or underscore.")]
    public string Username { get; set; } = string.Empty;

    
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email is not a valid email address.")]
    [StringLength(100, ErrorMessage = "Email cannot be longer than 100 characters.")]
    public string Email { get; set; } = string.Empty;

    
    [Required(ErrorMessage = "Password is required.")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{8,72}$",
        ErrorMessage = "Password must be 8-72 characters and contain at least one letter and one number.")]
    public string Password { get; set; } = string.Empty;
}

/// Credentials used to log in.
public class LoginRequest
{
    
    [Required(ErrorMessage = "Username is required.")]
    public string Username { get; set; } = string.Empty;

    
    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}

/// The account that was created. The password hash is never returned.
public class RegisterResponse
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

/// The signed JWT and when it expires.
public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public DateTime ExpiresAtUtc { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

/// The logged-in user's profile, read from the JWT claims.
public class UserProfileResponse
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}