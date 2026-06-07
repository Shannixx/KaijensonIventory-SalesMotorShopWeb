using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class InventoryHealthController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InventoryHealthController> _logger;

        public InventoryHealthController(ApplicationDbContext context, ILogger<InventoryHealthController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            if (!IsSessionValid()) return RedirectToLogin();

            try
            {
                var products = await _context.Products.AsNoTracking().ToListAsync();
                var transactions = await _context.InventoryTransactions.AsNoTracking().ToListAsync();

                var productHealth = products.Select(p =>
                {
                    int calculatedStock = transactions
                        .Where(t => t.ProductId == p.ProductId)
                        .Sum(t => t.Quantity);
                    return new ProductHealthInfo
                    {
                        ProductId = p.ProductId,
                        ProductName = p.ProductName,
                        CurrentStock = p.QuantityOnHand,
                        CalculatedStock = calculatedStock,
                        TransactionCount = transactions.Count(t => t.ProductId == p.ProductId),
                        AverageCost = p.AverageCost,
                        ReorderLevel = p.ReorderLevel
                    };
                }).OrderBy(p => p.HasDiscrepancy ? 0 : 1).ThenBy(p => p.ProductName).ToList();

                int openingCount = transactions.Count(t => t.TransactionType == "Opening Balance");
                int stockInCount = transactions.Count(t => t.TransactionType == "StockIn");
                int saleCount = transactions.Count(t => t.TransactionType == "Sale");
                int reversalCount = transactions.Count(t => t.TransactionType == "SaleReversal");
                int serviceCount = transactions.Count(t => t.TransactionType == "ServiceUse");
                int poCount = transactions.Count(t => t.TransactionType == "PO Receiving");
                int adjCount = transactions.Count(t => t.TransactionType == "Adjustment");

                return View(new InventoryHealthViewModel
                {
                    Products = productHealth,
                    TotalChecked = productHealth.Count,
                    DiscrepancyCount = productHealth.Count(p => p.HasDiscrepancy),
                    TotalTransactions = transactions.Count,
                    OpeningBalanceCount = openingCount,
                    StockInCount = stockInCount,
                    SaleCount = saleCount,
                    SaleReversalCount = reversalCount,
                    ServiceUseCount = serviceCount,
                    POReceivingCount = poCount,
                    AdjustmentCount = adjCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in inventory health check");
                TempData["ErrorMessage"] = "An error occurred while checking inventory health.";
                return View(new InventoryHealthViewModel());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RebuildBalances()
        {
            if (!IsSessionValid()) return RedirectToLogin();
            if (!IsAdmin())
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var products = await _context.Products.ToListAsync();
                var transactions = await _context.InventoryTransactions.AsNoTracking().ToListAsync();

                int updated = 0;
                foreach (var product in products)
                {
                    int calculatedStock = transactions
                        .Where(t => t.ProductId == product.ProductId)
                        .Sum(t => t.Quantity);

                    if (product.QuantityOnHand != calculatedStock)
                    {
                        _context.InventoryTransactions.Add(new InventoryTransaction
                        {
                            ProductId = product.ProductId,
                            TransactionType = "Adjustment",
                            Quantity = calculatedStock - product.QuantityOnHand,
                            UnitCost = product.AverageCost,
                            StaffId = GetCurrentStaffId(),
                            TransactionDate = DateTime.Now,
                            Remarks = $"Auto-adjustment: QtyOnHand was {product.QuantityOnHand}, corrected to {calculatedStock}"
                        });

                        product.QuantityOnHand = calculatedStock;
                        product.StockStatus = calculatedStock <= 0
                            ? "Out of Stock"
                            : calculatedStock <= product.ReorderLevel
                                ? "Low Stock"
                                : "Available";
                        updated++;
                    }
                }

                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = GetCurrentStaffId(),
                    Action = "Rebuild Inventory",
                    Module = "Inventory",
                    Description = $"Inventory balances rebuilt from transaction history. {updated} products adjusted.",
                    Timestamp = DateTime.Now
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Inventory balances rebuilt. {updated} product(s) adjusted. Adjustment transactions created.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rebuilding inventory balances");
                TempData["ErrorMessage"] = "An error occurred while rebuilding inventory balances.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAdjustment(int productId, int adjustmentQuantity, string reason)
        {
            if (!IsSessionValid()) return RedirectToLogin();
            if (!IsAdmin())
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var product = await _context.Products.FindAsync(productId);
                if (product == null) return NotFound();

                product.QuantityOnHand += adjustmentQuantity;
                product.StockStatus = product.QuantityOnHand <= 0
                    ? "Out of Stock"
                    : product.QuantityOnHand <= product.ReorderLevel
                        ? "Low Stock"
                        : "Available";

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = productId,
                    TransactionType = "Adjustment",
                    Quantity = adjustmentQuantity,
                    UnitCost = product.AverageCost,
                    StaffId = GetCurrentStaffId(),
                    TransactionDate = DateTime.Now,
                    Remarks = $"Manual adjustment: {reason} ({(adjustmentQuantity >= 0 ? "+" : "")}{adjustmentQuantity} units)"
                });

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Inventory adjusted for {product.ProductName}. New quantity: {product.QuantityOnHand}";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating inventory adjustment");
                TempData["ErrorMessage"] = "An error occurred while adjusting inventory.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
