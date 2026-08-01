using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public interface IPurchaseOrderService
    {
        Task<PurchaseOrderListResult> GetPagedAsync(string? searchString, string? statusFilter, int page, int pageSize = 10);

        Task<PurchaseOrderViewModel> PrepareCreateViewModelAsync(PurchaseOrderViewModel? model = null);

        Task<PurchaseOrderViewModel?> PrepareEditViewModelAsync(int id);

        Task<PurchaseOrderViewModel> PrepareEditViewModelAsync(PurchaseOrderViewModel model);

        Task<PurchaseOrderViewModel?> GetDetailsViewModelAsync(int id);

        Task<Result> CreateAsync(PurchaseOrderViewModel model, int currentStaffId);

        Task<Result> UpdateAsync(PurchaseOrderViewModel model, int currentStaffId);

        Task<Result> ApproveAsync(int id, int currentStaffId);

        Task<Result> CancelAsync(int id, int currentStaffId);

        Task<Result> DeleteAsync(int id, int currentStaffId);

        Task<PurchaseOrder?> GetByIdAsync(int id);

        Task LogPrintAsync(int id, int currentStaffId);

        Task<SupplierInfoDto?> GetSupplierInfoAsync(int id);

        Task<List<ProductLookupDto>> GetProductsBySupplierAsync(int id);
    }

    public class PurchaseOrderListResult
    {
        public List<PurchaseOrder> Items { get; set; } = new();

        public int TotalCount { get; set; }

        public int TotalPages { get; set; }
    }

    public class SupplierInfoDto
    {
        public string? ContactPerson { get; set; }

        public string? ContactNumber { get; set; }

        public string? Address { get; set; }
    }

    public class ProductLookupDto
    {
        public int ProductId { get; set; }

        public string? ProductName { get; set; }

        public string? Brand { get; set; }
    }
}
