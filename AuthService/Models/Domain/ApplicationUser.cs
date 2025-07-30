using Microsoft.AspNetCore.Identity;
using System.Data;

namespace AuthService.Models.Domain
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string MobileActiveCode { get; set; } = string.Empty;
        public bool IsActivateMobile { get; set; }
        public bool IsActivateEmail { get; set; }
        public Role Role { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime RefreshTokenExpiryTime { get; set; }
    }

    public enum Role
    {
        Player,
        Admin,
        SuperAdmin
    }
}
