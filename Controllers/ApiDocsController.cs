using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagementSystem.Controllers
{
    /// <summary>
    /// Public developer documentation for the read-only REST API (/ApiDocs).
    /// Anyone can read it; calling /api/v1 still needs an API key from an Admin.
    /// </summary>
    [AllowAnonymous]
    public class ApiDocsController : Controller
    {
        public IActionResult Index() => View();
    }
}
