using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models
{
    public class ImportJob
    {
        public int Id { get; set; }

        [Required]
        public string Source { get; set; } = "CSV"; // "CSV" or "MockApi"

        public int RowCount { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }

        public string PerformedByEmail { get; set; } = string.Empty;
        public DateTime PerformedAt { get; set; } = DateTime.Now;
    }
}