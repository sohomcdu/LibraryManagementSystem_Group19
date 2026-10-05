using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibraHub.Domain;

public class Branch
{
    public int Id { get; set; }
    [MaxLength(5)] public string Code { get; set; } = "";
    [MaxLength(80)] public string Name { get; set; } = "";
    public TimeOnly OpensAt { get; set; } = new(9, 0);
    public TimeOnly ClosesAt { get; set; } = new(17, 0);
    /// <summary>Comma separated DayOfWeek names, e.g. "Sunday,Monday".</summary>
    public string ClosedDays { get; set; } = "";
    public List<Desk> Desks { get; set; } = new();
}

public class Desk
{
    public int Id { get; set; }
    [MaxLength(60)] public string Name { get; set; } = "";
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public bool IsKiosk { get; set; }
}

public class Category
{
    public int Id { get; set; }
    [MaxLength(60)] public string Name { get; set; } = "";
}

public class AppUser
{
    public int Id { get; set; }
    [MaxLength(120)] public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    [MaxLength(100)] public string FullName { get; set; } = "";
    [MaxLength(30)] public string? Mobile { get; set; }
    public Role Role { get; set; }
    [MaxLength(20)] public string? CardNumber { get; set; }
    public string? PinHash { get; set; }
    public int? HomeBranchId { get; set; }
    public Branch? HomeBranch { get; set; }
    public int? DeskId { get; set; }
    public Desk? Desk { get; set; }

    // F3 – channel preferences per notification type
    public Channels PrefBorrow { get; set; } = Channels.Email;
    public Channels PrefDueSoon { get; set; } = Channels.Email;
    public Channels PrefOverdue { get; set; } = Channels.Email;
    public Channels PrefHold { get; set; } = Channels.Email;

    public int FailedLogins { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class Item
{
    public int Id { get; set; }
    [MaxLength(12)] public string Code { get; set; } = "";
    [MaxLength(200)] public string Title { get; set; } = "";
    [MaxLength(300)] public string? Subtitle { get; set; }
    [MaxLength(150)] public string Author { get; set; } = "";
    [MaxLength(13)] public string Isbn { get; set; } = "";
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    [MaxLength(100)] public string? Publisher { get; set; }
    public int? Year { get; set; }
    [MaxLength(40)] public string? Language { get; set; } = "English";
    public int? Pages { get; set; }
    public string? Description { get; set; }
    /// <summary>Two hex colours "#rrggbb,#rrggbb" for the generated cover placeholder.</summary>
    public string CoverColors { get; set; } = "#12324A,#0F766E";
    public int HomeBranchId { get; set; }
    public Branch HomeBranch { get; set; } = null!;
    public int CurrentBranchId { get; set; }
    public Branch CurrentBranch { get; set; } = null!;
    public ItemStatus Status { get; set; } = ItemStatus.Available;
    public string? StatusNote { get; set; }
    /// <summary>Lower-case, accent-free text used by the catalogue search.</summary>
    public string SearchText { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    /// <summary>Optimistic concurrency token (spec page 10 – "concurrency check on the item row").</summary>
    [ConcurrencyCheck] public int Version { get; set; }
    public List<ItemEvent> History { get; set; } = new();

    public bool AwayFromHome => HomeBranchId != CurrentBranchId;
}

public class ItemEvent
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public DateTime At { get; set; } = DateTime.Now;
    [MaxLength(300)] public string Text { get; set; } = "";
    [MaxLength(100)] public string? By { get; set; }
}

public class Loan
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public int PatronId { get; set; }
    public AppUser Patron { get; set; } = null!;
    public int? DeskId { get; set; }
    public Desk? Desk { get; set; }
    public DateTime BorrowedAt { get; set; }
    public DateTime DueAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public int Renewals { get; set; }

    public bool IsActive => ReturnedAt == null;
    public int DaysLate(DateTime now) =>
        Math.Max(0, ((ReturnedAt ?? now).Date - DueAt.Date).Days);
}

public class Fine
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public Loan Loan { get; set; } = null!;
    public int PatronId { get; set; }
    public AppUser Patron { get; set; } = null!;
    public int? DeskId { get; set; }
    public Desk? Desk { get; set; }
    public int DaysLate { get; set; }
    public int AmountCents { get; set; }
    public DateTime IssuedAt { get; set; }
    public FineStatus Status { get; set; } = FineStatus.Outstanding;
    public DateTime? SettledAt { get; set; }
    [NotMapped] public decimal Amount => AmountCents / 100m;
}

public class Reservation
{
    public int Id { get; set; }
    [MaxLength(13)] public string Isbn { get; set; } = "";
    [MaxLength(200)] public string TitleText { get; set; } = "";
    public int PatronId { get; set; }
    public AppUser Patron { get; set; } = null!;
    public int PickupBranchId { get; set; }
    public Branch PickupBranch { get; set; } = null!;
    public DateTime PlacedAt { get; set; }
    /// <summary>Queue ordering key. Equals PlacedAt unless a manager moves the hold to the front.</summary>
    public DateTime QueueKey { get; set; }
    public HoldStatus Status { get; set; } = HoldStatus.Waiting;
    public int? ReadyItemId { get; set; }
    public Item? ReadyItem { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    [MaxLength(10)] public string? PickupCode { get; set; }
    public bool IsActive => Status is HoldStatus.Waiting or HoldStatus.Ready;
}

public class BranchTransfer
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public int FromBranchId { get; set; }
    public Branch FromBranch { get; set; } = null!;
    public int ToBranchId { get; set; }
    public Branch ToBranch { get; set; } = null!;
    [MaxLength(40)] public string Reason { get; set; } = "Patron request";
    public TransferStatus Status { get; set; } = TransferStatus.Requested;
    public DateTime RequestedAt { get; set; }
    [MaxLength(100)] public string? RequestedBy { get; set; }
    public DateTime? DispatchedAt { get; set; }
    [MaxLength(100)] public string? DispatchedBy { get; set; }
    public DateTime? ReceivedAt { get; set; }
    [MaxLength(100)] public string? ReceivedBy { get; set; }
    [NotMapped] public string Number => $"T-{Id:D3}";
}

public class NotificationLog
{
    public int Id { get; set; }
    public int? PatronId { get; set; }
    public AppUser? Patron { get; set; }
    [MaxLength(120)] public string Recipient { get; set; } = "";
    public NotificationType Type { get; set; }
    public Channel Channel { get; set; }
    [MaxLength(200)] public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public SendStatus Status { get; set; }
    [MaxLength(200)] public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    /// <summary>Idempotency key – unique per loan/type/day/channel (spec F3).</summary>
    [MaxLength(120)] public string? UniqueKey { get; set; }
}

public class ApiKey
{
    public int Id { get; set; }
    [MaxLength(80)] public string Name { get; set; } = "";
    [MaxLength(64)] public string KeyHash { get; set; } = "";
    [MaxLength(12)] public string Prefix { get; set; } = "";
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int UsageCount { get; set; }
    public int RejectedCount { get; set; }
}

public class ImportJob
{
    public int Id { get; set; }
    [MaxLength(60)] public string Source { get; set; } = "";
    [MaxLength(100)] public string UserName { get; set; } = "";
    public DateTime At { get; set; }
    public int TotalRows { get; set; }
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
}

public class Setting
{
    [Key, MaxLength(40)] public string Key { get; set; } = "";
    [MaxLength(200)] public string Value { get; set; } = "";
}
