using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models
{
    public class Branch
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(10)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string Address { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public ICollection<Desk> Desks { get; set; } = new List<Desk>();
    }
}