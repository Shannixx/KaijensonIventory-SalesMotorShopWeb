using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public interface IReportService
    {
        Task<InventoryReportViewModel> GetInventoryReportAsync(DateTime start, DateTime end);
        Task<SalesPerformanceReportViewModel> GetSalesPerformanceReportAsync(DateTime start, DateTime end);
        Task<RevenueReportViewModel> GetRevenueReportAsync(DateTime start, DateTime end);
        Task<List<MostSoldProductViewModel>> GetMostSoldProductsAsync(DateTime start, DateTime end);
        Task<List<StockMovementViewModel>> GetStockMovementsAsync(DateTime start, DateTime end);
        Task<List<SerialNumberReportViewModel>> GetSerialNumberReportAsync(DateTime start, DateTime end);
        Task<decimal> GetTotalInventoryValueAsync(DateTime start, DateTime end, int? productId = null, int? categoryId = null);
        Task<int> GetLowStockItemCountAsync(DateTime start, DateTime end, int? productId = null, int? categoryId = null);
        Task<List<LowStockAlertViewModel>> GetLowStockAlertsAsync(DateTime start, DateTime end, int? productId = null, int? categoryId = null);
        Task<List<RevenueTrendItemViewModel>> GetRevenueTrendAsync(DateTime start, DateTime end, int? productId = null, int? categoryId = null);
        Task<List<SalesByCategoryViewModel>> GetSalesByCategoryAsync(DateTime start, DateTime end, int? productId = null, int? categoryId = null);
    }
}
