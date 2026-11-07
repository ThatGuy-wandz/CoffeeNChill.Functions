using System.Net;
using CoffeeNChill.Gateway.Dtos;
using CoffeeNChill.Gateway.Exceptions;
using CoffeeNChill.Gateway.Models;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.Gateway.Controllers;

///  login (JWT) and the logged-in user's profile, kept in the Users Azure Table.
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
public class AuthController : ControllerBase
{
    private readonly IUserService _users;
    private readonly ITokenService _tokens;

    public AuthController(IUserService users, ITokenService tokens)
    {
        _users = users;
        _tokens = tokens;
    }

    
    /// Public: no login needed. The password is stored only as a BCrypt hash.
    /// Every new account gets the Public role; roles cannot be chosen at registration.
    
    
    /// The account was created.
   
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterResponse>> Register(
        [FromBody] RegisterRequest request, CancellationToken ct)
    {
        var user = await _users.CreateAsync(
            request.Username, request.Email, request.Password, Roles.Public, ct);

        var response = new RegisterResponse
        {
            Username = user.Username,
            Email = user.Email,
            Role = user.Role,
            CreatedAtUtc = user.CreatedAtUtc
        };

        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// Log in and receive a JWT.
    
    /// Public: no login needed. Send the returned token on later requests as
    /// <c>Authorization: Bearer {token}</c>. The same error is returned for an unknown
    /// username and a wrong password, so attackers cannot tell which usernames exist.
    
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await _users.ValidateCredentialsAsync(request.Username, request.Password, ct);
        if (user is null)
        {
            throw new GatewayException(HttpStatusCode.Unauthorized, "Invalid username or password.");
        }

        var token = _tokens.CreateToken(user);

        return Ok(new AuthResponse
        {
            AccessToken = token.AccessToken,
            ExpiresAtUtc = token.ExpiresAtUtc,
            Username = user.Username,
            Role = user.Role
        });
    }

    
    /// <response code="200">The username, email and role from the token.</response>
    /// <response code="401">No token, or the token is invalid or expired.</response>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public ActionResult<UserProfileResponse> Me()
    {
        return Ok(new UserProfileResponse
        {
            Username = User.FindFirst("sub")?.Value ?? string.Empty,
            Email = User.FindFirst("email")?.Value ?? string.Empty,
            Role = User.FindFirst("role")?.Value ?? string.Empty
        });
    }
}