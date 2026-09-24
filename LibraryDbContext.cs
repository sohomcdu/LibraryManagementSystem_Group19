using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Data
{
    /// 
    /// The application's single EF Core database context. Inherits from
    /// IdentityDbContext&lt;ApplicationUser&gt; so the login/role tables
    /// (AspNetUsers, AspNetRoles, etc.) live in the same database as the
    /// library's own tables, rather than needing a second context.
    /// 
    public class LibraryDbContext : IdentityDbContext<ApplicationUser>
    {
        /// Creates the context with the connection options configured in Program.cs.
        public LibraryDbContext(DbContextOptions<LibraryDbContext> options)
            : base(options) { }

        /// Required override so Identity's own entity/table mappings are still applied alongside our own.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // Ensures Identity table mappings are applied
        }

        /// All items regardless of type (Book/Music/Toy), mapped via Table-Per-Hierarchy inheritance.
        public DbSet<Item> Items { get; set; } = null!;

        /// Convenience DbSet for querying only Book items.
        public DbSet<Book> Books { get; set; } = null!;

        /// Convenience DbSet for querying only Music items.
        public DbSet<Music> Music { get; set; } = null!;

        /// Convenience DbSet for querying only Toy items.
        public DbSet<Toy> Toys { get; set; } = null!;

        /// Registered library patrons.
        public DbSet<Borrower> Borrowers { get; set; } = null!;

        /// Confirmed loans (created directly by Reception, or via an approved BorrowRequest).
        public DbSet<BorrowRecord> BorrowRecords { get; set; } = null!;

        /// Members' pending/approved/rejected requests to borrow an item.
        public DbSet<BorrowRequest> BorrowRequests { get; set; } = null!;
    }
}
