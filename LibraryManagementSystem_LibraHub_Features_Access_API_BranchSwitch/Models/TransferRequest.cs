using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models;

public class TransferRequest
{
    public int Id { get; set; }

    [Required]
    [StringLength(20)]
    public string ItemCategory { get; set; } = string.Empty;

    public int FromBranchId { get; set; }
    public Branch? FromBranch { get; set; }

    public int ToBranchId { get; set; }
    public Branch? ToBranch { get; set; }

    [Range(1, 1000)]
    public int Quantity { get; set; }

    [Required]
    [StringLength(256)]
    public string RequestedBy { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; } = DateTime.Now;

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Pending";

    [StringLength(256)]
    public string? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    [StringLength(500)]
    public string? DecisionNote { get; set; }

    public ICollection<ItemTransfer> Transfers { get; set; } = new List<ItemTransfer>();
    public ICollection<TransferRequestItem> RequestedItems { get; set; } = new List<TransferRequestItem>();
}

public class TransferRequestItem
{
    public int Id { get; set; }

    public int TransferRequestId { get; set; }
    public TransferRequest? TransferRequest { get; set; }

    public int ItemId { get; set; }
    public Item? Item { get; set; }
}
