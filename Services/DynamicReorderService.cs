using KaijensonIventory_SalesMotorShopWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public class DynamicReorderService
    {
        private readonly ApplicationDbContext _context;

        public DynamicReorderService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task RecalculateProductAsync(int productId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null || !product.UseAutoReorder) return;

            var cutoff = DateTime.Now.AddDays(-90);

            var transactions = await _context.InventoryTransactions
                .Where(t => t.ProductId == productId
                         && t.TransactionDate >= cutoff
                         && (t.TransactionType == "Sale" || t.TransactionType == "SaleReversal"))
                .ToListAsync();

            int saleQty = transactions
                .Where(t => t.TransactionType == "Sale")
                .Sum(t => Math.Abs(t.Quantity));

            int reversalQty = transactions
                .Where(t => t.TransactionType == "SaleReversal")
                .Sum(t => t.Quantity);

            int netSold = saleQty - reversalQty;
            if (netSold < 0) netSold = 0;

            var firstSaleDate = transactions
                .Where(t => t.TransactionType == "Sale")
                .OrderBy(t => t.TransactionDate)
                .FirstOrDefault()?.TransactionDate;

            double daysSinceFirstSale;
            if (firstSaleDate.HasValue)
            {
                daysSinceFirstSale = Math.Max((DateTime.Now - firstSaleDate.Value).TotalDays, 1);
            }
            else
            {
                daysSinceFirstSale = 1;
            }

            double denominator = Math.Max(daysSinceFirstSale, 7);

            decimal averageDailySales = (decimal)netSold / (decimal)denominator;

            product.ReorderLevel = (int)Math.Max(1, Math.Ceiling(averageDailySales * product.LeadTimeDays));
            product.LastRecalcDate = DateTime.Now;
            StockStatusService.UpdateStockStatus(product);

            await _context.SaveChangesAsync();
        }

        public async Task RecalculateAllAsync()
        {
            var autoProductIds = await _context.Products
                .Where(p => p.UseAutoReorder)
                .Select(p => p.ProductId)
                .ToListAsync();

            foreach (var id in autoProductIds)
            {
                await RecalculateProductAsync(id);
            }
        }

        public async Task<decimal> GetCurrentAverageDailySalesAsync(int productId)
        {
            var cutoff = DateTime.Now.AddDays(-90);

            var transactions = await _context.InventoryTransactions
                .Where(t => t.ProductId == productId
                         && t.TransactionDate >= cutoff
                         && (t.TransactionType == "Sale" || t.TransactionType == "SaleReversal"))
                .ToListAsync();

            int saleQty = transactions
                .Where(t => t.TransactionType == "Sale")
                .Sum(t => Math.Abs(t.Quantity));

            int reversalQty = transactions
                .Where(t => t.TransactionType == "SaleReversal")
                .Sum(t => t.Quantity);

            int netSold = Math.Max(saleQty - reversalQty, 0);

            var firstSaleDate = transactions
                .Where(t => t.TransactionType == "Sale")
                .OrderBy(t => t.TransactionDate)
                .FirstOrDefault()?.TransactionDate;

            double daysSinceFirstSale;
            if (firstSaleDate.HasValue)
            {
                daysSinceFirstSale = Math.Max((DateTime.Now - firstSaleDate.Value).TotalDays, 1);
            }
            else
            {
                daysSinceFirstSale = 1;
            }

            double denominator = Math.Max(daysSinceFirstSale, 7);

            return denominator > 0 ? Math.Round((decimal)netSold / (decimal)denominator, 2) : 0;
        }
    }
}
