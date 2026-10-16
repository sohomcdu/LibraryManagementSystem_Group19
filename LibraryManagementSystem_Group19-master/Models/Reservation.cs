using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models
{
    public class Reservation
    {
        public int Id { get; set; }

        public int ItemId { get; set; }
        public Item? Item { get; set; }

        public int BorrowerId { get; set; }
        public Borrower? Borrower { get; set; }

        public DateTime RequestedDate { get; set; } = DateTime.Now;

        // "Waiting" -> "Ready" (item became Available, pickup window open)
        // -> "Fulfilled" (collected) or "Expired" (window passed) or "Cancelled".
        [Required]
        public string Status { get; set; } = "Waiting";

        public DateTime? PickupExpiresAt { get; set; }
    }
}