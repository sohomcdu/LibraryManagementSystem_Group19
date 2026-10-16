using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models
{
    /// <summary>
    /// A credential issued by an Admin to an external application that calls the
    /// keyed REST API (/api/v1/*). Only a SHA-256 hash of the key is stored.
    /// </summary>
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

        /// <summary>When this key last made a successful request (UTC). Null = never used.</summary>
        public DateTime? LastUsedAt { get; set; }

        /// <summary>Number of successful requests made with this key.</summary>
        public int RequestCount { get; set; }
    }
}
