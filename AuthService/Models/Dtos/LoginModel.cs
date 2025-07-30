namespace AuthService.Models.Dtos
{
    public class LoginModel
    {
        public string UsernameOrEmail { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

}
