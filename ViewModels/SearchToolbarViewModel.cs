namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public sealed class SearchToolbarViewModel
    {
        public string ActionName { get; set; } = "Index";
        public string? ControllerName { get; set; }
        public string QueryParameterName { get; set; } = "searchString";
        public string? QueryValue { get; set; }
        public string Placeholder { get; set; } = "Search...";
        public string AriaLabel { get; set; } = "Search";
        public string ClearActionName { get; set; } = "Index";
        public string? ClearControllerName { get; set; }
        public string InputId { get; set; } = "pageSearchInput";
    }
}
