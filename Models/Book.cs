namespace LibraryManagementSystem.Models
{
    /// 
    /// A book in the library's collection. Inherits Name, Description,
    /// LibraryCode, and Status from Item, and adds the extra fields the
    /// assignment brief asks for on books specifically (author/genre).
    /// 
    public class Book : Item
    {
        /// The book's author.
        public string Author { get; set; } = string.Empty;

        /// The book's genre, selected from a fixed dropdown list in the UI.
        public string Genre { get; set; } = string.Empty;
    }
}
