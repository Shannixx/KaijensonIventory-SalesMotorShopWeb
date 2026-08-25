using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.Services;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public class ProductService : IProductService
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityLogService _activityLogService;
        private readonly INotificationService _notificationService;

        public ProductService(ApplicationDbContext context,
                              IActivityLogService activityLogService,
                              INotificationService notificationService)
        {
            _context = context;
            _activityLogService = activityLogService;
            _notificationService = notificationService;
        }

        public async Task<ProductListResult> GetPagedAsync(string? searchString, int? categoryId, int page, int pageSize = 10)
        {
            IQueryable<Product> query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                string s = searchString.ToLower();
                query = query.Where(p => p.ProductName.ToLower().Contains(s)
                    || (p.Description != null && p.Description.ToLower().Contains(s))
                    || (p.Brand != null && p.Brand.ToLower().Contains(s)));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            int total = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(total / (double)pageSize);

            List<Product> items = await query
                .OrderBy(p => p.ProductName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            List<SelectListItem> categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.CategoryName)
                .Select(c => new SelectListItem { Value = c.CategoryId.ToString(), Text = c.CategoryName })
                .ToListAsync();

            return new ProductListResult
            {
                Items = items,
                TotalCount = total,
                TotalPages = totalPages,
                Categories = categories
            };
        }

        public async Task<ProductCreateViewModel> PrepareCreateViewModelAsync(ProductCreateViewModel? model = null)
        {
            model ??= new ProductCreateViewModel();
            return await PopulateListsAsync(model);
        }

        public async Task<ProductEditViewModel?> PrepareEditViewModelAsync(int id)
        {
            Product? product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null) return null;

            var model = new ProductEditViewModel
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                Brand = product.Brand,
                CategoryId = product.CategoryId,
                SupplierId = product.SupplierId,
                QuantityOnHand = product.QuantityOnHand,
                Description = product.Description,
                ModelCompatibility = product.ModelCompatibility,
                PurchaseOrderId = product.PurchaseOrderId,
                Price = product.Price,
                ReorderLevel = product.ReorderLevel,
                IsSerialized = product.IsSerialized
            };

            return await PopulateEditListsAsync(model);
        }

        public async Task<ProductEditViewModel> PrepareEditViewModelAsync(ProductEditViewModel model)
        {
            return await PopulateEditListsAsync(model);
        }

        public async Task<Result> CreateAsync(ProductCreateViewModel model, int currentStaffId)
        {
            var errors = Validate(model);
            if (errors.Any())
                return Result.Failure(errors);

            bool brandExists = await _context.Brands
                .AnyAsync(b => b.BrandName == model.Brand && b.Status == "Active");
            if (!brandExists)
                return Result.Failure("Brand", "The selected brand is not valid or is inactive.");

            bool nameExists = await _context.Products.AnyAsync(p =>
                p.ProductName == model.ProductName &&
                (p.Brand ?? "") == (model.Brand ?? "") &&
                p.SupplierId == model.SupplierId);
            if (nameExists)
                return Result.Failure("ProductName", "A product with this name already exists.");

            if (model.PurchaseOrderId.HasValue)
            {
                bool poExists = await _context.PurchaseOrders.AnyAsync(p => p.PurchaseOrderId == model.PurchaseOrderId.Value);
                if (!poExists)
                    return Result.Failure("PurchaseOrderId", "The selected purchase order is not valid.");
            }

            var product = new Product
            {
                ProductName = model.ProductName,
                Brand = model.Brand,
                CategoryId = model.CategoryId,
                SupplierId = model.SupplierId,
                QuantityOnHand = model.QuantityOnHand,
                Description = model.Description,
                ModelCompatibility = model.ModelCompatibility,
                PurchaseOrderId = model.PurchaseOrderId,
                LeadTimeDays = 30,
                Price = model.Price.Value,
                IsSerialized = model.IsSerialized,
                AverageCost = 0,
                ReorderLevel = model.ReorderLevel,
                StockStatus = StockHelper.GetStockStatus(model.QuantityOnHand),
                CreatedAt = DateTime.Now
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            await _activityLogService.LogAsync("Create Product", "Product",
                $"Product {product.ProductName} - Qty: {product.QuantityOnHand}, Price: {product.Price}",
                currentStaffId);

            return Result.Success();
        }

        public async Task<Result> UpdateAsync(ProductEditViewModel model, int currentStaffId)
        {
            var errors = ValidateEdit(model);
            if (errors.Any())
                return Result.Failure(errors);

            bool brandExists = await _context.Brands
                .AnyAsync(b => b.BrandName == model.Brand && b.Status == "Active");
            if (!brandExists)
                return Result.Failure("Brand", "The selected brand is not valid or is inactive.");

            bool nameExists = await _context.Products.AnyAsync(p =>
                p.ProductName == model.ProductName &&
                (p.Brand ?? "") == (model.Brand ?? "") &&
                p.SupplierId == model.SupplierId &&
                p.ProductId != model.ProductId);
            if (nameExists)
                return Result.Failure("ProductName", "A product with this name already exists.");

            if (model.PurchaseOrderId.HasValue)
            {
                bool poExists = await _context.PurchaseOrders.AnyAsync(p => p.PurchaseOrderId == model.PurchaseOrderId.Value);
                if (!poExists)
                    return Result.Failure("PurchaseOrderId", "The selected purchase order is not valid.");
            }

            Product? existing = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == model.ProductId);
            if (existing == null)
                return Result.Failure(null, "The product could not be found.");

            existing.ProductName = model.ProductName;
            existing.Brand = model.Brand;
            existing.CategoryId = model.CategoryId;
            existing.SupplierId = model.SupplierId;
            existing.QuantityOnHand = model.QuantityOnHand;
            existing.ModelCompatibility = model.ModelCompatibility;
            existing.Description = model.Description;
            existing.PurchaseOrderId = model.PurchaseOrderId;
            
                existing.IsSerialized = model.IsSerialized;
            existing.Price = model.Price ?? existing.Price;
                existing.ReorderLevel = model.ReorderLevel;
            existing.StockStatus = StockHelper.GetStockStatus(existing.QuantityOnHand);

            await _context.SaveChangesAsync();

            // Notification evaluation after a valid stock edit
            int newQty = existing.QuantityOnHand;

            if (newQty <= 0)
                await _notificationService.CreateOnceAsync(existing.ProductId, "OutOfStock",
                    $"{existing.ProductName} is out of stock.");
            else
                await _notificationService.ResolveUnreadAsync(existing.ProductId, "OutOfStock");

            if (newQty > 0 && newQty < StockHelper.LowStockThreshold)
                await _notificationService.CreateOnceAsync(existing.ProductId, "LowStock",
                    $"Low stock for {existing.ProductName} (Qty {newQty}).");
            else if (newQty >= StockHelper.LowStockThreshold)
                await _notificationService.ResolveUnreadAsync(existing.ProductId, "LowStock");

            if (newQty <= existing.ReorderLevel)
                await _notificationService.CreateOnceAsync(existing.ProductId, "Reorder",
                    $"{existing.ProductName} reached reorder level. Qty: {newQty}.");
            else
                await _notificationService.ResolveUnreadAsync(existing.ProductId, "Reorder");

            await _activityLogService.LogAsync("Edit Product", "Product",
                $"Product {existing.ProductName} - Qty: {existing.QuantityOnHand}, Price: {existing.Price}",
                currentStaffId);

            return Result.Success();
        }

        public async Task<Result> DeleteAsync(int id, int currentStaffId)
        {
            Product? product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == id);
            if (product == null)
                return Result.Failure(null, "The product could not be found.");

            _context.Products.Remove(product);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Result.Failure(null, "Cannot delete this product because it is referenced by existing records.");
            }

            await _activityLogService.LogAsync("Delete Product", "Product",
                $"Product {product.ProductName} deleted",
                currentStaffId);

            return Result.Success();
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductId == id);
        }

        private async Task<T> PopulateListsAsync<T>(T model) where T : ProductCreateViewModel
        {
            model.Categories = await _context.Categories.AsNoTracking().OrderBy(c => c.CategoryName)
                .Select(c => new SelectListItem { Value = c.CategoryId.ToString(), Text = c.CategoryName })
                .ToListAsync();

            model.Suppliers = await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName)
                .Select(s => new SelectListItem { Value = s.SupplierId.ToString(), Text = s.CompanyName })
                .ToListAsync();

            model.Brands = await _context.Brands.AsNoTracking()
                .OrderBy(b => b.Status == "Active" ? 0 : 1)
                .ThenBy(b => b.BrandName)
                .Select(b => new SelectListItem { Value = b.BrandName, Text = b.BrandName })
                .ToListAsync();

            model.PurchaseOrders = await _context.PurchaseOrders.AsNoTracking()
                .OrderByDescending(p => p.CreatedDate)
                .Select(p => new SelectListItem { Value = p.PurchaseOrderId.ToString(), Text = p.PurchaseOrderNumber })
                .ToListAsync();

            return model;
        }

        private async Task<ProductEditViewModel> PopulateEditListsAsync(ProductEditViewModel model)
        {
            model.Categories = await _context.Categories.AsNoTracking().OrderBy(c => c.CategoryName)
                .Select(c => new SelectListItem { Value = c.CategoryId.ToString(), Text = c.CategoryName })
                .ToListAsync();

            model.Suppliers = await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName)
                .Select(s => new SelectListItem { Value = s.SupplierId.ToString(), Text = s.CompanyName })
                .ToListAsync();

            model.Brands = await _context.Brands.AsNoTracking()
                .OrderBy(b => b.Status == "Active" ? 0 : 1)
                .ThenBy(b => b.BrandName)
                .Select(b => new SelectListItem { Value = b.BrandName, Text = b.BrandName })
                .ToListAsync();

            model.PurchaseOrders = await _context.PurchaseOrders.AsNoTracking()
                .OrderByDescending(p => p.CreatedDate)
                .Select(p => new SelectListItem { Value = p.PurchaseOrderId.ToString(), Text = p.PurchaseOrderNumber })
                .ToListAsync();

            return model;
        }

        private static List<ResultError> Validate(ProductCreateViewModel model)
        {
            var errors = new List<ResultError>();

            if (string.IsNullOrWhiteSpace(model.ProductName))
                errors.Add(new ResultError("ProductName", "Product name is required."));

            if (string.IsNullOrWhiteSpace(model.Brand))
                errors.Add(new ResultError("Brand", "Please select a brand."));

            if (model.CategoryId <= 0)
                errors.Add(new ResultError("CategoryId", "Please select a category."));

            if (model.SupplierId <= 0)
                errors.Add(new ResultError("SupplierId", "Please select a supplier."));

            if (model.QuantityOnHand < 0)
                errors.Add(new ResultError("QuantityOnHand", "Quantity cannot be negative."));

            return errors;
        }

        private static List<ResultError> ValidateEdit(ProductEditViewModel model)
        {
            var errors = new List<ResultError>();

            if (string.IsNullOrWhiteSpace(model.ProductName))
                errors.Add(new ResultError("ProductName", "Product name is required."));

            if (string.IsNullOrWhiteSpace(model.Brand))
                errors.Add(new ResultError("Brand", "Please select a brand."));

            if (model.CategoryId <= 0)
                errors.Add(new ResultError("CategoryId", "Please select a category."));

            if (model.SupplierId <= 0)
                errors.Add(new ResultError("SupplierId", "Please select a supplier."));

            if (model.QuantityOnHand < 0)
                errors.Add(new ResultError("QuantityOnHand", "Quantity cannot be negative."));

            return errors;
        }

        private static string CalculateStockStatus(int qty)
        {
            return StockHelper.GetStockStatus(qty);
        }
    }
}
