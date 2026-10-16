using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models
{
    public class Loan
    {
        [Key]
        public Guid LoanId { get; set; } = Guid.NewGuid();

        [Required]
        public string BookISBN { get; set; } = string.Empty;

        [Required]
        public string MemberId { get; set; } = string.Empty;

        public DateTime BorrowDate { get; set; } = DateTime.UtcNow;
        public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(14);
        public DateTime? ReturnDate { get; set; }

        public decimal CalculateFine(decimal dailyFineRate = 1.50m)
        {
            DateTime effectiveReturn = ReturnDate ?? DateTime.UtcNow;
            if (effectiveReturn <= DueDate) return 0.00m;

            int overdueDays = (effectiveReturn - DueDate).Days;
            return overdueDays * dailyFineRate;
        }
    }
}