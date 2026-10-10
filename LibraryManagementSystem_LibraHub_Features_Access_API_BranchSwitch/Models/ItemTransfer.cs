using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models
{
    // Records one item moving from one branch to another after approval.
    public class ItemTransfer
    {
        public int Id { get; set; }

        public int ItemId { get; set; }
        public Item? Item { get; set; }

        public int FromBranchId { get; set; }
        public Branch? FromBranch { get; set; }

        public int ToBranchId { get; set; }
        public Branch? ToBranch { get; set; }

        public int? TransferRequestId { get; set; }
        public TransferRequest? TransferRequest { get; set; }

        public DateTime InitiatedDate { get; set; } = DateTime.Now;
        public DateTime? ReceivedDate { get; set; }

        // "InTransit" or "Completed".
        [Required]
        public string Status { get; set; } = "InTransit";
    }
}