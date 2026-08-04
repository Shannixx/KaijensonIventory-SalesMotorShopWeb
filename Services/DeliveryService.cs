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
            var deliveries = await _context.Deliveries
                .Include(d => d.PurchaseOrder)
                    .ThenInclude(p => p.Supplier)
                .Include(d => d.PurchaseOrder)
                    .ThenInclude(p => p.Staff)
                .Include(d => d.PurchaseOrder)
                    .ThenInclude(p => p.Items)
                        .ThenInclude(i => i.Product)
                            .ThenInclude(p => p.Category)
                .AsNoTracking()
                .OrderByDescending(d => d.CreatedDate)
                .Select(d => new DeliveryViewModel
                {
                    DeliveryId = d.DeliveryId,
                    PurchaseOrderId = d.PurchaseOrderId,
                    PurchaseOrderNumber = d.PurchaseOrder != null ? d.PurchaseOrder.PurchaseOrderNumber : null,
                    Status = d.Status,
                    SupplierName = d.PurchaseOrder != null && d.PurchaseOrder.Supplier != null ? d.PurchaseOrder.Supplier.CompanyName : null,
                    OrderDate = d.PurchaseOrder != null ? d.PurchaseOrder.OrderDate : DateTime.MinValue,
                    DeliveredDate = d.DeliveredDate,
                    CreatedByName = d.PurchaseOrder != null && d.PurchaseOrder.Staff != null ? d.PurchaseOrder.Staff.StaffName : null,
                    Items = d.PurchaseOrder != null
                        ? d.PurchaseOrder.Items.Select(i => new DeliveryItemViewModel
                        {
                            ProductName = i.Product != null ? i.Product.ProductName : null,
                            Brand = i.Product != null ? i.Product.Brand : null,
                            Category = i.Product != null && i.Product.Category != null ? i.Product.Category.CategoryName : null,
                            Quantity = i.Quantity
                        }).ToList()
                        : new List<DeliveryItemViewModel>()
                })
                .ToListAsync();

            return deliveries;
        }

        public async Task<DeliveryViewModel?> GetDeliveryDetailsAsync(int id)
        {
            var delivery = await _context.Deliveries
                .Include(d => d.PurchaseOrder)
                    .ThenInclude(p => p.Supplier)
                .Include(d => d.PurchaseOrder)
                    .ThenInclude(p => p.Staff)
                .Include(d => d.PurchaseOrder)
                    .ThenInclude(p => p.Items)
                        .ThenInclude(i => i.Product)
                            .ThenInclude(p => p.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DeliveryId == id);

            if (delivery == null) return null;

            var order = delivery.PurchaseOrder;
            return new DeliveryViewModel
            {
                DeliveryId = delivery.DeliveryId,
                PurchaseOrderId = order?.PurchaseOrderId ?? 0,
                PurchaseOrderNumber = order?.PurchaseOrderNumber,
                Status = delivery.Status,
                SupplierName = order?.Supplier?.CompanyName,
                OrderDate = order?.OrderDate ?? DateTime.MinValue,
                DeliveredDate = delivery.DeliveredDate,
                CreatedByName = order?.Staff?.StaffName,
                Items = order?.Items.Select(i => new DeliveryItemViewModel
                {
                    ProductName = i.Product?.ProductName,
                    Brand = i.Product?.Brand,
                    Category = i.Product?.Category?.CategoryName,
                    Quantity = i.Quantity
                }).ToList() ?? new List<DeliveryItemViewModel>()
            };
        }

        public async Task<Result> DeliverAsync(int id, int currentStaffId)
        {
            var delivery = await _context.Deliveries
                .Include(d => d.PurchaseOrder)
                    .ThenInclude(p => p.Items)
                        .ThenInclude(i => i.Product)
                            .ThenInclude(p => p.Category)
                .FirstOrDefaultAsync(d => d.DeliveryId == id);

            if (delivery == null)
                return Result.Failure(null, "The delivery could not be found.");

            if (delivery.Status != "Pending")
                return Result.Failure(null, $"Cannot mark delivery as delivered with status '{delivery.Status}'.");

            var order = delivery.PurchaseOrder;
            if (order == null)
                return Result.Failure(null, "Associated purchase order not found.");

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

            delivery.Status = "Delivered";
            delivery.DeliveredDate = DateTime.Now;

            await _context.SaveChangesAsync();

            await _activityLogService.LogAsync("Mark Delivery", "Delivery",
                $"Delivery for PO {order.PurchaseOrderNumber} marked as Delivered", currentStaffId);

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
