using Microsoft.AspNetCore.Identity;

namespace LibraryManagementSystem.Models
{
    /// 
    /// Extends ASP.NET Core Identity's built-in IdentityUser so login accounts
    /// can carry a display name alongside the standard email/username/password
    /// fields Identity already manages. Roles (Admin, Reception, Manager,
    /// Member) are assigned separately through IdentityRole and are not
    /// stored on this class directly.
    /// 
    public class ApplicationUser : IdentityUser
    {
        /// The user's full name, shown in the UI instead of their raw email/username.
        public string FullName { get; set; } = string.Empty;
    }
}
