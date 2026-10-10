namespace LibraryManagementSystem.Models;

public sealed class BranchSelectorViewModel
{
    public IReadOnlyList<Branch> Branches { get; init; } = Array.Empty<Branch>();
    public string? SelectedCode { get; init; }
}
