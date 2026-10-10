using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models
{
    /// <summary>
    /// Abstract base class for anything the library lends out.
    /// Book, Music, and Toy all inherit from this class.
    /// </summary>
    public abstract class Item
    {
        public int Id { get; set; }

        [Required]
        public string LibraryCode { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Now one of six values: "Available", "Borrowed", "Damaged",
        /// "Destroy", "Reserved" (held for a waitlisted patron), or
        /// "InTransit" (moving between branches). The last two are new
        /// for the group project's F4/F5 features.
        /// </summary>
        [Required]
        public string Status { get; set; } = "Available";

        /// <summary>Which branch currently holds this item. Nullable so existing baseline data (no branch assigned yet) doesn't break.</summary>
        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }
    }
}