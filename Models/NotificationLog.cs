using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models
{
    public class NotificationLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string PatronId { get; set; } = string.Empty;

        [Required]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;
    }
}