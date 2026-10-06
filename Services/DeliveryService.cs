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

        public async Task<List<DeliveryViewModel>> GetAwaitingDeliveryAsync(bool archived = false)
        {
            var deliveries = await _context.Deliveries
                .Where(d => archived ? d.IsDeleted : !d.IsDeleted)
                .Where(d => d.PurchaseOrder != null && (archived || !d.PurchaseOrder.IsDeleted) && (archived || d.Status == "Pending" || d.Status == "Partially Delivered" || d.Status == "Delivered"))
                .Include(d => d.PurchaseOrder!)
                    .ThenInclude(p => p.Supplier)
                .Include(d => d.PurchaseOrder!)
                    .ThenInclude(p => p.Staff)
                .Include(d => d.PurchaseOrder!)
                    .ThenInclude(p => p.Items.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.Product!)
                            .ThenInclude(p => p.Category)
                .AsNoTracking()
                .OrderByDescending(d => d.CreatedDate)
                .Select(d => new DeliveryViewModel
                {
                    DeliveryId = d.DeliveryId,
                    PurchaseOrderId = d.PurchaseOrderId,
                    PurchaseOrderNumber = d.PurchaseOrder != null ? d.PurchaseOrder.PurchaseOrderNumber : string.Empty,
                    Status = d.Status,
                    SupplierName = d.PurchaseOrder != null && d.PurchaseOrder.Supplier != null ? d.PurchaseOrder.Supplier.CompanyName : string.Empty,
                    OrderDate = d.PurchaseOrder != null ? d.PurchaseOrder.OrderDate : DateTime.MinValue,
                    DeliveredDate = d.DeliveredDate,
                    CreatedByName = d.PurchaseOrder != null && d.PurchaseOrder.Staff != null ? d.PurchaseOrder.Staff.StaffName : string.Empty,
Items = d.PurchaseOrder != null
                ? d.PurchaseOrder.Items.Select(i => new DeliveryItemViewModel
                {
                    ProductName = i.Product != null ? i.Product.ProductName : string.Empty,
                    Brand = i.Product != null ? i.Product.Brand : string.Empty,
                    Category = i.Product != null && i.Product.Category != null ? i.Product.Category.CategoryName : string.Empty,
                    Quantity = i.Quantity,
                    ReceivedQuantity = i.ReceivedQuantity,
                    PurchaseOrderItemId = i.PurchaseOrderItemId
                }).ToList()
                : new List<DeliveryItemViewModel>()
                })
                .ToListAsync();

            return deliveries;
        }

        public async Task<DeliveryViewModel?> GetDeliveryDetailsAsync(int id, bool archived = false)
        {
            var delivery = await _context.Deliveries
                .Include(d => d.PurchaseOrder!)
                    .ThenInclude(p => p.Supplier)
                .Include(d => d.PurchaseOrder!)
                    .ThenInclude(p => p.Staff)
                .Include(d => d.PurchaseOrder!)
                    .ThenInclude(p => p.Items.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.Product!)
                            .ThenInclude(p => p.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DeliveryId == id && (archived ? d.IsDeleted : !d.IsDeleted));

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
                ReceivedByName = delivery.ReceivedBy.HasValue ? _context.Staff.FirstOrDefault(s=>s.StaffId==delivery.ReceivedBy.Value)?.StaffName : null,
                Remarks = delivery.Remarks,
                CreatedByName = order?.Staff?.StaffName,
Items = order?.Items?.Select(i => new DeliveryItemViewModel
{
                    ProductName = i.Product != null ? i.Product.ProductName : string.Empty,
                    Brand = i.Product != null ? i.Product.Brand : string.Empty,
                    Category = i.Product != null && i.Product.Category != null ? i.Product.Category.CategoryName : string.Empty,
                    Quantity = i.Quantity,
                    ReceivedQuantity = i.ReceivedQuantity,
                    PurchaseOrderItemId = i.PurchaseOrderItemId
                }).ToList() ?? new List<DeliveryItemViewModel>(),
                History = history
            };
        }

        public async Task<Result> DeliverAsync(int id, Dictionary<int,int> receiveQuantities, int currentStaffId, string receiptKey, string? remarks = null)
        {
            if (!Guid.TryParseExact(receiptKey, "N", out var receiptId))
                return Result.Failure("ReceiptKey", "Invalid receipt form. Reload the page and try again.");
            receiptKey = receiptId.ToString("N");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            // Reserve the receipt before reading its status or remaining quantities.
            var receiptLock = $"Delivery:{id}";
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                DECLARE @lockResult int;
                EXEC @lockResult = sp_getapplock @Resource = {receiptLock},
                    @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
                IF @lockResult < 0 THROW 51002, 'Unable to reserve the delivery. Please retry.', 1;");

            // A repeated POST for the same form is already committed; do not replay stock.
            if (await _context.DeliveryItems.AsNoTracking()
                .AnyAsync(di => di.DeliveryId == id && di.ReceiptKey == receiptKey))
                return Result.Success();

            var orderId = await _context.Deliveries.AsNoTracking()
                .Where(d => d.DeliveryId == id).Select(d => (int?)d.PurchaseOrderId).FirstOrDefaultAsync();
            if (!orderId.HasValue)
                return Result.Failure(null, "The delivery could not be found.");
            await InventoryWriteLock.AcquireOrderAsync(_context, orderId.Value);

            var delivery = await _context.Deliveries
                .Include(d => d.PurchaseOrder!)
                    .ThenInclude(p => p.Items.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.Product!)
                            .ThenInclude(p => p.Category)
                .FirstOrDefaultAsync(d => d.DeliveryId == id && !d.IsDeleted && !d.PurchaseOrder!.IsDeleted);

            if (delivery == null)
                return Result.Failure(null, "The delivery could not be found.");

            if (delivery.PurchaseOrder?.IsDeleted == true)
                return Result.Failure(null, "The associated purchase order is archived.");

            if (delivery.Status != "Pending" && delivery.Status != "Partially Delivered")
                return Result.Failure(null, $"Cannot mark delivery as delivered with status '{delivery.Status}'.");

            var order = delivery.PurchaseOrder;
            if (order == null)
                return Result.Failure(null, "Associated purchase order not found.");

            // Reserve stock before using the initially loaded product and PO quantities.
            var lockedProductIds = order.Items.Select(i => i.ProductId).Distinct().OrderBy(productId => productId).ToArray();
            foreach (var productId in lockedProductIds)
                await InventoryWriteLock.AcquireProductAsync(_context, productId);
            var refreshedProducts = new HashSet<int>();
            foreach (var item in order.Items)
            {
                await _context.Entry(item).ReloadAsync();
                if (!lockedProductIds.Contains(item.ProductId))
                    return Result.Failure(null, "The purchase order changed while receiving. Please retry.");
                if (item.Product != null && refreshedProducts.Add(item.Product.ProductId))
                    await _context.Entry(item.Product).ReloadAsync();
            }

            if (order.Items.Any(i => i.Product == null || i.Product.IsDeleted))
                return Result.Failure(null, "The purchase order contains an archived or missing product.");

            if (order.Items.All(i => i.Product == null))
                return Result.Failure(null, "The purchase order has no deliverable items.");

            var restockedProducts = new List<Product>();
            var receivedStockByProduct = new Dictionary<int, (Product Product, int QuantityReceived)>();
            
            bool anyReceived = false;
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

                // Validation
                if (receiveNow < 0)
                {
                    return Result.Failure(null, $"Received quantity cannot be negative for item {item.PurchaseOrderItemId}.");
                }

                if (receiveNow == 0)
                {
                    // Skip items with zero receive; will validate later if all zero
                    continue;
                }

                if (receiveNow > remaining)
                {
                    return Result.Failure(null, $"Receive quantity {receiveNow} exceeds remaining {remaining} for item {item.PurchaseOrderItemId}.");
                }

                anyReceived = true;

                // Update product inventory
                int previousQty = item.Product?.QuantityOnHand ?? 0;
                item.Product!.QuantityOnHand += receiveNow;
                if (item.Product.IsSerialized)
                    _context.SerialUnits.AddRange(Enumerable.Range(0, receiveNow)
                        .Select(_ => SerialUnit.CreateAvailable(item.Product.ProductId)));
                item.Product.LastStockInDate = DateTime.Now;

                decimal unitCost = (item.Price > 0 ? item.Price : (item.Product?.Price > 0 ? item.Product.Price : item.Product?.AverageCost ?? 0));
                item.Product!.AverageCost = CalculateNewAverageCost(
                    previousQty,
                    item.Product.AverageCost,
                    receiveNow,
                    unitCost);

                item.Product!.StockStatus = StockHelper.GetStockStatus(item.Product!.QuantityOnHand);
                item.Product!.LastUpdated = DateTime.Now;

                // Track stock-in quantities per product for one event notification per receipt.
                if (item.Product is { } product)
                {
                    restockedProducts.Add(product);
                    if (receivedStockByProduct.TryGetValue(product.ProductId, out var existingStock))
                        receivedStockByProduct[product.ProductId] = (product, existingStock.QuantityReceived + receiveNow);
                    else
                        receivedStockByProduct[product.ProductId] = (product, receiveNow);
                }

                // Update PO item received quantity
                item.ReceivedQuantity += receiveNow;

                // Record delivery item history
                var deliveryItem = new DeliveryItem
                {
                    DeliveryId = delivery.DeliveryId,
                    PurchaseOrderItemId = item.PurchaseOrderItemId,
                    ReceivedQuantity = receiveNow,
                    ReceiptKey = receiptKey,
                    ReceivedDate = DateTime.Now
                };
                _context.DeliveryItems.Add(deliveryItem);
            }
            if (!anyReceived)
            {
                return Result.Failure(null, "Enter at least one quantity to receive.");
            }

            // Determine delivery and order status based on remaining quantities
            bool allReceived = order.Items.All(i => i.ReceivedQuantity >= i.Quantity);
            delivery.Status = allReceived ? "Delivered" : "Partially Delivered";
            if (allReceived)
                delivery.DeliveredDate = DateTime.Now;

            // Update purchase order status
            order.Status = allReceived ? "Delivered" : "Partially Delivered";
            order.UpdatedDate = DateTime.Now;

            delivery.ReceivedBy = currentStaffId;
            if (!string.IsNullOrWhiteSpace(remarks))
                delivery.Remarks = remarks;
            await _context.SaveChangesAsync();

            await _activityLogService.LogAsync("Mark Delivery", "Delivery",
                $"Delivery for PO {order.PurchaseOrderNumber} processed. Status: {delivery.Status}", currentStaffId);

            // Emit an event notification for each product actually received in this operation.
            foreach (var stockIn in receivedStockByProduct.Values)
            {
                string unitLabel = stockIn.QuantityReceived == 1 ? "unit" : "units";
                await _notificationService.CreateAsync(
                    stockIn.Product.ProductId,
                    "NewStock",
                    $"{stockIn.QuantityReceived} {unitLabel} received for {stockIn.Product.ProductName}. Current stock: {stockIn.Product.QuantityOnHand}.",
                    currentStaffId);
            }

            // Notification evaluation after stock increased (recovery + reorder check)
            foreach (var product in restockedProducts)
            {
                int newQty = product.QuantityOnHand;

                // Stock available again -> resolve stale Out of Stock alerts
                if (newQty > 0)
                    await _notificationService.ResolveUnreadAsync(product.ProductId, "OutOfStock", currentStaffId);

                // Still below the low stock threshold -> create/keep an active Low Stock alert (deduplicated)
                if (newQty > 0 && newQty < StockHelper.LowStockThreshold)
                    await _notificationService.CreateOnceAsync(product.ProductId, "LowStock",
                        $"Low stock for {product.ProductName} (Qty {newQty}).", currentStaffId);
                // Back above the low stock threshold -> resolve stale Low Stock alerts
                else if (newQty >= StockHelper.LowStockThreshold)
                    await _notificationService.ResolveUnreadAsync(product.ProductId, "LowStock", currentStaffId);

                // Reorder: still at/below the reorder level -> keep an active alert (deduplicated);
                // back above it -> resolve previous Reorder notifications
                if (newQty <= product.ReorderLevel)
                {
                    await _notificationService.CreateOnceAsync(product.ProductId, "Reorder",
                        $"{product.ProductName} reached reorder level. Qty: {newQty}.", currentStaffId);
                }
                else
                {
                    await _notificationService.ResolveUnreadAsync(product.ProductId, "Reorder", currentStaffId);
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

        public async Task<Result> ArchiveAsync(int id, int currentStaffId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            // Match the receipt's lock order: delivery first, then its purchase order.
            var receiptLock = $"Delivery:{id}";
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                DECLARE @lockResult int;
                EXEC @lockResult = sp_getapplock @Resource = {receiptLock},
                    @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
                IF @lockResult < 0 THROW 51002, 'Unable to reserve the delivery. Please retry.', 1;");
            var orderId = await _context.Deliveries.AsNoTracking()
                .Where(d => d.DeliveryId == id).Select(d => (int?)d.PurchaseOrderId).FirstOrDefaultAsync();
            if (!orderId.HasValue)
                return Result.Failure(null, "The delivery could not be found or is already archived.");
            await InventoryWriteLock.AcquireOrderAsync(_context, orderId.Value);
            var delivery = await _context.Deliveries.FirstOrDefaultAsync(d => d.DeliveryId == id && !d.IsDeleted);
            if (delivery == null) return Result.Failure(null, "The delivery could not be found or is already archived.");
            await _context.Entry(delivery).ReloadAsync();
            if (delivery.IsDeleted || delivery.PurchaseOrderId != orderId.Value)
                return Result.Failure(null, "The delivery changed while archiving. Please retry.");

            // Archiving is visibility-only; posted receipt events and their inventory effects remain unchanged.
            bool hasHistory = await _context.DeliveryItems.AnyAsync(di => di.DeliveryId == id);
            if (hasHistory && delivery.Status != "Delivered" && delivery.Status != "Partially Delivered")
                return Result.Failure(null, "The delivery cannot be archived in its current state.");

            delivery.IsDeleted = true;
            delivery.DeletedAt = DateTime.UtcNow;
            delivery.DeletedBy = currentStaffId;

            if (delivery.PurchaseOrderId > 0)
            {
                var order = await _context.PurchaseOrders.FirstOrDefaultAsync(p => p.PurchaseOrderId == delivery.PurchaseOrderId && !p.IsDeleted);
                if (order != null)
                {
                    order.IsDeleted = true;
                    order.DeletedAt = delivery.DeletedAt;
                    order.DeletedBy = currentStaffId;
                }
            }

            await _activityLogService.LogAsync("Archive Delivery", "Delivery", $"Archived delivery {id} without changing inventory.", currentStaffId);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Result.Success();
        }

        public async Task<Result> RestoreAsync(int id, int currentStaffId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            // A restore must not race the receipt or archive of the same delivery/order.
            var receiptLock = $"Delivery:{id}";
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                DECLARE @lockResult int;
                EXEC @lockResult = sp_getapplock @Resource = {receiptLock},
                    @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
                IF @lockResult < 0 THROW 51002, 'Unable to reserve the delivery. Please retry.', 1;");
            var orderId = await _context.Deliveries.AsNoTracking()
                .Where(d => d.DeliveryId == id).Select(d => (int?)d.PurchaseOrderId).FirstOrDefaultAsync();
            if (!orderId.HasValue)
                return Result.Failure(null, "The archived delivery could not be found.");
            await InventoryWriteLock.AcquireOrderAsync(_context, orderId.Value);
            var delivery = await _context.Deliveries.FirstOrDefaultAsync(d => d.DeliveryId == id);
            if (delivery == null) return Result.Failure(null, "The archived delivery could not be found.");
            await _context.Entry(delivery).ReloadAsync();
            if (!delivery.IsDeleted || delivery.PurchaseOrderId != orderId.Value)
                return Result.Failure(null, "The archived delivery changed. Please reload before restoring.");
            var order = await _context.PurchaseOrders.FirstOrDefaultAsync(p => p.PurchaseOrderId == orderId.Value);
            if (order == null) return Result.Failure(null, "The linked purchase order could not be found.");
            await _context.Entry(order).ReloadAsync();
            if (order.IsDeleted)
            {
                var supplier = await _context.Suppliers.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SupplierId == order.SupplierId);
                if (supplier == null || supplier.IsDeleted || supplier.Status != "Active")
                    return Result.Failure(null, "The delivery cannot be restored until its supplier is active.");
            }

            var activeProducts = await _context.PurchaseOrderItems
                .Where(i => i.PurchaseOrderId == delivery.PurchaseOrderId && !i.IsDeleted)
                .Join(_context.Products, i => i.ProductId, p => p.ProductId, (i, p) => p)
                .Where(p => p.IsDeleted)
                .AnyAsync();
            if (activeProducts)
                return Result.Failure(null, "The delivery cannot be restored while a linked product is archived.");

            if (order.IsDeleted)
            {
                order.IsDeleted = false;
                order.DeletedAt = null;
                order.DeletedBy = null;
            }
            delivery.IsDeleted = false;
            delivery.DeletedAt = null;
            delivery.DeletedBy = null;
            await _activityLogService.LogAsync("Restore Delivery", "Delivery", $"Restored delivery {id} and its parent order without replaying inventory.", currentStaffId);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Result.Success();
        }

        private static string CalculateStockStatus(int qty)
        {
            return StockHelper.GetStockStatus(qty);
        }
    }
}
