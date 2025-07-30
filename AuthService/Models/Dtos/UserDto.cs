namespace AuthService.Models.Dtos
{
    public class UserDto
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public bool IsActivateMobile { get; set; }
        public bool IsActivateEmail { get; set; }
        public string Role { get; set; } = string.Empty;
    }
}
