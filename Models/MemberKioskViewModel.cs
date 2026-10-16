namespace LibraryManagementSystem.Models;

/// <summary>Page model for the member self-service kiosk (Me/Kiosk).</summary>
public sealed class MemberKioskViewModel
{
    public Borrower? Borrower { get; set; }
    public List<BorrowRecord> ActiveLoans { get; set; } = new();
    public List<Branch> Branches { get; set; } = new();
    public string? BranchCode { get; set; }
    public string? ItemCode { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}
