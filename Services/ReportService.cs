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
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;
        public ReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<InventoryReportViewModel> GetInventoryReportAsync(DateTime start, DateTime end)
        {
            var items = await _context.Products
                .Include(p => p.Category)
                .Select(p => new InventoryReportItemViewModel
                {
                    ProductName = p.ProductName,
                    CategoryName = p.Category.CategoryName,
                    QuantityOnHand = p.QuantityOnHand,
                    StockStatus = p.StockStatus,
                    ReorderLevel = p.ReorderLevel
                })
                .ToListAsync();
            return new InventoryReportViewModel { Items = items };
        }

        public async Task<SalesPerformanceReportViewModel> GetSalesPerformanceReportAsync(DateTime start, DateTime end)
        {
var startInclusive = start.Date;
            var endExclusive = end.Date.AddDays(1);
            var query = _context.SalesTransactions
                 .Where(t => t.TransactionDate >= startInclusive && t.TransactionDate < endExclusive);

            var transactionCount = await query.CountAsync();
            var totalQuantity = await query.SelectMany(t => t.Items).SumAsync(i => i.Quantity);
            var totalRevenue = await query.SumAsync(t => t.TotalAmount);

            return new SalesPerformanceReportViewModel
            {
                TransactionCount = transactionCount,
                TotalQuantitySold = totalQuantity,
                TotalRevenue = totalRevenue
            };
        }

        public async Task<RevenueReportViewModel> GetRevenueReportAsync(DateTime start, DateTime end)
        {
var startInclusive = start.Date;
            var endExclusive = end.Date.AddDays(1);
            var revenue = await _context.SalesTransactions
                 .Where(t => t.TransactionDate >= startInclusive && t.TransactionDate < endExclusive)
                 .SumAsync(t => t.TotalAmount);
            return new RevenueReportViewModel { TotalRevenue = revenue };
        }

        public async Task<List<MostSoldProductViewModel>> GetMostSoldProductsAsync(DateTime start, DateTime end)
        {
var startInclusive = start.Date;
            var endExclusive = end.Date.AddDays(1);
            var data = await _context.SalesTransactions
                 .Where(t => t.TransactionDate >= startInclusive && t.TransactionDate < endExclusive)
                 .SelectMany(t => t.Items)
                .GroupBy(i => i.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    QuantitySold = g.Sum(i => i.Quantity),
                    Revenue = g.Sum(i => i.Quantity * i.UnitPrice),
                    UnitPrice = g.Sum(i => i.Quantity * i.UnitPrice) / (decimal)g.Sum(i => i.Quantity)
                })
                .OrderByDescending(x => x.QuantitySold)
                .Take(100)
                .ToListAsync();

            var products = await _context.Products
                .Where(p => data.Select(d => d.ProductId).Contains(p.ProductId))
                .ToDictionaryAsync(p => p.ProductId, p => p.ProductName);

            return data.Select(d => new MostSoldProductViewModel
            {
                ProductName = products.ContainsKey(d.ProductId) ? products[d.ProductId] : "[Deleted]",
                QuantitySold = d.QuantitySold,
                Revenue = d.Revenue,
                UnitPrice = d.UnitPrice
            }).ToList();
        }

        public async Task<List<StockMovementViewModel>> GetStockMovementsAsync(DateTime start, DateTime end)
        {
            // Combine PurchaseOrder items (incoming) and SalesTransaction items (outgoing)
var startInclusive = start.Date;
            var endExclusive = end.Date.AddDays(1);
            var purchases = await _context.DeliveryItems
                .Where(di => di.ReceivedDate >= startInclusive && di.ReceivedDate < endExclusive)
                .Select(di => new StockMovementViewModel
                {
                    Date = di.ReceivedDate,
                    ProductName = di.PurchaseOrderItem.Product.ProductName,
                    MovementType = "Purchase",
                    Quantity = di.ReceivedQuantity,
                    Reference = di.Delivery.PurchaseOrder.PurchaseOrderNumber
                })
                .ToListAsync();

var sales = await _context.SalesItems
                .Where(s => s.Transaction.TransactionDate >= startInclusive && s.Transaction.TransactionDate < endExclusive)
                .Select(s => new StockMovementViewModel
                {
                    Date = s.Transaction.TransactionDate,
                    ProductName = s.Product.ProductName,
                    MovementType = "Sale",
                    Quantity = -s.Quantity,
                    Reference = s.Transaction.InvoiceNumber
                })
                .ToListAsync();

            var combined = purchases.Concat(sales).OrderBy(m => m.Date).ToList();
            return combined;
        }

