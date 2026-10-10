using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models
{
    // A simulated notification: nothing is actually emailed/texted, this is
    // logged to a dashboard table (and could later be wired to a real
    // email/SMS provider without changing any calling code).
    public class Notification
    {
        public int Id { get; set; }

        // "Borrow", "DueSoon", "Overdue", "HoldAvailable", "TransferArrived"
        [Required]
        public string Type { get; set; } = string.Empty;

        // "Email", "SMS", "Staff"
        [Required]
        public string Channel { get; set; } = string.Empty;

        public string Recipient { get; set; } = string.Empty;

        [Required]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string Body { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.Now;
    }
}