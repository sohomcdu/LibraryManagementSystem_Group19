using LibraHub.Domain;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Desk> Desks => Set<Desk>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemEvent> ItemEvents => Set<ItemEvent>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<Fine> Fines => Set<Fine>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<BranchTransfer> Transfers => Set<BranchTransfer>();
    public DbSet<NotificationLog> Notifications => Set<NotificationLog>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<Setting> Settings => Set<Setting>();

    // Store enums as readable strings so the SQLite file is easy to inspect.
    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<Role>().HaveConversion<string>();
        b.Properties<ItemStatus>().HaveConversion<string>();
        b.Properties<HoldStatus>().HaveConversion<string>();
        b.Properties<TransferStatus>().HaveConversion<string>();
        b.Properties<NotificationType>().HaveConversion<string>();
        b.Properties<Channel>().HaveConversion<string>();
        b.Properties<SendStatus>().HaveConversion<string>();
        b.Properties<FineStatus>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        foreach (var fk in m.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;

        m.Entity<Branch>().HasIndex(x => x.Code).IsUnique();
        m.Entity<Category>().HasIndex(x => x.Name).IsUnique();
        m.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
        m.Entity<AppUser>().HasIndex(x => x.CardNumber).IsUnique();

        m.Entity<Item>().HasIndex(x => x.Code).IsUnique();
        m.Entity<Item>().HasIndex(x => x.Isbn);
        m.Entity<Item>().HasOne(x => x.HomeBranch).WithMany().HasForeignKey(x => x.HomeBranchId);
        m.Entity<Item>().HasOne(x => x.CurrentBranch).WithMany().HasForeignKey(x => x.CurrentBranchId);

        m.Entity<Reservation>().HasIndex(x => new { x.Isbn, x.Status });
        m.Entity<Reservation>().HasOne(x => x.ReadyItem).WithMany().HasForeignKey(x => x.ReadyItemId);

        m.Entity<BranchTransfer>().HasOne(x => x.FromBranch).WithMany().HasForeignKey(x => x.FromBranchId);
        m.Entity<BranchTransfer>().HasOne(x => x.ToBranch).WithMany().HasForeignKey(x => x.ToBranchId);

        // F3 – the unique key makes the scanner idempotent ("Scanner runs twice → no duplicates").
        m.Entity<NotificationLog>().HasIndex(x => x.UniqueKey).IsUnique();
        m.Entity<ApiKey>().HasIndex(x => x.KeyHash).IsUnique();
    }
}
