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
        private readonly INotificationService _notificationService;

        public DeliveryService(ApplicationDbContext context,
                               IActivityLogService activityLogService,
                               INotificationService notificationService)
        {
            _context = context;
            _activityLogService = activityLogService;
            _notificationService = notificationService;
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
                    Quantity = i.Quantity,
                    ReceivedQuantity = i.ReceivedQuantity,
                    PurchaseOrderItemId = i.PurchaseOrderItemId
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
            // Load delivery items (receiving events) for history
            var deliveryItems = await _context.DeliveryItems
                .Where(di => di.DeliveryId == id)
                .OrderBy(di => di.ReceivedDate)
                .ToListAsync();

            var cumulative = new Dictionary<int, int>(); // PO item ID -> cumulative received
            var history = new List<DeliveryHistoryViewModel>();
            foreach (var di in deliveryItems)
            {
                var poItem = order?.Items.FirstOrDefault(i => i.PurchaseOrderItemId == di.PurchaseOrderItemId);
                int orderedQty = poItem?.Quantity ?? 0;
                int prev = cumulative.ContainsKey(di.PurchaseOrderItemId) ? cumulative[di.PurchaseOrderItemId] : 0;
                int newCum = prev + di.ReceivedQuantity;
                cumulative[di.PurchaseOrderItemId] = newCum;

                // Calculate overall PO status after this receiving event
                int totalOrdered = order?.Items.Sum(i => i.Quantity) ?? 0;
                int totalReceived = cumulative.Values.Sum();
                string statusAfter = totalReceived >= totalOrdered ? "Delivered" : "Partially Delivered";

                history.Add(new DeliveryHistoryViewModel
                {
                    PurchaseOrderNumber = order?.PurchaseOrderNumber,
                    OrderDate = order?.OrderDate ?? DateTime.MinValue,
                    ProductName = poItem?.Product?.ProductName,
                    DateReceived = di.ReceivedDate,
                    QuantityReceived = di.ReceivedQuantity,
                    StatusAfter = statusAfter
                });
            }

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
                    Quantity = i.Quantity,
                    ReceivedQuantity = i.ReceivedQuantity,
                    PurchaseOrderItemId = i.PurchaseOrderItemId
                }).ToList() ?? new List<DeliveryItemViewModel>(),
                History = history
            };
        }

        public async Task<Result> DeliverAsync(int id, Dictionary<int,int> receiveQuantities, int currentStaffId)
        {
            var delivery = await _context.Deliveries
                .Include(d => d.PurchaseOrder)
                    .ThenInclude(p => p.Items)
                        .ThenInclude(i => i.Product)
                            .ThenInclude(p => p.Category)
                .FirstOrDefaultAsync(d => d.DeliveryId == id);

            if (delivery == null)
                return Result.Failure(null, "The delivery could not be found.");

            if (delivery.Status != "Pending" && delivery.Status != "Partially Delivered")
                return Result.Failure(null, $"Cannot mark delivery as delivered with status '{delivery.Status}'.");

            var order = delivery.PurchaseOrder;
            if (order == null)
                return Result.Failure(null, "Associated purchase order not found.");

            if (order.Items.All(i => i.Product == null))
                return Result.Failure(null, "The purchase order has no deliverable items.");

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var restockedProducts = new List<Product>();
            
            foreach (var item in order.Items.Where(i => i.Product != null))
            {
                // Determine how many units remain to be received for this PO item
                int remaining = item.Quantity - item.ReceivedQuantity;
                if (remaining <= 0)
                    continue; // already fully received

                // Determine receive quantity from input (if provided) otherwise default to remaining
                int receiveNow = 0;
                if (receiveQuantities != null && receiveQuantities.TryGetValue(item.PurchaseOrderItemId, out var requested))
                {
                    receiveNow = requested;
                }

                if (receiveNow <= 0)
                    continue; // nothing to receive for this item

                if (receiveNow > remaining)
                {
                    return Result.Failure(null, $"Receive quantity {receiveNow} exceeds remaining {remaining} for item {item.PurchaseOrderItemId}.");
                }

                // Update product inventory
                int previousQty = item.Product!.QuantityOnHand;
                item.Product.QuantityOnHand += receiveNow;
                item.Product.LastStockInDate = DateTime.Now;

                decimal unitCost = item.Product.Price > 0 ? item.Product.Price : item.Product.AverageCost;
                item.Product.AverageCost = CalculateNewAverageCost(
                    previousQty,
                    item.Product.AverageCost,
                    receiveNow,
                    unitCost);

                item.Product.StockStatus = StockHelper.GetStockStatus(item.Product.QuantityOnHand);

                // Track restocked products for post-save notification evaluation
                restockedProducts.Add(item.Product!);

                // Update PO item received quantity
                item.ReceivedQuantity += receiveNow;

                // Record delivery item history
                var deliveryItem = new DeliveryItem
                {
                    DeliveryId = delivery.DeliveryId,
                    PurchaseOrderItemId = item.PurchaseOrderItemId,
                    ReceivedQuantity = receiveNow,
                    ReceivedDate = DateTime.Now
                };
                _context.DeliveryItems.Add(deliveryItem);

                
            }

            // Determine delivery and order status based on remaining quantities
            bool allReceived = order.Items.All(i => i.ReceivedQuantity >= i.Quantity);
            delivery.Status = allReceived ? "Delivered" : "Partially Delivered";
            if (allReceived)
                delivery.DeliveredDate = DateTime.Now;

            // Update purchase order status
            order.Status = allReceived ? "Delivered" : "Partially Delivered";
            order.UpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            await _activityLogService.LogAsync("Mark Delivery", "Delivery",
                $"Delivery for PO {order.PurchaseOrderNumber} processed. Status: {delivery.Status}", currentStaffId);

            // Notification evaluation after stock increased (recovery + reorder check)
            foreach (var product in restockedProducts)
            {
                int newQty = product.QuantityOnHand;

                // Stock available again -> resolve stale Out of Stock alerts
                if (newQty > 0)
                    await _notificationService.ResolveUnreadAsync(product.ProductId, "OutOfStock");

                // Still below the low stock threshold -> create/keep an active Low Stock alert (deduplicated)
                if (newQty > 0 && newQty < StockHelper.LowStockThreshold)
                    await _notificationService.CreateOnceAsync(product.ProductId, "LowStock",
                        $"Low stock for {product.ProductName} (Qty {newQty}).");
                // Back above the low stock threshold -> resolve stale Low Stock alerts
                else if (newQty >= StockHelper.LowStockThreshold)
                    await _notificationService.ResolveUnreadAsync(product.ProductId, "LowStock");

                // Reorder: still at/below the reorder level -> keep an active alert (deduplicated);
                // back above it -> resolve previous Reorder notifications
                if (newQty <= product.ReorderLevel)
                {
                    await _notificationService.CreateOnceAsync(product.ProductId, "Reorder",
                        $"{product.ProductName} reached reorder level. Qty: {newQty}.");
                }
                else
                {
                    await _notificationService.ResolveUnreadAsync(product.ProductId, "Reorder");
                }
            }

            await transaction.CommitAsync();

            return Result.Success();
        }

        private static decimal CalculateNewAverageCost(decimal oldQty, decimal oldAvgCost, decimal newQty, decimal newUnitCost)
        {
            if (oldQty + newQty == 0) return newUnitCost;
            return ((oldQty * oldAvgCost) + (newQty * newUnitCost)) / (oldQty + newQty);
        }

        private static string CalculateStockStatus(int qty)
        {
            return StockHelper.GetStockStatus(qty);
        }
    }
}
