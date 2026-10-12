using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models
{
    /// 
    /// A music item in the library's collection. Inherits the shared Item
    /// fields and adds the extra fields the brief asks for on music
    /// specifically (artist/year).
    /// 
    public class Music : Item
    {
        /// The performing artist or band.
        public string Artist { get; set; } = string.Empty;

        /// The year this recording was released.
        [Display(Name = "Release Year")]
        public int ReleaseYear { get; set; }
    }
}
