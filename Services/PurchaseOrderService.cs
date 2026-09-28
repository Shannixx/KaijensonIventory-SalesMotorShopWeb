using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public class PurchaseOrderService : IPurchaseOrderService
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityLogService _activityLogService;

        public PurchaseOrderService(ApplicationDbContext context, IActivityLogService activityLogService)
        {
            _context = context;
            _activityLogService = activityLogService;
        }

        public async Task<PurchaseOrderListResult> GetPagedAsync(string? searchString, string? statusFilter, int page, int pageSize = 10, bool includeDeleted = false)
        {
            IQueryable<PurchaseOrder> query = _context.PurchaseOrders
                .Include(p => p.Supplier)
                .Include(p => p.Staff)
                .AsNoTracking()
                .Where(p => includeDeleted ? p.IsDeleted : !p.IsDeleted);

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                string s = searchString.ToLower();
                query = query.Where(p =>
                    p.PurchaseOrderNumber.ToLower().Contains(s) ||
                    p.Supplier!.CompanyName.ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter) &&
                (statusFilter == "Pending" || statusFilter == "Approved" ||
                 statusFilter == "Delivered" || statusFilter == "Cancelled"))
            {
                query = query.Where(p => p.Status == statusFilter);
            }

            int total = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(total / (double)pageSize);

            List<PurchaseOrder> items = await query
                .OrderByDescending(p => p.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PurchaseOrderListResult
            {
                Items = items,
                TotalCount = total,
                TotalPages = totalPages
            };
        }

        public async Task<PurchaseOrderViewModel> PrepareCreateViewModelAsync(PurchaseOrderViewModel? model = null)
        {
            model ??= new PurchaseOrderViewModel();
            return await PopulateListsAsync(model);
        }

        public async Task<PurchaseOrderViewModel?> PrepareEditViewModelAsync(int id)
        {
            PurchaseOrder? order = await _context.PurchaseOrders
                .Where(p => !p.IsDeleted)
                .Include(p => p.Supplier)
                .Include(p => p.Items.Where(i => !i.IsDeleted)).ThenInclude(i => i.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PurchaseOrderId == id);

            if (order == null) return null;

            var viewModel = new PurchaseOrderViewModel
            {
                PurchaseOrderId = order.PurchaseOrderId,
                PurchaseOrderNumber = order.PurchaseOrderNumber,
                SupplierId = order.SupplierId,
                 OrderDate = order.OrderDate,
                 ExpectedDeliveryDate = order.ExpectedDeliveryDate,
                 
                 Status = order.Status,
                TotalAmount = order.TotalAmount,
                Remarks = order.Remarks,
                Items = order.Items.Where(i => !i.IsDeleted).Select(i => new PurchaseOrderItemViewModel
                {
                    PurchaseOrderItemId = i.PurchaseOrderItemId,
                    ProductId = i.ProductId,
                    ProductName = i.Product?.ProductName,
                    Brand = i.Product?.Brand,
                    CurrentStock = i.Product?.QuantityOnHand ?? 0,
                    Quantity = i.Quantity,
                    Price = i.Price,
                    Subtotal = i.Subtotal
                }).ToList()
            };

            // Populate active suppliers only for operational edits.
            viewModel.Suppliers = await _context.Suppliers.AsNoTracking()
                .Where(s => !s.IsDeleted && s.Status == "Active")
                .OrderBy(s => s.CompanyName)
                .Select(s => new SelectListItem { Value = s.SupplierId.ToString(), Text = s.CompanyName })
                .ToListAsync();
            return viewModel;
        }

        public async Task<PurchaseOrderViewModel> PrepareEditViewModelAsync(PurchaseOrderViewModel model)
        {
            return await PopulateListsAsync(model);
        }

        public async Task<PurchaseOrderViewModel?> GetDetailsViewModelAsync(int id, bool includeDeleted = false)
        {
            PurchaseOrder? order = await _context.PurchaseOrders
                .Where(p => includeDeleted || !p.IsDeleted)
                .Include(p => p.Supplier)
                .Include(p => p.Staff)
                .Include(p => p.Items.Where(i => !i.IsDeleted)).ThenInclude(i => i.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PurchaseOrderId == id);

            if (order == null) return null;

            return new PurchaseOrderViewModel
            {
                PurchaseOrderId = order.PurchaseOrderId,
                PurchaseOrderNumber = order.PurchaseOrderNumber,
                SupplierId = order.SupplierId,
                SupplierName = order.Supplier?.CompanyName,
                ContactPerson = order.Supplier?.ContactPerson,
                ContactNumber = order.Supplier?.ContactNumber,
                SupplierAddress = order.Supplier?.Address,
                 OrderDate = order.OrderDate,
                 ExpectedDeliveryDate = order.ExpectedDeliveryDate,

                 Status = order.Status,
                TotalAmount = order.TotalAmount,
                Remarks = order.Remarks,
                CreatedByName = order.Staff?.StaffName,
                CreatedDate = order.CreatedDate,
                UpdatedDate = order.UpdatedDate,
                Items = order.Items.Where(i => !i.IsDeleted).Select(i => new PurchaseOrderItemViewModel
                {
                    PurchaseOrderItemId = i.PurchaseOrderItemId,
                    ProductId = i.ProductId,
                    ProductName = i.Product?.ProductName,
                    Brand = i.Product?.Brand,
                    CurrentStock = i.Product?.QuantityOnHand ?? 0,
                    Quantity = i.Quantity,
                    Price = i.Price,
                    Subtotal = i.Subtotal
                }).ToList()
            };
        }

        public async Task<Result> CreateAsync(PurchaseOrderViewModel model, int currentStaffId)
        {
            var validation = await ValidateItemsAsync(model);
            if (!validation.Succeeded)
                return validation;

            string poNumber = await GeneratePONumberAsync();

            var order = new PurchaseOrder
            {
                PurchaseOrderNumber = poNumber,
                 SupplierId = model.SupplierId,
                 OrderDate = model.OrderDate,
                 ExpectedDeliveryDate = model.ExpectedDeliveryDate,
                 Status = "Pending",
                 Remarks = model.Remarks,
                CreatedBy = currentStaffId,
                CreatedDate = DateTime.Now
            };

            foreach (var item in model.Items.Where(i => i.ProductId > 0))
            {
                var subtotal = item.Quantity * item.Price;
                order.Items.Add(new PurchaseOrderItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    Price = item.Price,
                    Subtotal = subtotal
                });
            }
            // Calculate total amount
            order.TotalAmount = order.Items.Sum(i => i.Subtotal);

            await using var transaction = await _context.Database.BeginTransactionAsync();

            _context.PurchaseOrders.Add(order);
            await _context.SaveChangesAsync();

            // Create pending delivery record linked to the newly created PO
            var delivery = new Delivery
            {
                PurchaseOrderId = order.PurchaseOrderId,
                Status = "Pending",
                CreatedDate = DateTime.Now
            };
            _context.Deliveries.Add(delivery);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            await _activityLogService.LogAsync("Create Purchase Order", "PurchaseOrder",
                $"Created PO {poNumber}", currentStaffId);

            return Result.Success();
        }

        public async Task<Result> UpdateAsync(PurchaseOrderViewModel model, int currentStaffId)
        {
            // Load existing order first to allow supplier‑change rules.
            PurchaseOrder? order = await _context.PurchaseOrders
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.PurchaseOrderId == model.PurchaseOrderId);

            if (order == null || order.IsDeleted)
                return Result.Failure(null, "The purchase order could not be found or is archived.");

            if (order.Status == "Delivered" || order.Status == "Cancelled")
                return Result.Failure(null, "Cannot edit a purchase order that has been delivered or cancelled.");

            // Validate supplier change according to business rules.
            if (model.SupplierId <= 0)
                return Result.Failure("SupplierId", "Please select a supplier.");

            var selectedSupplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == model.SupplierId && !s.IsDeleted);
            if (selectedSupplier == null || selectedSupplier.Status != "Active")
                return Result.Failure("SupplierId", "The selected supplier is inactive and cannot be used for a purchase order.");

            // Validate items (active product existence, selected-supplier ownership, no duplicates, quantity >0).
            if (model.Items == null || model.Items.Count == 0 || model.Items.All(i => i.ProductId <= 0))
                return Result.Failure("Items", "Please add at least one product.");

            var validItems = model.Items.Where(i => i.ProductId > 0).ToList();
            foreach (var item in validItems)
            {
                if (item.Quantity <= 0)
                    return Result.Failure($"Items[{validItems.IndexOf(item)}].Quantity", "Quantity must be greater than 0.");
            }

            var duplicateIds = validItems.GroupBy(i => i.ProductId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateIds.Any())
                return Result.Failure("Items", "Duplicate products are not allowed.");

            var productIds = validItems.Select(i => i.ProductId).Distinct().ToList();
            var selectedProducts = await _context.Products
                .Where(p => productIds.Contains(p.ProductId) && !p.IsDeleted)
                .Select(p => new { p.ProductId, p.SupplierId })
                .ToListAsync();
            if (selectedProducts.Count != productIds.Count)
                return Result.Failure("Items", "One or more selected products are not valid.");
            if (selectedProducts.Any(p => p.SupplierId != model.SupplierId))
                return Result.Failure("Items", "All products must belong to the selected supplier.");

            // Apply updates.
             order.SupplierId = model.SupplierId;
             order.OrderDate = model.OrderDate;
             order.ExpectedDeliveryDate = model.ExpectedDeliveryDate;
             order.Remarks = model.Remarks;
            order.UpdatedDate = DateTime.Now;

            bool hasReceivedItems = order.Items.Any(i => i.ReceivedQuantity > 0);
            if (hasReceivedItems)
                return Result.Failure(null, "Purchase orders with received quantities cannot have their items replaced.");

            foreach (var existingItem in order.Items)
            {
                existingItem.IsDeleted = true;
                existingItem.DeletedAt = DateTime.UtcNow;
                existingItem.DeletedBy = currentStaffId;
            }

            foreach (var item in validItems)
            {
                var subtotal = item.Quantity * item.Price;
                order.Items.Add(new PurchaseOrderItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    Price = item.Price,
                    Subtotal = subtotal
                });
            }
            // Recalculate total amount from active submitted lines only.
            order.TotalAmount = validItems.Sum(i => i.Quantity * i.Price);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await _activityLogService.LogAsync("Edit Purchase Order", "PurchaseOrder",
                $"Edited PO {order.PurchaseOrderNumber}", currentStaffId);

            return Result.Success();
        }





        public async Task<Result> DeleteAsync(int id, int currentStaffId)
        {
            PurchaseOrder? order = await _context.PurchaseOrders
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.PurchaseOrderId == id && !p.IsDeleted);

            if (order == null)
                return Result.Failure(null, "The purchase order could not be found or is already archived.");

            string poNumber = order.PurchaseOrderNumber;
            order.IsDeleted = true;
            order.DeletedAt = DateTime.UtcNow;
            order.DeletedBy = currentStaffId;

            var delivery = await _context.Deliveries
                .FirstOrDefaultAsync(d => d.PurchaseOrderId == id && !d.IsDeleted);
            if (delivery != null)
            {
                delivery.IsDeleted = true;
                delivery.DeletedAt = DateTime.UtcNow;
                delivery.DeletedBy = currentStaffId;
            }

            await _activityLogService.LogAsync("Archive Purchase Order", "PurchaseOrder",
                $"Archived PO {poNumber} ({order.PurchaseOrderId})", currentStaffId);
            await _context.SaveChangesAsync();
            return Result.Success();
        }

        public async Task<Result> RestoreAsync(int id, int currentStaffId)
        {
            PurchaseOrder? order = await _context.PurchaseOrders
                .Include(p => p.Items)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.PurchaseOrderId == id && p.IsDeleted);
            if (order == null)
                return Result.Failure(null, "The archived purchase order could not be found.");
            if (order.Supplier == null || order.Supplier.IsDeleted || order.Supplier.Status != "Active")
                return Result.Failure(null, "The purchase order cannot be restored until its supplier is active.");

            var archivedProducts = await _context.PurchaseOrderItems
                .Where(i => i.PurchaseOrderId == id && !i.IsDeleted)
                .Join(_context.Products, i => i.ProductId, p => p.ProductId, (i, p) => p)
                .Where(p => p.IsDeleted)
                .Select(p => p.ProductName)
                .ToListAsync();
            if (archivedProducts.Count > 0)
                return Result.Failure(null, "The purchase order cannot be restored while one or more products are archived.");

            order.IsDeleted = false;
            order.DeletedAt = null;
            order.DeletedBy = null;
            var delivery = await _context.Deliveries.FirstOrDefaultAsync(d => d.PurchaseOrderId == id && d.IsDeleted);
            if (delivery != null)
            {
                delivery.IsDeleted = false;
                delivery.DeletedAt = null;
                delivery.DeletedBy = null;
            }
            await _activityLogService.LogAsync("Restore Purchase Order", "PurchaseOrder",
                $"Restored PO {order.PurchaseOrderNumber} ({order.PurchaseOrderId})", currentStaffId);
            await _context.SaveChangesAsync();
            return Result.Success();
        }

        public async Task<PurchaseOrder?> GetByIdAsync(int id)
        {
            return await _context.PurchaseOrders
                .Where(p => !p.IsDeleted)
                .Include(p => p.Supplier)
                .Include(p => p.Staff)
                .Include(p => p.Items.Where(i => !i.IsDeleted)).ThenInclude(i => i.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PurchaseOrderId == id);
        }

        public async Task LogPrintAsync(int id, int currentStaffId)
        {
            string? poNumber = await _context.PurchaseOrders
                .AsNoTracking()
                .Where(p => p.PurchaseOrderId == id)
                .Select(p => p.PurchaseOrderNumber)
                .FirstOrDefaultAsync();

            await _activityLogService.LogAsync("Print Purchase Order", "PurchaseOrder",
                $"Printed PO {poNumber ?? "?"}", currentStaffId);
        }

        public async Task<SupplierInfoDto?> GetSupplierInfoAsync(int id)
        {
            return await _context.Suppliers
                .AsNoTracking()
                .Where(s => s.SupplierId == id)
                .Select(s => new SupplierInfoDto
                {
                    ContactPerson = s.ContactPerson,
                    ContactNumber = s.ContactNumber,
                    Address = s.Address
                })
                .FirstOrDefaultAsync();
        }

        public async Task<List<ProductLookupDto>> GetProductsBySupplierAsync(int id)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(p => p.SupplierId == id && !p.IsDeleted && p.Supplier != null && !p.Supplier.IsDeleted && p.Supplier.Status == "Active")
                .OrderBy(p => p.ProductName)
                .Select(p => new ProductLookupDto
                {
                    ProductId = p.ProductId,
                    ProductName = p.ProductName,
                    Brand = p.Brand,
                    Category = p.Category != null ? p.Category.CategoryName : null
                })
                .ToListAsync();
        }

        private async Task<Result> ValidateItemsAsync(PurchaseOrderViewModel model)
        {
            if (model.SupplierId <= 0)
                return Result.Failure("SupplierId", "Please select a supplier.");

            if (model.Items == null || model.Items.Count == 0 || model.Items.All(i => i.ProductId <= 0))
                return Result.Failure("Items", "Please add at least one product.");

            var validItems = model.Items.Where(i => i.ProductId > 0).ToList();
            // Ensure supplier is active
            var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == model.SupplierId && !s.IsDeleted);
            if (supplier == null || supplier.Status != "Active")
                return Result.Failure("SupplierId", "The selected supplier is inactive and cannot be used for a new purchase order.");

            for (int i = 0; i < model.Items.Count; i++)
            {
                var item = model.Items[i];
                if (item.ProductId > 0 && item.Quantity <= 0)
                    return Result.Failure($"Items[{i}].Quantity", "Quantity must be greater than 0.");
            }

            var duplicateProductIds = validItems
                .GroupBy(i => i.ProductId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateProductIds.Any())
                return Result.Failure("Items", "Duplicate products are not allowed.");

            var productIds = validItems.Select(i => i.ProductId).Distinct().ToList();
            var selectedProducts = await _context.Products
                .Where(p => productIds.Contains(p.ProductId) && !p.IsDeleted)
                .Select(p => new { p.ProductId, p.SupplierId })
                .ToListAsync();
            if (selectedProducts.Count != productIds.Count)
                return Result.Failure("Items", "One or more selected products are not valid.");
            if (selectedProducts.Any(p => p.SupplierId != model.SupplierId))
                return Result.Failure("Items", "All products must belong to the selected supplier.");

            return Result.Success();
        }

        private async Task<PurchaseOrderViewModel> PopulateListsAsync(PurchaseOrderViewModel model)
        {
model.Suppliers = await _context.Suppliers.AsNoTracking()
                 .Where(s => !s.IsDeleted && s.Status == "Active")
                 .OrderBy(s => s.CompanyName)
                 .Select(s => new SelectListItem { Value = s.SupplierId.ToString(), Text = s.CompanyName })
                 .ToListAsync();

            return model;
        }

        private async Task<string> GeneratePONumberAsync()
        {
            string prefix = "PO-" + DateTime.Now.ToString("yyyyMMdd") + "-";

            string? lastNumber = await _context.PurchaseOrders
                .Where(p => p.PurchaseOrderNumber.StartsWith(prefix))
                .OrderByDescending(p => p.PurchaseOrderNumber)
                .Select(p => p.PurchaseOrderNumber)
                .FirstOrDefaultAsync();

            int nextSeq = 1;
            if (lastNumber != null)
            {
                string lastSeq = lastNumber[prefix.Length..];
                int.TryParse(lastSeq, out nextSeq);
                nextSeq++;
            }

            return prefix + nextSeq.ToString("D4");
        }
    }
}
