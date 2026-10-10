using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models;

public sealed class KioskActivationViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
