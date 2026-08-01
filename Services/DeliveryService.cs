using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public class DeliveryService : IDeliveryService
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityLogService _activityLogService;

        public DeliveryService(ApplicationDbContext context, IActivityLogService activityLogService)
        {
            _context = context;
            _activityLogService = activityLogService;
        }

        public async Task<List<DeliveryViewModel>> GetAwaitingDeliveryAsync()
        {
            var orders = await _context.PurchaseOrders
                .Include(p => p.Supplier)
                .AsNoTracking()
                .Where(p => p.Status == "Approved")
                .OrderByDescending(p => p.CreatedDate)
                .Select(p => new DeliveryViewModel
                {
                    PurchaseOrderId = p.PurchaseOrderId,
                    PurchaseOrderNumber = p.PurchaseOrderNumber,
                    Status = p.Status,
                    SupplierName = p.Supplier != null ? p.Supplier.CompanyName : null,
                    OrderDate = p.OrderDate,
                    DeliveredDate = p.DeliveredDate,
                    CreatedByName = p.Staff != null ? p.Staff.StaffName : null,
                    Items = p.Items.Select(i => new DeliveryItemViewModel
                    {
                        ProductName = i.Product != null ? i.Product.ProductName : null,
                        Brand = i.Product != null ? i.Product.Brand : null,
                        PartType = i.Product != null ? i.Product.PartType : null,
                        Quantity = i.Quantity
                    }).ToList()
                })
                .ToListAsync();

            return orders;
        }

        public async Task<DeliveryViewModel?> GetDeliveryDetailsAsync(int id)
        {
            var order = await _context.PurchaseOrders
                .Include(p => p.Supplier)
                .Include(p => p.Staff)
                .Include(p => p.Items).ThenInclude(i => i.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PurchaseOrderId == id);

            if (order == null) return null;

            return new DeliveryViewModel
            {
                PurchaseOrderId = order.PurchaseOrderId,
                PurchaseOrderNumber = order.PurchaseOrderNumber,
                Status = order.Status,
                SupplierName = order.Supplier?.CompanyName,
                OrderDate = order.OrderDate,
                DeliveredDate = order.DeliveredDate,
                CreatedByName = order.Staff?.StaffName,
                Items = order.Items.Select(i => new DeliveryItemViewModel
                {
                    ProductName = i.Product?.ProductName,
                    Brand = i.Product?.Brand,
                    PartType = i.Product?.PartType,
                    Quantity = i.Quantity
                }).ToList()
            };
        }

        public async Task<Result> DeliverAsync(int id, int currentStaffId)
        {
            PurchaseOrder? order = await _context.PurchaseOrders
                .Include(p => p.Items).ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(p => p.PurchaseOrderId == id);

            if (order == null)
                return Result.Failure(null, "The purchase order could not be found.");

            if (order.Status != "Approved")
                return Result.Failure(null, $"Cannot deliver a purchase order with status '{order.Status}'.");

            if (order.Items.All(i => i.Product == null))
                return Result.Failure(null, "The purchase order has no deliverable items.");

            await using var transaction = await _context.Database.BeginTransactionAsync();

            foreach (var item in order.Items.Where(i => i.Product != null))
            {
                int previousQty = item.Product!.QuantityOnHand;

                item.Product.QuantityOnHand += item.Quantity;
                item.Product.LastStockInDate = DateTime.Now;

                decimal unitCost = item.Product.Price > 0 ? item.Product.Price : item.Product.AverageCost;
                item.Product.AverageCost = CalculateNewAverageCost(
                    previousQty,
                    item.Product.AverageCost,
                    item.Quantity,
                    unitCost);

                item.Product.StockStatus = CalculateStockStatus(
                    item.Product.QuantityOnHand, item.Product.ReorderLevel);
            }

            order.Status = "Delivered";
            order.DeliveredDate = DateTime.Now;
            order.DeliveredBy = currentStaffId;
            order.UpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            await _activityLogService.LogAsync("Deliver Purchase Order", "PurchaseOrder",
                $"Delivered PO {order.PurchaseOrderNumber} (Approved -> Delivered)", currentStaffId);

            await transaction.CommitAsync();

            return Result.Success();
        }

        private static decimal CalculateNewAverageCost(decimal oldQty, decimal oldAvgCost, decimal newQty, decimal newUnitCost)
        {
            if (oldQty + newQty == 0) return newUnitCost;
            return ((oldQty * oldAvgCost) + (newQty * newUnitCost)) / (oldQty + newQty);
        }

        private static string CalculateStockStatus(int qty, int reorder)
        {
            if (qty <= 0) return "Out of Stock";
            if (qty <= reorder) return "Low Stock";
            return "Available";
        }
    }
}
