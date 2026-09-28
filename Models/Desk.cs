using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models
{
    // A physical check-out station within a Branch.
    public class Desk
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Label { get; set; } = string.Empty;

        public int BranchId { get; set; }
        public Branch? Branch { get; set; }

        public bool IsActive { get; set; } = true;
    }
}