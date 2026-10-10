using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagementSystem.Controllers
{
    /// 
    /// Handles authentication for every role: login, logout, and public
    /// self-registration for Members. Built on top of ASP.NET Core Identity's
    /// SignInManager/UserManager rather than the full scaffolded Identity UI,
    /// to keep the login/register pages simple and fully under our own control.
    /// 
    public class AccountController : Controller
    {
        /// Handles the actual sign-in/sign-out cookie work.
        private readonly SignInManager<ApplicationUser> _signInManager;

        /// Handles creating and looking up user accounts.
        private readonly UserManager<ApplicationUser> _userManager;

        /// EF Core database context, used here to create the linked Borrower profile on registration.
        private readonly LibraryDbContext _context;

        /// Creates the controller with its required Identity services and database context.
        public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, LibraryDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
        }

        /// GET: Account/Login - shows the login form. Accepts an optional returnUrl so a user can be sent back to the page they came from after logging in.
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        /// POST: Account/Login - verifies the submitted credentials and signs the user in if they're correct.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["Email"] = email;

            var result = await _signInManager.PasswordSignInAsync(email, password, isPersistent: false, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View();
        }

        /// GET: Account/Register - shows the public signup form. Anyone can reach this page, no login required.
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        /// 
        /// POST: Account/Register - creates a new login account in the Member
        /// role, then creates and links a matching Borrower profile so
        /// staff can find this person the same way as any other borrower.
        /// The new user is signed in immediately on success.
        /// 
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string fullName, string email, string phone, string password, string confirmPassword)
        {
            ViewData["FullName"] = fullName;
            ViewData["Email"] = email;
            ViewData["Phone"] = phone;

            if (password != confirmPassword)
            {
                ModelState.AddModelError("confirmPassword", "Passwords do not match.");
                return View();
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View();
            }

            await _userManager.AddToRoleAsync(user, "Member");

            // Every self-registered Member gets a matching Borrower profile so
            // Admin's borrower and circulation workflows treat this profile
            // the same as one created manually.
            var borrower = new Borrower
            {
                FullName = fullName,
                Email = email,
                Phone = phone ?? string.Empty,
                ApplicationUserId = user.Id
            };
            _context.Borrowers.Add(borrower);
            await _context.SaveChangesAsync();

            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Home");
        }

        /// POST: Account/Logout - clears the current sign-in cookie.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        /// Shown when a signed-in user tries to reach a page their role doesn't have permission for.
        [Authorize]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
