using Microsoft.AspNetCore.Mvc;

namespace LibraryManagementSystem.Controllers
{
    /// 
    /// Serves the public homepage. No authorization required - this is the
    /// landing page every visitor sees first, with shortcuts to Search,
    /// Register, and Login.
    /// 
    public class HomeController : Controller
    {
        /// GET: Home - renders the homepage banner and shortcut cards.
        public IActionResult Index()
        {
            return View();
        }
    }
}
