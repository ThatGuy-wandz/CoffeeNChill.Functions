using System.Net;
using CoffeeNChill.Gateway.Dtos;
using CoffeeNChill.Gateway.Exceptions;
using CoffeeNChill.Gateway.Models;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.Gateway.Controllers;

/// <summary>Registration, login (JWT) and the logged-in user's profile, kept in the Users Azure Table.</summary>
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

    /// <summary>Create a new account.</summary>
    /// <remarks>
    /// Public: no login needed. The password is stored only as a BCrypt hash.
    /// Every new account gets the Public role; roles cannot be chosen at registration.
    /// </remarks>
    /// <param name="request">Username, email and password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">The account was created.</response>
    /// <response code="400">A field is missing or invalid.</response>
    /// <response code="409">That username is already taken.</response>
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

    /// <summary>Log in and receive a JWT.</summary>
    /// <remarks>
    /// Public: no login needed. Send the returned token on later requests as
    /// <c>Authorization: Bearer {token}</c>. The same error is returned for an unknown
    /// username and a wrong password, so attackers cannot tell which usernames exist.
    /// </remarks>
    /// <param name="request">Username and password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">A signed JWT with an expiry time and the user's role.</response>
    /// <response code="400">A field is missing.</response>
    /// <response code="401">The username or password is wrong.</response>
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

    /// <summary>Show who the token belongs to.</summary>
    /// <remarks>Any logged-in user (Public, Staff or Admin). Useful for checking a token works.</remarks>
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