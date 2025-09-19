// AuthService/Controllers/AuthController.cs
using AuthService.Models.Dtos;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuthService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;

    public AuthController(IAuthService authService, IConfiguration configuration)
    {
        _authService = authService;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterModel model)
    {
        var result = await _authService.RegisterAsync(model);
        if (!result.Success)
            return BadRequest(new { message = result.Message });
        return Ok(new { message = "User registered successfully", data = result.Data });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        var result = await _authService.LoginAsync(model);
        if (!result.Success)
            return Unauthorized(new { message = result.Message });

        // Cookie options for cross-service sharing
        var cookieOptions = new CookieOptions
        {
            HttpOnly = false, // Allow JavaScript access for SignalR
            Secure = Request.IsHttps, // Only HTTPS in production
            SameSite = SameSiteMode.None, // Allow cross-origin
            Expires = result.Data.ExpiresIn,
            Domain = "localhost", // Share across all localhost ports
            Path = "/"
        };

        // For development - less restrictive options
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            cookieOptions.SameSite = SameSiteMode.Lax;
            cookieOptions.HttpOnly = false;
        }

        // Set cookies
        Response.Cookies.Append(
            _configuration["Jwt:AccessTokenCookieName"] ?? "auth_access",
            result.Data.AccessToken,
            cookieOptions);

        Response.Cookies.Append(
            _configuration["Jwt:RefreshTokenCookieName"] ?? "auth_refresh",
            result.Data.RefreshToken,
            cookieOptions);

        // Also return token in response for direct use
        return Ok(new
        {
            message = "Login successful",
            accessToken = result.Data.AccessToken,
            refreshToken = result.Data.RefreshToken,
            expiresIn = result.Data.ExpiresIn
        });
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken()
    {
        var accessToken = Request.Cookies[_configuration["Jwt:AccessTokenCookieName"]];
        var refreshToken = Request.Cookies[_configuration["Jwt:RefreshTokenCookieName"]];

        // Also check Authorization header as fallback
        if (string.IsNullOrEmpty(accessToken))
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                accessToken = authHeader.Substring(7);
            }
        }

        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new { message = "Missing tokens" });
        }

        var tokenRequest = new TokenRequestDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };

        var result = await _authService.RefreshTokenAsync(tokenRequest);
        if (!result.Success)
            return Unauthorized(new { message = result.Message });

        var cookieOptions = new CookieOptions
        {
            HttpOnly = false,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.None,
            Expires = result.Data.ExpiresIn,
            Domain = "localhost",
            Path = "/"
        };

        Response.Cookies.Append(
            _configuration["Jwt:AccessTokenCookieName"] ?? "auth_access",
            result.Data.AccessToken,
            cookieOptions);

        Response.Cookies.Append(
            _configuration["Jwt:RefreshTokenCookieName"] ?? "auth_refresh",
            result.Data.RefreshToken,
            cookieOptions);

        return Ok(new
        {
            message = "Token refreshed successfully",
            accessToken = result.Data.AccessToken,
            refreshToken = result.Data.RefreshToken,
            expiresIn = result.Data.ExpiresIn
        });
    }

    [HttpPost("islogin")]
    [Authorize]
    public async Task<IActionResult> IsLogin()
    {
        var username = User.Identity?.Name;
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        return Ok(new
        {
            message = "User is authenticated",
            isAuthenticated = true,
            username,
            userId,
            role
        });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // Clear cookies
        var cookieOptions = new CookieOptions
        {
            Expires = DateTime.UtcNow.AddDays(-1),
            Domain = "localhost",
            Path = "/"
        };

        Response.Cookies.Append(
            _configuration["Jwt:AccessTokenCookieName"] ?? "auth_access",
            "",
            cookieOptions);

        Response.Cookies.Append(
            _configuration["Jwt:RefreshTokenCookieName"] ?? "auth_refresh",
            "",
            cookieOptions);

        return Ok(new { message = "Logged out successfully" });
    }

    // Test endpoint for development
    [HttpGet("test")]
    public IActionResult Test()
    {
        return Ok(new
        {
            message = "AuthService is running",
            time = DateTime.UtcNow,
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        });
    }
}