public async Task<List<SerialNumberReportViewModel>> GetSerialNumberReportAsync(DateTime start, DateTime end)
        {
            var startInclusive = start.Date;
            var endExclusive = end.Date.AddDays(1);
            var data = await _context.SerialUnits
                .Include(s => s.Product)
                .Include(s => s.SalesTransaction)
                .Where(s => (s.SalesTransaction != null && s.SalesTransaction.TransactionDate >= startInclusive && s.SalesTransaction.TransactionDate < endExclusive) ||
                            (s.SalesTransaction == null && s.CreatedDate >= startInclusive && s.CreatedDate < endExclusive))
                .Select(s => new SerialNumberReportViewModel
                {
                    SerialNumber = s.SerialNumber,
                    ProductName = s.Product.ProductName,
                    Status = s.Status,
                    SaleId = s.SalesTransaction != null ? s.SalesTransaction.TransactionId : (int?)null,
                    SaleDate = s.SalesTransaction != null ? s.SalesTransaction.TransactionDate : (DateTime?)null
                })
                .ToListAsync();
            return data;
        }

public async Task<decimal> GetTotalInventoryValueAsync(DateTime start, DateTime end, int? productId = null, int? categoryId = null)
        {
            // Calculate inventory value, applying optional product and category filters
            var query = _context.Products.AsQueryable();
            if (productId.HasValue)
                query = query.Where(p => p.ProductId == productId.Value);
            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);
            var total = await query.SumAsync(p => p.QuantityOnHand * p.AverageCost);
            return total;
        }

        public async Task<int> GetLowStockItemCountAsync(DateTime start, DateTime end, int? productId = null, int? categoryId = null)
        {
            var query = _context.Products.AsQueryable();
            if (productId.HasValue)
                query = query.Where(p => p.ProductId == productId.Value);
            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);
            var count = await query.CountAsync(p => p.QuantityOnHand <= p.ReorderLevel);
            return count;
        }

        public async Task<List<LowStockAlertViewModel>> GetLowStockAlertsAsync(DateTime start, DateTime end, int? productId = null, int? categoryId = null)
        {
            var query = _context.Products.AsQueryable();
            if (productId.HasValue)
                query = query.Where(p => p.ProductId == productId.Value);
            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);
            var alerts = await query
                .Where(p => p.QuantityOnHand <= p.ReorderLevel)
                .Select(p => new LowStockAlertViewModel
                {
                    ProductName = p.ProductName,
                    QuantityOnHand = p.QuantityOnHand,
                    ReorderLevel = p.ReorderLevel,
                    StockStatus = p.StockStatus
                })
                .ToListAsync();
            return alerts;
        }

        public async Task<List<RevenueTrendItemViewModel>> GetRevenueTrendAsync(DateTime start, DateTime end, int? productId = null, int? categoryId = null)
        {
            var startInclusive = start.Date;
            var endExclusive = end.Date.AddDays(1);
            // Build base query for sales items within date range
            var itemsQuery = _context.SalesItems
                .Where(si => si.Transaction.TransactionDate >= startInclusive && si.Transaction.TransactionDate < endExclusive)
                .AsQueryable();
            if (productId.HasValue)
                itemsQuery = itemsQuery.Where(si => si.ProductId == productId.Value);
            if (categoryId.HasValue)
                itemsQuery = itemsQuery.Where(si => si.Product.CategoryId == categoryId.Value);
            var data = await itemsQuery
                .GroupBy(si => si.Transaction.TransactionDate.Date)
                .Select(g => new RevenueTrendItemViewModel
                {
                    Period = g.Key,
                    Revenue = g.Sum(si => si.Quantity * si.UnitPrice),
                    UnitsSold = g.Sum(si => si.Quantity)
                })
                .OrderBy(r => r.Period)
                .ToListAsync();
            return data;
        }

        public async Task<List<SalesByCategoryViewModel>> GetSalesByCategoryAsync(DateTime start, DateTime end, int? productId = null, int? categoryId = null)
        {
            var startInclusive = start.Date;
            var endExclusive = end.Date.AddDays(1);
            var itemsQuery = _context.SalesItems
                .Where(si => si.Transaction.TransactionDate >= startInclusive && si.Transaction.TransactionDate < endExclusive)
                .AsQueryable();
            if (productId.HasValue)
                itemsQuery = itemsQuery.Where(si => si.ProductId == productId.Value);
            if (categoryId.HasValue)
                itemsQuery = itemsQuery.Where(si => si.Product.CategoryId == categoryId.Value);
            var data = await itemsQuery
                .Include(i => i.Product)
                .ThenInclude(p => p.Category)
                .GroupBy(i => i.Product.Category.CategoryName)
                .Select(g => new SalesByCategoryViewModel
                {
                    CategoryName = g.Key,
                    Revenue = g.Sum(i => i.Quantity * i.UnitPrice),
                    UnitsSold = g.Sum(i => i.Quantity)
                })
                .OrderByDescending(s => s.Revenue)
                .ToListAsync();
            return data;
        }

    }
}
