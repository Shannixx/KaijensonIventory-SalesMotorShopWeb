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
                if (product.StockStatus == "OutOfStock")
                    throw new InvalidOperationException($"Product {product.ProductName} is out of stock.");

                // Verify sufficient stock now
                if (product.QuantityOnHand < cartItem.Quantity)
                    throw new InvalidOperationException($"Only {product.QuantityOnHand} of {product.ProductName} are available.");

                // Snapshot price
                var unitPrice = product.Price;
                var subtotal = unitPrice * cartItem.Quantity;
                serverTotal += subtotal;

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
            var transaction = new SalesTransaction
            {
                InvoiceNumber = $"INV-{Guid.NewGuid():N}",
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

            // Update inventory and status with notification handling
            foreach (var product in affectedProducts)
            {
                var originalStatus = product.StockStatus;
                product.QuantityOnHand -= cart.Items.First(i => i.ProductId == product.ProductId).Quantity;
                product.LastSaleDate = DateTime.Now;

                if (product.QuantityOnHand == 0)
                    product.StockStatus = "OutOfStock";
                else if (product.QuantityOnHand <= product.ReorderLevel)
                    product.StockStatus = "Low";
                else
                    product.StockStatus = "Available";

                // Notification on status transition
                if (originalStatus == "Available" && product.StockStatus == "Low")
                {
                    await _notificationService.CreateAsync(product.ProductId, "LowStock",
                        $"Low stock for {product.ProductName} (Qty {product.QuantityOnHand}).");
                }
                else if (originalStatus == "Available" && product.StockStatus == "OutOfStock")
                {
                    await _notificationService.CreateAsync(product.ProductId, "OutOfStock",
                        $"{product.ProductName} is out of stock.");
                }
                else if (originalStatus == "Low" && product.StockStatus == "OutOfStock")
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
                $"Invoice {transaction.InvoiceNumber}, Total ₱{transaction.TotalAmount}",
                staffId);

            await tx.CommitAsync();
            return transaction;
        }
    }
}
