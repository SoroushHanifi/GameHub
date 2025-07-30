using AuthService.Models.Dtos;

namespace AuthService.Services 
{
    public interface IAuthService
    {
        Task<ServiceResponse<UserDto>> RegisterAsync(RegisterModel model);
        Task<ServiceResponse<TokenResponseDto>> LoginAsync(LoginModel model);
        Task<ServiceResponse<TokenResponseDto>> RefreshTokenAsync(TokenRequestDto tokenRequest);
    }

}
