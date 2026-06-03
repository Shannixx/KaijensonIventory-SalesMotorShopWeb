using KaijensonIventory_SalesMotorShopWeb.Models;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalProducts { get; set; }
        public int TotalCategories { get; set; }
        public int TotalSuppliers { get; set; }
        public int TotalMechanics { get; set; }
        public int LowStockCount { get; set; }
        public decimal TodaySalesAmount { get; set; }
        public decimal TotalInventoryValue { get; set; }
        public List<Product> RecentLowStockProducts { get; set; } = new();
        public List<SalesTransaction> RecentSales { get; set; } = new();
        public List<ServiceTransaction> OngoingServices { get; set; } = new();
        public List<string> ChartLabels { get; set; } = new();
        public List<decimal> ChartSalesData { get; set; } = new();
        public List<string> CategoryLabels { get; set; } = new();
        public List<int> CategoryCounts { get; set; } = new();
    }
}
