namespace LibraryManagementSystem.Models
{
    public class Book : Item
    {
        public string Author { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;

        // Added for the F6 CSV importer feature.
        public string Isbn { get; set; } = string.Empty;
    }
}