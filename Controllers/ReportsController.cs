using System;
using System.Threading.Tasks;
using KaijensonIventory_SalesMotorShopWeb.Services;
using KaijensonIventory_SalesMotorShopWeb.Data;
using Microsoft.EntityFrameworkCore;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class ReportsController : BaseController
    {
        private readonly IReportService _reportService;
        private readonly ApplicationDbContext _context;
public ReportsController(IReportService reportService, ApplicationDbContext context)
            {
                _reportService = reportService;
                _context = context;
            }

        // GET: /Reports
        public async Task<IActionResult> Index(ReportFilterViewModel filter)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;
            if (!IsOwnerOrManager())
            {
                TempData["ErrorMessage"] = "Access denied. Manager or Owner required.";
                return RedirectToAction("Index", "Dashboard");
            }

            // Ensure defaults if not provided
            if (filter.StartDate == default) filter.StartDate = DateTime.Today.AddMonths(-1);
            if (filter.EndDate == default) filter.EndDate = DateTime.Today;

            // Validate dates
            if (filter.StartDate > filter.EndDate)
            {
                ModelState.AddModelError(string.Empty, "Start Date must be before End Date.");
                // Repopulate selections for view
                ViewBag.Products = await _context.Products.OrderBy(p => p.ProductName).ToListAsync();
                ViewBag.Categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();
                var errorViewModel = new ReportsPageViewModel { Filter = filter };
                return View(errorViewModel);
            }

            // Populate optional filter selections
            ViewBag.Products = await _context.Products.OrderBy(p => p.ProductName).ToListAsync();
            ViewBag.Categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();

            // Use half‑open date range
            var start = filter.StartDate.Date;
            var end = filter.EndDate.Date;

            var inventory = await _reportService.GetInventoryReportAsync(start, end);
            var mostSold = await _reportService.GetMostSoldProductsAsync(start, end);
            var stockMovements = await _reportService.GetStockMovementsAsync(start, end);
            var serials = await _reportService.GetSerialNumberReportAsync(start, end);

            // Sales Performance & Revenue with optional product/category filters
            SalesPerformanceReportViewModel salesPerf;
            RevenueReportViewModel revenue;

            var startInclusive = start;
            var endExclusive = end.AddDays(1);
            var salesQuery = _context.SalesTransactions
                .Where(t => t.TransactionDate >= startInclusive && t.TransactionDate < endExclusive);

            if (filter.ProductId.HasValue || filter.CategoryId.HasValue)
            {
                var productIdsQuery = _context.Products.AsQueryable();
                if (filter.ProductId.HasValue)
                    productIdsQuery = productIdsQuery.Where(p => p.ProductId == filter.ProductId.Value);
                if (filter.CategoryId.HasValue)
                    productIdsQuery = productIdsQuery.Where(p => p.CategoryId == filter.CategoryId.Value);
                var productIds = await productIdsQuery.Select(p => p.ProductId).ToListAsync();

                var salesItems = _context.SalesItems
                    .Where(si => productIds.Contains(si.ProductId) && si.Transaction.TransactionDate >= startInclusive && si.Transaction.TransactionDate < endExclusive);

                var transactionIds = await salesItems.Select(si => si.TransactionId).Distinct().ToListAsync();
                var transactionCount = await _context.SalesTransactions
                    .Where(t => transactionIds.Contains(t.TransactionId)).CountAsync();
                var totalQuantity = await salesItems.SumAsync(si => si.Quantity);
                var totalRevenue = await salesItems.SumAsync(si => si.Quantity * si.UnitPrice);

                salesPerf = new SalesPerformanceReportViewModel
                {
                    TransactionCount = transactionCount,
                    TotalQuantitySold = totalQuantity,
                    TotalRevenue = totalRevenue
                };
                revenue = new RevenueReportViewModel { TotalRevenue = totalRevenue };
            }
            else
            {
                salesPerf = await _reportService.GetSalesPerformanceReportAsync(start, end);
                revenue = await _reportService.GetRevenueReportAsync(start, end);
            }

            // Apply optional product filter to other reports
            if (filter.ProductId.HasValue)
            {
                var product = await _context.Products.FindAsync(filter.ProductId.Value);
                if (product != null)
                {
                    var productName = product.ProductName;
                    inventory.Items = inventory.Items.Where(i => i.ProductName == productName).ToList();
                    mostSold = mostSold.Where(p => p.ProductName == productName).ToList();
                    stockMovements = stockMovements.Where(m => m.ProductName == productName).ToList();
                    serials = serials.Where(s => s.ProductName == productName).ToList();
                }
            }

            // Apply optional category filter
            if (filter.CategoryId.HasValue)
            {
                var category = await _context.Categories.FindAsync(filter.CategoryId.Value);
                if (category != null)
                {
                    var categoryName = category.CategoryName;
                    inventory.Items = inventory.Items.Where(i => i.CategoryName == categoryName).ToList();

                    var categoryProductNames = await _context.Products
                        .Where(p => p.CategoryId == filter.CategoryId.Value)
                        .Select(p => p.ProductName)
                        .ToListAsync();
                    mostSold = mostSold.Where(p => categoryProductNames.Contains(p.ProductName)).ToList();
                    stockMovements = stockMovements.Where(m => categoryProductNames.Contains(m.ProductName)).ToList();
                    serials = serials.Where(s => categoryProductNames.Contains(s.ProductName)).ToList();
                }
            }

            // Serial number filter
            if (!string.IsNullOrWhiteSpace(filter.SerialNumber))
            {
                var trimmed = filter.SerialNumber.Trim();
                serials = serials.Where(s => string.Equals(s.SerialNumber, trimmed, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var viewModel = new ReportsPageViewModel
            {
                Filter = filter,
                InventoryReport = inventory,
                SalesPerformanceReport = salesPerf,
                RevenueReport = revenue,
                MostSoldProducts = mostSold,
                StockMovements = stockMovements,
                SerialNumberReport = serials
            };

            return View(viewModel);
        }
    }
}
