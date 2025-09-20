using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AmalaSpotLocator.Interfaces;
using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;
using AmalaSpotLocator.Core.Applications.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace AmalaSpotLocator.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
[Tags("Authentication")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly IUserService _userService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthenticationService authService,
        IUserService userService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _userService = userService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.RegisterAsync(request);
            
            if (!result.Success)
                return BadRequest(new { message = result.ErrorMessage });

            return Ok(new
            {
                token = result.Token,
                refreshToken = result.RefreshToken,
                expiresAt = result.ExpiresAt,
                user = new
                {
                    id = result.User?.Id,
                    firstName = result.User?.FirstName,
                    lastName = result.User?.LastName,
                    email = result.User?.Email,
                    role = result.User?.Role.ToString()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration");
            return StatusCode(500, new { message = "Registration failed" });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.LoginAsync(request);
            
            if (!result.Success)
                return Unauthorized(new { message = result.ErrorMessage });

            return Ok(new
            {
                token = result.Token,
                refreshToken = result.RefreshToken,
                expiresAt = result.ExpiresAt,
                user = new
                {
                    id = result.User?.Id,
                    firstName = result.User?.FirstName,
                    lastName = result.User?.LastName,
                    email = result.User?.Email,
                    role = result.User?.Role.ToString()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user login");
            return StatusCode(500, new { message = "Login failed" });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return BadRequest(new { message = "Refresh token is required" });

            var result = await _authService.RefreshTokenAsync(request.RefreshToken);
            
            if (!result.Success)
                return Unauthorized(new { message = result.ErrorMessage });

            return Ok(new
            {
                token = result.Token,
                refreshToken = result.RefreshToken,
                expiresAt = result.ExpiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(500, new { message = "Token refresh failed" });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                await _authService.RevokeRefreshTokenAsync(request.RefreshToken);
            }

            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, new { message = "Logout failed" });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
                return NotFound();

            return Ok(new
            {
                id = user.Id,
                firstName = user.FirstName,
                lastName = user.LastName,
                email = user.Email,
                phoneNumber = user.PhoneNumber,
                role = user.Role.ToString(),
                preferredLanguage = user.PreferredLanguage,
                preferredMaxBudget = user.PreferredMaxBudget,
                preferredMinRating = user.PreferredMinRating,
                createdAt = user.CreatedAt,
                lastLoginAt = user.LastLoginAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user");
            return StatusCode(500, new { message = "Failed to retrieve user information" });
        }
    }

    [HttpPut("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var success = await _userService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
            
            if (!success)
                return BadRequest(new { message = "Current password is incorrect" });

            return Ok(new { message = "Password changed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");
            return StatusCode(500, new { message = "Password change failed" });
        }
    }
}

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    [Required]
    [MinLength(6)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;
}