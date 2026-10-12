namespace LibraryManagementSystem.Models
{
    public enum ItemStatus
    {
        Available,
        Borrowed,
        Reserved,
        Maintenance,
        Lost,
        InTransit,
        Damaged
    }

    public static class ItemStatusExtensions
    {
        public static ItemStatus ToItemStatus(this string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return ItemStatus.Available;

            return Enum.TryParse<ItemStatus>(status, true, out var result)
                ? result
                : ItemStatus.Available;
        }
    }
}