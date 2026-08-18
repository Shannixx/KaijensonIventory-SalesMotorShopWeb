using System;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class ReportsPageViewModel
    {
        public ReportFilterViewModel Filter { get; set; } = new();
        public InventoryReportViewModel InventoryReport { get; set; } = new();
        public SalesPerformanceReportViewModel SalesPerformanceReport { get; set; } = new();
        public RevenueReportViewModel RevenueReport { get; set; } = new();
        public System.Collections.Generic.List<MostSoldProductViewModel> MostSoldProducts { get; set; } = new();
        public System.Collections.Generic.List<StockMovementViewModel> StockMovements { get; set; } = new();
        public System.Collections.Generic.List<SerialNumberReportViewModel> SerialNumberReport { get; set; } = new();
    }
}
