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
        public int OutOfStockCount { get; set; }
        public int TodaySalesCount { get; set; }
        public decimal TodaySalesAmount { get; set; }
        public decimal TodayProfit { get; set; }
        public decimal TotalInventoryValue { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCOGS { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal ProfitMargin { get; set; }
        public List<Product> RecentLowStockProducts { get; set; } = new();
        public List<SalesTransaction> RecentSales { get; set; } = new();
        public List<ServiceTransaction> OngoingServices { get; set; } = new();
        public List<string> ChartLabels { get; set; } = new();
        public List<decimal> ChartSalesData { get; set; } = new();
        public List<decimal> ChartProfitData { get; set; } = new();
        public List<string> CategoryLabels { get; set; } = new();
        public List<int> CategoryCounts { get; set; } = new();
        public List<ProductSalesInfo> TopSellingProducts { get; set; } = new();
        public int TotalCustomers { get; set; }
        public int WalkInCount { get; set; }
        public int RegisteredCustomers { get; set; }
        public decimal AveragePurchasePerCustomer { get; set; }
        public List<CustomerSummaryInfo> TopCustomers { get; set; } = new();
        public List<CustomerSummaryInfo> RecentCustomers { get; set; } = new();
        public int LowStockRequireReorder { get; set; }
        public int PendingPurchaseOrders { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public decimal MonthlyProfit { get; set; }
        public List<StockIn> RecentStockIns { get; set; } = new();
        public List<Notification> ActiveAlerts { get; set; } = new();
    }

    public class ProductSalesInfo
    {
        public string ProductName { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class CustomerSummaryInfo
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? ContactNumber { get; set; }
        public decimal TotalPurchases { get; set; }
        public DateTime? LastPurchaseDate { get; set; }
        public int TransactionCount { get; set; }
    }
}
