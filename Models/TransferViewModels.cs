using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models;

public class TransferRequestFormViewModel
{
    [Required]
    [Display(Name = "Item category")]
    public string ItemCategory { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    [Display(Name = "From branch")]
    public int FromBranchId { get; set; }

    [Range(1, int.MaxValue)]
    [Display(Name = "To branch")]
    public int ToBranchId { get; set; }

    public List<int> SelectedItemIds { get; set; } = [];
    public IReadOnlyList<Item> AvailableItems { get; set; } = [];
}

public class TransferIndexViewModel
{
    public IReadOnlyList<TransferRequest> Requests { get; set; } = [];
    public IReadOnlyList<ItemTransfer> Transfers { get; set; } = [];
}

public class TransferRequestReviewRow
{
    public required TransferRequest Request { get; init; }
    public int SelectedAvailable { get; init; }
}
