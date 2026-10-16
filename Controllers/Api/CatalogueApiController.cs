using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers.Api
{
    /// <summary>
    /// Read-only REST API for external applications.
    ///
    /// The same endpoints are served on two routes and return identical JSON:
    ///   /api/v1/...      for registered integrations - requires an X-API-Key header
    ///   /api/public/...  open discovery endpoints - no key needed
    ///
    /// Key checking and request logging happen in <see cref="ApiAccessFilter"/>.
    /// [AllowAnonymous] only means "no website sign-in cookie is needed"; it does not
    /// switch off the API key check. Only GET is supported, so the API cannot change data.
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    [Route("api/v1")]
    [Route("api/public")]
    [ServiceFilter(typeof(ApiAccessFilter))]
    [Produces("application/json")]
    public sealed class CatalogueApiController : ControllerBase
    {
        private const int MaxPageSize = 100;
        private readonly LibraryDbContext _db;

        public CatalogueApiController(LibraryDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// GET items - search the catalogue.
        /// Query options: q (text), type (book|music|toy), category (genre / toy type),
        /// branch (CEN|NTH|RIV), status (available|all, default available), page, pageSize.
        /// </summary>
        [HttpGet("items")]
        public async Task<IActionResult> Items(
            [FromQuery] string? q,
            [FromQuery] string? type,
            [FromQuery] string? category,
            [FromQuery] string? branch,
            [FromQuery] string? status = "available",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page < 1)
                return BadRequestProblem("page must be 1 or more.");
            if (pageSize < 1 || pageSize > MaxPageSize)
                return BadRequestProblem($"pageSize must be between 1 and {MaxPageSize}.");

            IQueryable<Item> query = _db.Items.AsNoTracking().Include(i => i.Branch);

            // Status: only available items unless the caller asks for everything.
            var wantAll = string.Equals(status, "all", StringComparison.OrdinalIgnoreCase);
            if (!wantAll)
            {
                if (!string.Equals(status, "available", StringComparison.OrdinalIgnoreCase))
                    return BadRequestProblem("status must be 'available' or 'all'.");
                query = query.Where(i => i.Status == "Available");
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                switch (NormaliseType(type))
                {
                    case "Book": query = query.Where(i => i is Book); break;
                    case "Music": query = query.Where(i => i is Music); break;
                    case "Toy": query = query.Where(i => i is Toy); break;
                    default: return BadRequestProblem("type must be book, music or toy.");
                }
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                var c = category.Trim().ToLower();
                query = query.Where(i =>
                    (i is Book && ((Book)i).Genre.ToLower() == c) ||
                    (i is Toy && ((Toy)i).Type.ToLower() == c) ||
                    (i is Music && c == "music"));
            }

            if (!string.IsNullOrWhiteSpace(branch))
            {
                var code = branch.Trim().ToUpper();
                query = query.Where(i => i.Branch != null && i.Branch.Code == code);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                // Case-insensitive search: "code" finds "Clean Code".
                var term = q.Trim().ToLower();
                query = query.Where(i =>
                    i.Name.ToLower().Contains(term) ||
                    i.LibraryCode.ToLower().Contains(term) ||
                    i.Description.ToLower().Contains(term) ||
                    (i is Book && ((Book)i).Author.ToLower().Contains(term)) ||
                    (i is Music && ((Music)i).Artist.ToLower().Contains(term)));
            }

            // Count and page in the database rather than loading the whole catalogue.
            var total = await query.CountAsync();
            var rows = await query
                .OrderBy(i => i.Name).ThenBy(i => i.LibraryCode)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                page,
                pageSize,
                total,
                totalPages = (int)Math.Ceiling(total / (double)pageSize),
                items = rows.Select(ToSummary)
            });
        }

        /// <summary>GET items/{id} - full details of one item, or 404.</summary>
        [HttpGet("items/{id:int}")]
        public async Task<IActionResult> Item(int id)
        {
            var item = await _db.Items.AsNoTracking().Include(i => i.Branch).FirstOrDefaultAsync(i => i.Id == id);
            if (item == null)
            {
                return Problem(statusCode: 404, title: "Not Found", detail: $"No catalogue item with id {id}.");
            }

            return Ok(new
            {
                item.Id,
                item.LibraryCode,
                item.Name,
                type = TypeOf(item),
                category = CategoryOf(item),
                creator = CreatorOf(item),
                item.Description,
                item.Status,
                isAvailable = item.Status == "Available",
                branch = BranchOf(item.Branch),
                isbn = item is Book b && !string.IsNullOrWhiteSpace(b.Isbn) ? b.Isbn : null,
                releaseYear = item is Music m ? m.ReleaseYear : (int?)null,
                minimumAge = item is Toy t ? t.MinimumAge : (int?)null
            });
        }

        /// <summary>GET categories - item types and their categories, with available counts.</summary>
        [HttpGet("categories")]
        public async Task<IActionResult> Categories()
        {
            var books = await _db.Books.AsNoTracking()
                .GroupBy(b => b.Genre)
                .Select(g => new { name = g.Key, available = g.Count(b => b.Status == "Available") })
                .ToListAsync();
            var toys = await _db.Toys.AsNoTracking()
                .GroupBy(t => t.Type)
                .Select(g => new { name = g.Key, available = g.Count(t => t.Status == "Available") })
                .ToListAsync();
            var musicAvailable = await _db.Music.AsNoTracking().CountAsync(m => m.Status == "Available");

            var types = new[]
            {
                new
                {
                    type = "Book",
                    slug = "book",
                    available = books.Sum(x => x.available),
                    categories = books.Where(x => !string.IsNullOrWhiteSpace(x.name)).OrderBy(x => x.name).ToList()
                },
                new
                {
                    type = "Music",
                    slug = "music",
                    available = musicAvailable,
                    categories = new[] { new { name = "Music", available = musicAvailable } }.ToList()
                },
                new
                {
                    type = "Toy",
                    slug = "toy",
                    available = toys.Sum(x => x.available),
                    categories = toys.Where(x => !string.IsNullOrWhiteSpace(x.name)).OrderBy(x => x.name).ToList()
                }
            };

            return Ok(new { count = types.Length, types });
        }

        /// <summary>GET branches - library branches and how many items are available at each.</summary>
        [HttpGet("branches")]
        public async Task<IActionResult> Branches()
        {
            var branches = await _db.Branches.AsNoTracking()
                .OrderBy(b => b.Name)
                .Select(b => new
                {
                    code = b.Code,
                    name = b.Name,
                    address = b.Address,
                    isOpen = b.IsActive,
                    availableItems = _db.Items.Count(i => i.BranchId == b.Id && i.Status == "Available")
                })
                .ToListAsync();

            return Ok(new { count = branches.Count, branches });
        }

        /// <summary>GET status - is the service up, and a catalogue summary.</summary>
        [HttpGet("status")]
        public async Task<IActionResult> Status()
        {
            var branches = await _db.Branches.AsNoTracking()
                .OrderBy(b => b.Name)
                .Select(b => new { code = b.Code, name = b.Name, isOpen = b.IsActive })
                .ToListAsync();

            return Ok(new
            {
                service = "Library Management System API",
                version = "v1",
                status = "operational",
                checkedAt = DateTimeOffset.UtcNow,
                catalogue = new
                {
                    totalItems = await _db.Items.CountAsync(),
                    availableItems = await _db.Items.CountAsync(i => i.Status == "Available")
                },
                branches
            });
        }

        // ---------- helpers that keep every endpoint's JSON consistent ----------

        private static object ToSummary(Item i) => new
        {
            i.Id,
            i.LibraryCode,
            i.Name,
            type = TypeOf(i),
            category = CategoryOf(i),
            creator = CreatorOf(i),
            i.Status,
            branch = BranchOf(i.Branch)
        };

        private static object? BranchOf(Branch? b) => b == null ? null : new { code = b.Code, name = b.Name };

        private static string TypeOf(Item i) => i switch { Book => "Book", Music => "Music", Toy => "Toy", _ => "Other" };

        private static string CategoryOf(Item i) => i switch
        {
            Book b => b.Genre,
            Toy t => t.Type,
            Music => "Music",
            _ => string.Empty
        };

        private static string? CreatorOf(Item i) => i switch
        {
            Book b => b.Author,
            Music m => m.Artist,
            _ => null
        };

        private static string? NormaliseType(string type) => type.Trim().ToLowerInvariant() switch
        {
            "book" or "books" => "Book",
            "music" or "album" or "albums" => "Music",
            "toy" or "toys" => "Toy",
            _ => null
        };

        private ObjectResult BadRequestProblem(string detail) =>
            Problem(statusCode: 400, title: "Bad Request", detail: detail);
    }
}
