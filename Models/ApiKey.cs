using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models
{
    public class ApiKey
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ClientName { get; set; } = string.Empty;

        [Required]
        public string KeyHash { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}