using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public class SaleService : ISaleService
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityLogService _activityLogService;
        private readonly INotificationService _notificationService;

        public SaleService(ApplicationDbContext context,
                           IActivityLogService activityLogService,
                           INotificationService notificationService)
        {
            _context = context;
            _activityLogService = activityLogService;
            _notificationService = notificationService;
        }

        public async Task<SalesTransaction> ProcessSaleAsync(
            CartViewModel cart,
            decimal amountPaid,
            string checkoutKey,
            int staffId)
        {
            if (string.IsNullOrWhiteSpace(checkoutKey) || checkoutKey.Length > 100)
                throw new InvalidOperationException("Invalid checkout key.");

            // Take the month reservation before checkout-key and product locks.
            // A competing sale must not hold an index-gap lock while waiting
            // for this month's number to become available.
            var transactionDate = DateTime.Now;
            var monthStart = new DateTime(transactionDate.Year, transactionDate.Month, 1);
            await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await InventoryWriteLock.AcquireSalesMonthAsync(_context, monthStart);
            await InventoryWriteLock.AcquireCheckoutAsync(_context, checkoutKey);
            // A SERIALIZABLE read of a missing key otherwise holds a shared index-gap lock:
            // competing checkouts can each read the gap, then block one another on insert.
            var existing = await _context.SalesTransactions
                .FromSqlInterpolated($@"SELECT * FROM dbo.SalesTransactions WITH (UPDLOCK, HOLDLOCK, INDEX(IX_SalesTransactions_CheckoutKey))
                    WHERE CheckoutKey = {checkoutKey}")
                .Include(t => t.Items)
                .FirstOrDefaultAsync();
            if (existing != null)
            {
                await tx.CommitAsync();
                return existing;
            }

            // Reserve all products in a stable order.
            foreach (var productId in cart.Items.Select(i => i.ProductId).Distinct().OrderBy(id => id))
                await InventoryWriteLock.AcquireProductAsync(_context, productId);

            // Re-read products and calculate totals
            decimal serverTotal = 0m;
            var itemsToCreate = new List<SalesItem>();
            var affectedProducts = new List<Product>();
            var serialsToSell = new List<SerialUnit>();

            foreach (var cartItem in cart.Items)
            {
                // Validate quantity > 0
                if (cartItem.Quantity <= 0)
                    throw new InvalidOperationException($"Quantity must be greater than zero for product ID {cartItem.ProductId}.");

                var product = await _context.Products
                    .Where(p => p.ProductId == cartItem.ProductId && !p.IsDeleted)
                    .FirstOrDefaultAsync();

                if (product == null)
                    throw new InvalidOperationException($"Product with ID {cartItem.ProductId} not found.");
                // Tracking queries can return an older in-memory entity from this context.
                // Refresh under the inventory lock before checking stock or pricing.
                await _context.Entry(product).ReloadAsync();
                if (product.IsDeleted)
                    throw new InvalidOperationException($"Product with ID {cartItem.ProductId} is archived.");

                // Ensure product is enabled/available (StockStatus not OutOfStock)
                if (product.StockStatus == "Out of Stock")
                    throw new InvalidOperationException($"Product {product.ProductName} is out of stock.");

                // Verify sufficient stock now
                if (product.QuantityOnHand < cartItem.Quantity)
                    throw new InvalidOperationException($"Only {product.QuantityOnHand} of {product.ProductName} are available.");

                // Snapshot price
                var unitPrice = product.Price;
                var subtotal = unitPrice * cartItem.Quantity;
                serverTotal += subtotal;

                // Select existing physical units, never mint a serial at checkout.
                if (product.IsSerialized)
                {
                    var available = await _context.SerialUnits
                        .Where(s => s.ProductId == product.ProductId && s.Status == "Available" && s.SalesTransactionId == null)
                        .OrderBy(s => s.SerialUnitId).Take(cartItem.Quantity).ToListAsync();
                    if (available.Count != cartItem.Quantity)
                        throw new InvalidOperationException($"Not enough serialized units are available for {product.ProductName}.");

                    if (cart.SerialNumbers.TryGetValue(product.ProductId, out var selected) && selected.Count > 0)
                    {
                        if (selected.Count != cartItem.Quantity ||
                            selected.Distinct(StringComparer.OrdinalIgnoreCase).Count() != selected.Count ||
                            !selected.OrderBy(s => s).SequenceEqual(available.Select(s => s.SerialNumber).OrderBy(s => s)))
                            throw new InvalidOperationException($"Available serial numbers changed for {product.ProductName}. Refresh the cart before payment.");
                    }
                    serialsToSell.AddRange(available);
                }

                // Prepare SalesItem
                var salesItem = new SalesItem
                {
                    ProductId = product.ProductId,
                    Quantity = cartItem.Quantity,
                    UnitPrice = unitPrice,
                    Subtotal = subtotal
                };
                itemsToCreate.Add(salesItem);
                affectedProducts.Add(product);
            }

            // Validate payment
            if (amountPaid < serverTotal)
                throw new InvalidOperationException("Amount paid is insufficient for the total amount.");

            var change = amountPaid - serverTotal;

            // Create SalesTransaction
            // Generate receipt number in format MMM-XXYY (month, transaction count, total quantity)
                var monthEnd = monthStart.AddMonths(1);
                var monthTransactionCount = await _context.SalesTransactions
                    .Where(t => t.TransactionDate >= monthStart && t.TransactionDate < monthEnd)
                    .CountAsync();
                var transactionNumber = monthTransactionCount + 1;
                // Ensure transaction number is within allowed range 1‑99
                if (transactionNumber < 1 || transactionNumber > 99)
                    throw new InvalidOperationException($"Transaction number {transactionNumber} is out of allowed range (1-99).");
                var monthPart = transactionDate.Month.ToString("D3"); // 3‑digit month
                var transactionPart = transactionNumber.ToString("D2"); // 2‑digit transaction within month
                var totalQuantity = cart.Items.Sum(i => i.Quantity);
                // Ensure total quantity is within allowed range 1‑99
                if (totalQuantity < 1 || totalQuantity > 99)
                    throw new InvalidOperationException($"Total quantity {totalQuantity} is out of allowed range (1-99).");
                var totalQtyPart = totalQuantity.ToString("D2"); // 2‑digit total quantity
                var receiptNumber = $"{monthPart}-{transactionPart}{totalQtyPart}";
                var transaction = new SalesTransaction
                {
                    InvoiceNumber = receiptNumber,
                    CheckoutKey = checkoutKey,
                    CustomerName = cart.CustomerName ?? string.Empty,
                    TransactionDate = transactionDate,
                    TotalAmount = serverTotal,
                    AmountPaid = amountPaid,
                    Change = change,
                    StaffId = staffId,
                    Items = new List<SalesItem>()
                };

            _context.SalesTransactions.Add(transaction);
            await _context.SaveChangesAsync(); // to get TransactionId

            // Attach items (set TransactionId)
            foreach (var item in itemsToCreate)
            {
                item.TransactionId = transaction.TransactionId;
                transaction.Items.Add(item);
            }

            // Assign previously generated serials to the saved sale without changing their identity.
            foreach (var serialUnit in serialsToSell)
            {
                serialUnit.Status = "Sold";
                serialUnit.SalesTransactionId = transaction.TransactionId;
                serialUnit.SoldDate = DateTime.UtcNow;
            }

            // Update inventory and status with notification handling
            foreach (var product in affectedProducts)
            {
                var originalStatus = product.StockStatus;
                product.QuantityOnHand -= cart.Items.First(i => i.ProductId == product.ProductId).Quantity;
                product.LastSaleDate = DateTime.Now;

                product.StockStatus = StockHelper.GetStockStatus(product.QuantityOnHand);
                product.LastUpdated = DateTime.Now;

                // Notification on status transition (deduplicated: an unread alert
                // of the same type for this product is never duplicated)
                if (originalStatus == "Available" && product.StockStatus == "Low Stock")
                {
                    await _notificationService.CreateOnceAsync(product.ProductId, "LowStock",
                        $"Low stock for {product.ProductName} (Qty {product.QuantityOnHand}).", staffId);
                }
                else if (originalStatus == "Available" && product.StockStatus == "Out of Stock")
                {
                    await _notificationService.CreateOnceAsync(product.ProductId, "OutOfStock",
                        $"{product.ProductName} is out of stock.", staffId);
                }
                else if (originalStatus == "Low Stock" && product.StockStatus == "Out of Stock")
                {
                    await _notificationService.CreateOnceAsync(product.ProductId, "OutOfStock",
                        $"{product.ProductName} is out of stock.", staffId);
                }

                // Reorder is evaluated independently from the visual stock status:
                // QuantityOnHand <= ReorderLevel means a reorder notification is needed.
                if (product.QuantityOnHand <= product.ReorderLevel)
                {
                    await _notificationService.CreateOnceAsync(product.ProductId, "Reorder",
                        $"{product.ProductName} reached reorder level. Qty: {product.QuantityOnHand}.", staffId);
                }
            }

            await _context.SaveChangesAsync();

            // Activity log
            await _activityLogService.LogAsync(
                "Sale",
                "Sales",
                $"Receipt #{transaction.InvoiceNumber}, Total ₱{transaction.TotalAmount}",
                staffId);

            await tx.CommitAsync();
            return transaction;
        }
    }
}
