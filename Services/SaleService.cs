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
            // Idempotency check
            var existing = await _context.SalesTransactions
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.CheckoutKey == checkoutKey);
            if (existing != null)
                return existing;

            // Begin serializable transaction
            await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            // Re‑read products and calculate totals
            decimal serverTotal = 0m;
            var itemsToCreate = new List<SalesItem>();
            var affectedProducts = new List<Product>();

            foreach (var cartItem in cart.Items)
            {
                // Validate quantity > 0
                if (cartItem.Quantity <= 0)
                    throw new InvalidOperationException($"Quantity must be greater than zero for product ID {cartItem.ProductId}.");

                var product = await _context.Products
                    .Where(p => p.ProductId == cartItem.ProductId)
                    .FirstOrDefaultAsync();

                if (product == null)
                    throw new InvalidOperationException($"Product with ID {cartItem.ProductId} not found.");

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

                // Serialized product validation
if (product.IsSerialized)
                 {
                     if (cart.SerialNumbers == null || !cart.SerialNumbers.TryGetValue(product.ProductId, out var serialList))
                         throw new InvalidOperationException($"Serial numbers are required for serialized product {product.ProductName}.");
                     // Normalize serial numbers: trim whitespace
                     var normalizedSerials = serialList.Select(s => s.Trim()).ToList();
                     // Reject empty or whitespace‑only serials after trimming
                     if (normalizedSerials.Any(s => string.IsNullOrWhiteSpace(s)))
                         throw new InvalidOperationException($"Serial numbers cannot be empty or whitespace for product {product.ProductName}.");
                     if (normalizedSerials.Count != cartItem.Quantity)
                         throw new InvalidOperationException($"Number of serials ({normalizedSerials.Count}) does not match quantity ({cartItem.Quantity}) for product {product.ProductName}.");
                     // Ensure serials are unique within this list after normalization
                     if (normalizedSerials.Distinct().Count() != normalizedSerials.Count)
                         throw new InvalidOperationException($"Duplicate serial numbers provided for product {product.ProductName}.");
                     // Replace original list with normalized for later processing
                     cart.SerialNumbers[product.ProductId] = normalizedSerials;
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
                var transactionDate = DateTime.Now;
                var monthStart = new DateTime(transactionDate.Year, transactionDate.Month, 1);
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
                    TransactionDate = DateTime.Now,
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

            // Create SerialUnit records for serialized products
            foreach (var cartItem in cart.Items)
            {
                var product = affectedProducts.First(p => p.ProductId == cartItem.ProductId);
                if (product.IsSerialized)
                {
                    var serialList = cart.SerialNumbers[product.ProductId];
                    foreach (var serial in serialList)
                    {
                        // Ensure serial is not already used in another sale
                        if (await _context.SerialUnits.AnyAsync(s => s.SerialNumber == serial))
                        {
                            throw new InvalidOperationException($"Serial number '{serial}' has already been used in another transaction.");
                        }
                        var serialUnit = new SerialUnit
                        {
                            SerialNumber = serial,
                            ProductId = product.ProductId,
                            SalesTransactionId = transaction.TransactionId,
                            Status = "Sold",
                            SoldDate = DateTime.Now,
                            CreatedDate = DateTime.Now
                        };
                        _context.SerialUnits.Add(serialUnit);
                    }
                }
            }

            // Update inventory and status with notification handling
            foreach (var product in affectedProducts)
            {
                var originalStatus = product.StockStatus;
                product.QuantityOnHand -= cart.Items.First(i => i.ProductId == product.ProductId).Quantity;
                product.LastSaleDate = DateTime.Now;

                product.StockStatus = StockHelper.GetStockStatus(product.QuantityOnHand);

                // Notification on status transition
                if (originalStatus == "Available" && product.StockStatus == "Low Stock")
                {
                    await _notificationService.CreateAsync(product.ProductId, "LowStock",
                        $"Low stock for {product.ProductName} (Qty {product.QuantityOnHand}).");
                }
                else if (originalStatus == "Available" && product.StockStatus == "Out of Stock")
                {
                    await _notificationService.CreateAsync(product.ProductId, "OutOfStock",
                        $"{product.ProductName} is out of stock.");
                }
                else if (originalStatus == "Low Stock" && product.StockStatus == "Out of Stock")
                {
                    await _notificationService.CreateAsync(product.ProductId, "OutOfStock",
                        $"{product.ProductName} is out of stock.");
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
