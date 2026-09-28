using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Data
{
    public class LibraryDbContext : IdentityDbContext<ApplicationUser>
    {
        public LibraryDbContext(DbContextOptions<LibraryDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Prevent SQL Server's multiple cascade path problem.
            // An ItemTransfer has two relationships to Branch:
            // FromBranch and ToBranch.
            modelBuilder.Entity<ItemTransfer>()
                .HasOne(t => t.FromBranch)
                .WithMany()
                .HasForeignKey(t => t.FromBranchId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ItemTransfer>()
                .HasOne(t => t.ToBranch)
                .WithMany()
                .HasForeignKey(t => t.ToBranchId)
                .OnDelete(DeleteBehavior.NoAction);
        }

        public DbSet<Item> Items { get; set; } = null!;
        public DbSet<Book> Books { get; set; } = null!;
        public DbSet<Music> Music { get; set; } = null!;
        public DbSet<Toy> Toys { get; set; } = null!;
        public DbSet<Borrower> Borrowers { get; set; } = null!;
        public DbSet<BorrowRecord> BorrowRecords { get; set; } = null!;
        public DbSet<BorrowRequest> BorrowRequests { get; set; } = null!;

        // Group project Part B features
        public DbSet<Branch> Branches { get; set; } = null!;
        public DbSet<Desk> Desks { get; set; } = null!;
        public DbSet<ItemTransfer> ItemTransfers { get; set; } = null!;
        public DbSet<Reservation> Reservations { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<ImportJob> ImportJobs { get; set; } = null!;
    }
}