using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Data;

/// 
/// Reception can view borrower records; Admin manages them.
/// 
[Authorize(Roles = "Reception,Admin")]
public class BorrowerController : Controller
{
    /// EF Core database context, injected via dependency injection.
    private readonly LibraryDbContext _context;

    /// Creates the controller with its required database context.
    public BorrowerController(LibraryDbContext context)
    {
        _context = context;
    }

    /// GET: Borrower - lists every registered borrower.
    public async Task<IActionResult> Index()    
    {
        return View(await _context.Borrowers.ToListAsync());
    }

    /// GET: Borrower/Details/5 - shows the full details of a single borrower.
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var borrower = await _context.Borrowers
            .FirstOrDefaultAsync(m => m.Id == id);
        if (borrower == null)
        {
            return NotFound();
        }

        return View(borrower);
    }

    /// GET: Borrower/Create - shows the blank form for registering a new borrower manually.
    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        return View();
    }

    /// 
    /// POST: Borrower/Create - saves a new borrower after validating the
    /// submitted form. Only binds the four safe fields (overposting
    /// protection), so ApplicationUserId can never be set from this form -
    /// it is only ever set automatically during public self-registration.
    /// 
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,FullName,Email,Phone")] Borrower borrower)
    {
        if (ModelState.IsValid)
        {
            _context.Add(borrower);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(borrower);
    }

    /// GET: Borrower/Edit/5 - shows the edit form pre-filled with the existing borrower's data.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var borrower = await _context.Borrowers.FindAsync(id);
        if (borrower == null)
        {
            return NotFound();
        }
        return View(borrower);
    }

    /// POST: Borrower/Edit/5 - saves changes to an existing borrower, guarding against concurrent edits.
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,FullName,Email,Phone")] Borrower borrower)
    {
        if (id != borrower.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(borrower);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Someone else deleted this record between the page loading and the save.
                if (!BorrowerExists(borrower.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(borrower);
    }

    /// GET: Borrower/Delete/5 - shows a confirmation page before deleting a borrower.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var borrower = await _context.Borrowers
            .FirstOrDefaultAsync(m => m.Id == id);
        if (borrower == null)
        {
            return NotFound();
        }

        return View(borrower);
    }

    /// POST: Borrower/Delete/5 - actually removes the borrower after the confirmation page is submitted.
    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var borrower = await _context.Borrowers.FindAsync(id);
        if (borrower != null)
        {
            _context.Borrowers.Remove(borrower);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    /// Checks whether a borrower with the given Id still exists, used to tell a real 404 apart from a concurrency conflict.
    private bool BorrowerExists(int? id)
    {
        return _context.Borrowers.Any(e => e.Id == id);
    }
}
