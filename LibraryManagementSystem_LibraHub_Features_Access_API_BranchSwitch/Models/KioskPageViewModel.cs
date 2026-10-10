namespace LibraryManagementSystem.Models;

public sealed class KioskPageViewModel
{
    public string? CardEmail { get; set; }
    public string? ItemCode { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public Borrower? Borrower { get; set; }
    public List<BorrowRecord> ActiveLoans { get; set; } = new();
    public Item? ScannedItem { get; set; }
}
