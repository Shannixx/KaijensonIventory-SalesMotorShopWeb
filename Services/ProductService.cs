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

        public async Task<ProductListResult> GetPagedAsync(string? searchString, int? categoryId, int page, int pageSize = 10, bool includeDeleted = false)
        {
            IQueryable<Product> query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .AsNoTracking();

            query = includeDeleted ? query.Where(p => p.IsDeleted) : query.Where(p => !p.IsDeleted);

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
                .OrderBy(p => p.ProductId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            List<SelectListItem> categories = await _context.Categories
                .AsNoTracking()
                .Where(c => !c.IsDeleted)
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
                .FirstOrDefaultAsync(p => p.ProductId == id && !p.IsDeleted);

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
                .AnyAsync(b => b.BrandName == model.Brand && !b.IsDeleted);
            if (!brandExists)
                return Result.Failure("Brand", "The selected brand is not valid or is inactive.");

            bool nameExists = await _context.Products.AnyAsync(p =>
                !p.IsDeleted &&
                p.ProductName == model.ProductName &&
                (p.Brand ?? "") == (model.Brand ?? "") &&
                p.SupplierId == model.SupplierId);
            if (nameExists)
                return Result.Failure("ProductName", "A product with this name already exists.");

            var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == model.SupplierId && !s.IsDeleted);
            if (supplier == null || supplier.Status != "Active")
                return Result.Failure("SupplierId", "The selected supplier is inactive and cannot be assigned to a new product.");

            bool activeCategory = await _context.Categories.AnyAsync(c => c.CategoryId == model.CategoryId && !c.IsDeleted);
            if (!activeCategory)
                return Result.Failure("CategoryId", "The selected category is not active.");

            if (model.PurchaseOrderId.HasValue)
            {
                bool poExists = await _context.PurchaseOrders.AnyAsync(p => p.PurchaseOrderId == model.PurchaseOrderId.Value && !p.IsDeleted);
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
                    Price = model.Price ?? 0m,
                    IsSerialized = model.IsSerialized,
                    AverageCost = 0,
                    ReorderLevel = model.ReorderLevel,
                    StockStatus = StockHelper.GetStockStatus(model.QuantityOnHand),
                    CreatedAt = DateTime.Now,
                    CreatedBy = currentStaffId,
                    LastUpdated = DateTime.Now
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
                .AnyAsync(b => b.BrandName == model.Brand && !b.IsDeleted);
            if (!brandExists)
                return Result.Failure("Brand", "The selected brand is not valid or is inactive.");

            bool nameExists = await _context.Products.AnyAsync(p =>
                !p.IsDeleted &&
                p.ProductName == model.ProductName &&
                (p.Brand ?? "") == (model.Brand ?? "") &&
                p.SupplierId == model.SupplierId &&
                p.ProductId != model.ProductId);
            if (nameExists)
                return Result.Failure("ProductName", "A product with this name already exists.");

            if (model.PurchaseOrderId.HasValue)
            {
                bool poExists = await _context.PurchaseOrders.AnyAsync(p => p.PurchaseOrderId == model.PurchaseOrderId.Value && !p.IsDeleted);
                if (!poExists)
                    return Result.Failure("PurchaseOrderId", "The selected purchase order is not valid.");
            }

            bool activeCategory = await _context.Categories.AnyAsync(c => c.CategoryId == model.CategoryId && !c.IsDeleted);
            if (!activeCategory)
                return Result.Failure("CategoryId", "The selected category is not active.");

            Product? existing = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == model.ProductId && !p.IsDeleted);
            if (existing == null)
                return Result.Failure(null, "The product could not be found.");

            existing.ProductName = model.ProductName;
            existing.Brand = model.Brand;
            existing.CategoryId = model.CategoryId;
            // Update supplier with validation
            var newSupplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == model.SupplierId && !s.IsDeleted);
            if (newSupplier == null || newSupplier.Status != "Active")
                return Result.Failure("SupplierId", "The selected supplier is inactive and cannot be assigned to the product.");
            existing.SupplierId = model.SupplierId;
            existing.QuantityOnHand = model.QuantityOnHand;
            existing.ModelCompatibility = model.ModelCompatibility;
            existing.Description = model.Description;
            existing.PurchaseOrderId = model.PurchaseOrderId;
            
                existing.IsSerialized = model.IsSerialized;
            existing.Price = model.Price ?? existing.Price;
                existing.ReorderLevel = model.ReorderLevel;
            existing.StockStatus = StockHelper.GetStockStatus(existing.QuantityOnHand);
            existing.LastUpdated = DateTime.Now;

            await _context.SaveChangesAsync();

            // Notification evaluation after a valid stock edit
            int newQty = existing.QuantityOnHand;

            if (newQty <= 0)
                await _notificationService.CreateOnceAsync(existing.ProductId, "OutOfStock",
                    $"{existing.ProductName} is out of stock.", currentStaffId);
            else
                await _notificationService.ResolveUnreadAsync(existing.ProductId, "OutOfStock", currentStaffId);

            if (newQty > 0 && newQty < StockHelper.LowStockThreshold)
                await _notificationService.CreateOnceAsync(existing.ProductId, "LowStock",
                    $"Low stock for {existing.ProductName} (Qty {newQty}).", currentStaffId);
            else if (newQty >= StockHelper.LowStockThreshold)
                await _notificationService.ResolveUnreadAsync(existing.ProductId, "LowStock", currentStaffId);

            if (newQty <= existing.ReorderLevel)
                await _notificationService.CreateOnceAsync(existing.ProductId, "Reorder",
                    $"{existing.ProductName} reached reorder level. Qty: {newQty}.", currentStaffId);
            else
                await _notificationService.ResolveUnreadAsync(existing.ProductId, "Reorder", currentStaffId);

            await _activityLogService.LogAsync("Edit Product", "Product",
                $"Product {existing.ProductName} - Qty: {existing.QuantityOnHand}, Price: {existing.Price}",
                currentStaffId);

            return Result.Success();
        }

        public async Task<Result> DeleteAsync(int id, int currentStaffId)
        {
            Product? product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == id && !p.IsDeleted);
            if (product == null)
                return Result.Failure(null, "The product could not be found or is already archived.");

            if (product.QuantityOnHand > 0)
                return Result.Failure(null, "A product with stock on hand cannot be archived. Reduce stock through an audited inventory operation first.");

            bool hasAvailableSerial = await _context.SerialUnits.AnyAsync(s =>
                s.ProductId == id && s.Status == "Available");
            if (hasAvailableSerial)
                return Result.Failure(null, "A product with available serialized units cannot be archived.");

            product.IsDeleted = true;
            product.DeletedAt = DateTime.UtcNow;
            product.DeletedBy = currentStaffId;
            await _activityLogService.LogAsync("Archive Product", "Product",
                $"Product {product.ProductName} ({product.ProductId}) archived",
                currentStaffId);
            await _context.SaveChangesAsync();

            return Result.Success();
        }

        public async Task<Result> RestoreAsync(int id, int currentStaffId)
        {
            Product? product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == id && p.IsDeleted);
            if (product == null)
                return Result.Failure(null, "The archived product could not be found.");

            bool activeCategory = await _context.Categories.AnyAsync(c => c.CategoryId == product.CategoryId && !c.IsDeleted);
            bool activeSupplier = await _context.Suppliers.AnyAsync(s => s.SupplierId == product.SupplierId && !s.IsDeleted && s.Status == "Active");
            bool activeBrand = string.IsNullOrWhiteSpace(product.Brand) || await _context.Brands.AnyAsync(b => b.BrandName == product.Brand && !b.IsDeleted);
            if (!activeCategory || !activeSupplier || !activeBrand)
                return Result.Failure(null, "The product cannot be restored until its category, brand, and supplier are active.");

            product.IsDeleted = false;
            product.DeletedAt = null;
            product.DeletedBy = null;
            await _activityLogService.LogAsync("Restore Product", "Product",
                $"Product {product.ProductName} ({product.ProductId}) restored",
                currentStaffId);
            await _context.SaveChangesAsync();
            return Result.Success();
        }

        public async Task<Product?> GetByIdAsync(int id, bool includeDeleted = false)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Include(p => p.CreatedByStaff)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductId == id && (includeDeleted || !p.IsDeleted));
        }

        private async Task<T> PopulateListsAsync<T>(T model) where T : ProductCreateViewModel
        {
            model.Categories = await _context.Categories.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.CategoryName)
                .Select(c => new SelectListItem { Value = c.CategoryId.ToString(), Text = c.CategoryName })
                .ToListAsync();

            model.Suppliers = await _context.Suppliers.AsNoTracking()
                    .Where(s => !s.IsDeleted && (s.Status == "Active" || s.SupplierId == model.SupplierId))
                    .OrderBy(s => s.CompanyName)
                    .Select(s => new SelectListItem { Value = s.SupplierId.ToString(), Text = s.CompanyName })
                    .ToListAsync();

            model.Brands = await _context.Brands.AsNoTracking()
                .Where(b => !b.IsDeleted)
                .OrderBy(b => b.BrandName)
                .ThenBy(b => b.BrandName)
                .Select(b => new SelectListItem { Value = b.BrandName, Text = b.BrandName })
                .ToListAsync();

            model.PurchaseOrders = await _context.PurchaseOrders.AsNoTracking()
                .Where(p => !p.IsDeleted)
                .OrderByDescending(p => p.CreatedDate)
                .Select(p => new SelectListItem { Value = p.PurchaseOrderId.ToString(), Text = p.PurchaseOrderNumber })
                .ToListAsync();

            return model;
        }

        private async Task<ProductEditViewModel> PopulateEditListsAsync(ProductEditViewModel model)
        {
            model.Categories = await _context.Categories.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.CategoryName)
                .Select(c => new SelectListItem { Value = c.CategoryId.ToString(), Text = c.CategoryName })
                .ToListAsync();

            model.Suppliers = await _context.Suppliers.AsNoTracking()
                    .Where(s => !s.IsDeleted && (s.Status == "Active" || s.SupplierId == model.SupplierId))
                    .OrderBy(s => s.CompanyName)
                    .Select(s => new SelectListItem { Value = s.SupplierId.ToString(), Text = s.CompanyName })
                    .ToListAsync();

            model.Brands = await _context.Brands.AsNoTracking()
                .Where(b => !b.IsDeleted)
                .OrderBy(b => b.BrandName)
                .ThenBy(b => b.BrandName)
                .Select(b => new SelectListItem { Value = b.BrandName, Text = b.BrandName })
                .ToListAsync();

            model.PurchaseOrders = await _context.PurchaseOrders.AsNoTracking()
                .Where(p => !p.IsDeleted)
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
