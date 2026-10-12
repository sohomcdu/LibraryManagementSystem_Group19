namespace LibraryManagementSystem.Models;

public sealed class ManagerWorkloadViewModel
{
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public IReadOnlyList<WorkloadActivitySummary> Activity { get; init; } = [];
    public IReadOnlyList<WorkloadBacklogItem> Backlog { get; init; } = [];
}

public sealed class WorkloadActivitySummary
{
    public string Activity { get; init; } = string.Empty;
    public int Total { get; init; }
    public int Unresolved { get; init; }
}

public sealed class WorkloadBacklogItem
{
    public string Type { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public int AgeDays { get; init; }
}
