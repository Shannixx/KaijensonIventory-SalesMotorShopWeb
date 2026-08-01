using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public interface IProductService
    {
        Task<ProductListResult> GetPagedAsync(string? searchString, int? categoryId, int page, int pageSize = 10);

        Task<ProductCreateViewModel> PrepareCreateViewModelAsync(ProductCreateViewModel? model = null);

        Task<ProductEditViewModel?> PrepareEditViewModelAsync(int id);

        Task<ProductEditViewModel> PrepareEditViewModelAsync(ProductEditViewModel model);

        Task<Result> CreateAsync(ProductCreateViewModel model, int currentStaffId);

        Task<Result> UpdateAsync(ProductEditViewModel model, int currentStaffId);

        Task<Result> DeleteAsync(int id, int currentStaffId);

        Task<Product?> GetByIdAsync(int id);
    }

    public class ProductListResult
    {
        public List<Product> Items { get; set; } = new();

        public int TotalCount { get; set; }

        public int TotalPages { get; set; }

        public List<SelectListItem> Categories { get; set; } = new();
    }
}
