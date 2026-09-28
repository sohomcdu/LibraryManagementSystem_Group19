using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Services;

namespace LibraryManagementSystem.Controllers
{
    [Authorize(Roles = "Reception,Admin,Manager")]
    public class NotificationController : Controller
    {
        private readonly LibraryDbContext _context;
        public NotificationController(LibraryDbContext context) => _context = context;

        public async Task<IActionResult> Index()
        {
            var notifications = await _context.Notifications
                .OrderByDescending(n => n.SentAt)
                .Take(200)
                .ToListAsync();
            return View(notifications);
        }

        // No background job scheduler is set up, so this button lets staff
        // trigger the due-soon/overdue sweep on demand for demo purposes.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunSweep()
        {
            await NotificationService.RunDueDateSweepAsync(_context);
            return RedirectToAction(nameof(Index));
        }
    }
}