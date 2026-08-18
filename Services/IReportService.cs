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
    }
}
