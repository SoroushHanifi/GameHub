using AuthService.Models.Dtos;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        // Set JWT and Refresh Token in cookies
        Response.Cookies.Append(_configuration["Jwt:AccessTokenCookieName"], result.Data.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = result.Data.ExpiresIn
        });

        Response.Cookies.Append(_configuration["Jwt:RefreshTokenCookieName"], result.Data.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = result.Data.ExpiresIn.AddDays(int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"]))
        });

        return Ok(new { message = "Login successful" });
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken()
    {
        var accessToken = Request.Cookies[_configuration["Jwt:AccessTokenCookieName"]];
        var refreshToken = Request.Cookies[_configuration["Jwt:RefreshTokenCookieName"]];

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

        // Set new JWT and Refresh Token in cookies
        Response.Cookies.Append(_configuration["Jwt:AccessTokenCookieName"], result.Data.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = result.Data.ExpiresIn
        });

        Response.Cookies.Append(_configuration["Jwt:RefreshTokenCookieName"], result.Data.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = result.Data.ExpiresIn.AddDays(int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"]))
        });

        return Ok(new { message = "Token refreshed successfully" });
    }

    [HttpPost("Islogin")]
    [Authorize]
    public async Task<IActionResult> IsLogin()
    {
        return Ok(new { message = "Login successful" });
    }

}


