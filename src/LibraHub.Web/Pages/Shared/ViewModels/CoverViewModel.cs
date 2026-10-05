namespace LibraHub.Pages.Shared.ViewModels
{
    public class CoverViewModel
    {
        public string? Colors { get; set; }
        public string Size { get; set; } = "";
        public string? Category { get; set; }

        public CoverViewModel() { }
        public CoverViewModel(string? colors, string size, string? category = null)
        {
            Colors = colors;
            // ensure a non-null, non-empty Size
            Size = string.IsNullOrEmpty(size) ? "mid" : size;
            Category = category;
        }
    }
}
