namespace LibraryManagementSystem.Models;

/// <summary>Read-only summary of the signed-in member's own library activity.</summary>
public sealed class MeDashboardViewModel
{
    public Borrower Borrower { get; set; } = null!;
    public List<BorrowRecord> ActiveLoans { get; set; } = new();
    public List<Reservation> Reservations { get; set; } = new();
    public List<Notification> Notifications { get; set; } = new();
    public decimal RecordedFines { get; set; }
    public decimal EstimatedOutstandingFines { get; set; }
    public int OverdueLoanCount { get; set; }
}
