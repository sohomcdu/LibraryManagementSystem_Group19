using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models
{
    /// 
    /// A toy in the library's collection. Inherits the shared Item fields
    /// and adds the extra fields the brief asks for on toys specifically
    /// (type/age).
    /// 
    public class Toy : Item
    {
        /// The toy's category, selected from a fixed dropdown list in the UI (e.g. Puzzle, Board Game).
        public string Type { get; set; } = string.Empty;

        /// The youngest age this toy is considered safe/appropriate for.
        [Display(Name = "Minimum Age")]
        public int MinimumAge { get; set; }
    }
}
