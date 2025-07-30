using AuthService.Data;
using AuthService.Models.Domain;
using AuthService.Models.Dtos;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace AuthService.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuthDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;

        public AuthService(UserManager<ApplicationUser> userManager, AuthDbContext context, IConfiguration configuration, IMapper mapper)
        {
            _userManager = userManager;
            _context = context;
            _configuration = configuration;
            _mapper = mapper;
        }

        public async Task<ServiceResponse<UserDto>> RegisterAsync(RegisterModel model)
        {
            var user = _mapper.Map<ApplicationUser>(model);
            user.MobileActiveCode = GenerateMobileActiveCode();
            user.IsActivateMobile = false;
            user.IsActivateEmail = false;
            user.Role = Role.Player; // Default role

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                return new ServiceResponse<UserDto>
                {
                    Success = false,
                    Message = string.Join("; ", result.Errors.Select(e => e.Description))
                };
            }

            return new ServiceResponse<UserDto>
            {
                Success = true,
                Data = _mapper.Map<UserDto>(user)
            };
        }

        public async Task<ServiceResponse<TokenResponseDto>> LoginAsync(LoginModel model)
        {
            var user = await _userManager.FindByNameAsync(model.UsernameOrEmail) ??
                       await _userManager.FindByEmailAsync(model.UsernameOrEmail);

            if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
            {
                return new ServiceResponse<TokenResponseDto>
                {
                    Success = false,
                    Message = "Invalid credentials"
                };
            }

            if (!user.IsActivateEmail || !user.IsActivateMobile)
            {
                return new ServiceResponse<TokenResponseDto>
                {
                    Success = false,
                    Message = "Email or Mobile not activated"
                };
            }

            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();
            user.RefreshToken = refreshToken.Token;
            user.RefreshTokenExpiryTime = refreshToken.Expires;
            await _userManager.UpdateAsync(user);

            return new ServiceResponse<TokenResponseDto>
            {
                Success = true,
                Data = new TokenResponseDto
                {
                    AccessToken = token,
                    RefreshToken = refreshToken.Token,
                    ExpiresIn = DateTime.UtcNow.AddMinutes(int.Parse(_configuration["Jwt:AccessTokenExpiryMinutes"]))
                }
            };
        }

        public async Task<ServiceResponse<TokenResponseDto>> RefreshTokenAsync(TokenRequestDto tokenRequest)
        {
            var principal = GetPrincipalFromExpiredToken(tokenRequest.AccessToken);
            if (principal == null)
            {
                return new ServiceResponse<TokenResponseDto>
                {
                    Success = false,
                    Message = "Invalid token"
                };
            }

            var username = principal.Identity?.Name;
            var user = await _userManager.FindByNameAsync(username);
            if (user == null || user.RefreshToken != tokenRequest.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                return new ServiceResponse<TokenResponseDto>
                {
                    Success = false,
                    Message = "Invalid refresh token"
                };
            }

            var newAccessToken = GenerateJwtToken(user);
            var newRefreshToken = GenerateRefreshToken();
            user.RefreshToken = newRefreshToken.Token;
            user.RefreshTokenExpiryTime = newRefreshToken.Expires;
            await _userManager.UpdateAsync(user);

            return new ServiceResponse<TokenResponseDto>
            {
                Success = true,
                Data = new TokenResponseDto
                {
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshToken.Token,
                    ExpiresIn = DateTime.UtcNow.AddMinutes(int.Parse(_configuration["Jwt:AccessTokenExpiryMinutes"]))
                }
            };
        }

        private string GenerateJwtToken(ApplicationUser user)
        {
            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(int.Parse(_configuration["Jwt:AccessTokenExpiryMinutes"])),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private RefreshToken GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return new RefreshToken
            {
                Token = Convert.ToBase64String(randomNumber),
                Expires = DateTime.UtcNow.AddDays(int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"])),
                Created = DateTime.UtcNow
            };
        }

        private ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"])),
                ValidateLifetime = false, // Allow expired tokens for refresh
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidAudience = _configuration["Jwt:Audience"]
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
                if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    return null;
                }
                return principal;
            }
            catch
            {
                return null;
            }
        }

        private string GenerateMobileActiveCode()
        {
            return new Random().Next(100000, 999999).ToString();
        }
    }


}

