using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models
{
    /// <summary>
    /// One row per REST API call (public or keyed). Used by the Admin key page and the
    /// Manager "API usage" report to show who is using the API and what they search for.
    /// </summary>
    public class ApiRequestLog
    {
        [Key]
        public int Id { get; set; }

        /// <summary>When the request arrived (UTC).</summary>
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        /// <summary>"Public" for /api/public/*, "Key" for /api/v1/*.</summary>
        [Required, StringLength(10)]
        public string Channel { get; set; } = "Public";

        /// <summary>The API key used, if any (null for public calls and rejected keys).</summary>
        public int? ApiKeyId { get; set; }

        /// <summary>Request path, e.g. /api/v1/items.</summary>
        [Required, StringLength(200)]
        public string Path { get; set; } = string.Empty;

        /// <summary>The search text (?q=) if one was sent, for "most searched" stats.</summary>
        [StringLength(100)]
        public string? SearchTerm { get; set; }

        /// <summary>HTTP status code returned (200, 401, 404, ...).</summary>
        public int StatusCode { get; set; }
    }
}
