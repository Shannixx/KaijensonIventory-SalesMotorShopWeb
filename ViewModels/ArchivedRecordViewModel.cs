namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    // ControllerName and group keys are assigned only by ArchivesController's fixed sources.
    public sealed class ArchivedRecordViewModel
    {
        public int Id { get; init; }
        public string ControllerName { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? SecondaryText { get; init; }
        public DateTime? ArchivedAt { get; init; }
    }

    public sealed record ArchiveGroupViewModel(
        string Key,
        string Label,
        IReadOnlyList<ArchivedRecordViewModel> Records);

    public sealed class ArchivesIndexViewModel
    {
        public IReadOnlyList<ArchiveGroupViewModel> Groups { get; init; } = [];
        public int TotalCount => Groups.Sum(group => group.Records.Count);
    }
}
