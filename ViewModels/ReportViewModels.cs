using System;
using System.Collections.Generic;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class ReportFilterViewModel
    {
        public DateTime StartDate { get; set; } = DateTime.Today.AddMonths(-1);
        public DateTime EndDate { get; set; } = DateTime.Today;
        public int? ProductId { get; set; }
        public int? CategoryId { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
    }

    public class InventoryReportItemViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int QuantityOnHand { get; set; }
        public string StockStatus { get; set; } = string.Empty;
        public int ReorderLevel { get; set; }
    }

    public class InventoryReportViewModel
    {
        public List<InventoryReportItemViewModel> Items { get; set; } = new();
    }

    public class SalesPerformanceReportViewModel
    {
        public int TransactionCount { get; set; }
        public int TotalQuantitySold { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class RevenueReportViewModel
    {
        public decimal TotalRevenue { get; set; }
    }

    public class MostSoldProductViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class StockMovementViewModel
    {
        public DateTime Date { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string MovementType { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Reference { get; set; } = string.Empty;
    }

    public class SerialNumberReportViewModel
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? SaleId { get; set; }
        public DateTime? SaleDate { get; set; }
    }
}
