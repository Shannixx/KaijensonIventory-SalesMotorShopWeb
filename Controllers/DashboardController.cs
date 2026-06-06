using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var today = DateTime.Today;

                var todaySales = await _context.SalesTransactions
                    .Where(t => t.TransactionDate.Date == today)
                    .ToListAsync();

                decimal todaySalesAmount = todaySales.Sum(t => t.TotalAmount);
                decimal todayCOGS = 0;
                foreach (var sale in todaySales)
                {
                    var items = await _context.SalesItems
                        .Include(i => i.Product)
                        .Where(i => i.TransactionId == sale.TransactionId)
                        .ToListAsync();
                    todayCOGS += items.Sum(i => (i.Quantity * (i.Product?.AverageCost ?? 0)));
                }

                var allSales = await _context.SalesTransactions.ToListAsync();
                decimal totalRevenue = allSales.Sum(t => t.TotalAmount);
                decimal totalCOGS = 0;
                foreach (var sale in allSales)
                {
                    var items = await _context.SalesItems
                        .Include(i => i.Product)
                        .Where(i => i.TransactionId == sale.TransactionId)
                        .ToListAsync();
                    totalCOGS += items.Sum(i => (i.Quantity * (i.Product?.AverageCost ?? 0)));
                }
                decimal totalProfit = totalRevenue - totalCOGS;
                decimal profitMargin = totalRevenue > 0 ? (totalProfit / totalRevenue) * 100 : 0;

                var sixMonthsAgo = today.AddMonths(-5);
                var monthlySalesData = await _context.SalesTransactions
                    .Where(t => t.TransactionDate >= sixMonthsAgo)
                    .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.Month,
                        Total = g.Sum(t => t.TotalAmount)
                    })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month)
                    .AsNoTracking()
                    .ToListAsync();

                var chartLabels = new List<string>();
                var chartSalesData = new List<decimal>();
                var chartProfitData = new List<decimal>();

                foreach (var ms in monthlySalesData)
                {
                    chartLabels.Add(new DateTime(ms.Year, ms.Month, 1).ToString("MMM yyyy"));

                    var monthSales = await _context.SalesTransactions
                        .Where(t => t.TransactionDate.Year == ms.Year && t.TransactionDate.Month == ms.Month)
                        .ToListAsync();

                    decimal monthCOGS = 0;
                    foreach (var sale in monthSales)
                    {
                        var items = await _context.SalesItems
                            .Include(i => i.Product)
                            .Where(i => i.TransactionId == sale.TransactionId)
                            .ToListAsync();
                        monthCOGS += items.Sum(i => (i.Quantity * (i.Product?.AverageCost ?? 0)));
                    }

                    chartSalesData.Add(ms.Total);
                    chartProfitData.Add(ms.Total - monthCOGS);
                }

                var topSelling = await _context.SalesItems
                    .GroupBy(i => i.Product!.ProductName)
                    .Select(g => new ProductSalesInfo
                    {
                        ProductName = g.Key,
                        TotalQuantity = g.Sum(i => i.Quantity),
                        TotalRevenue = g.Sum(i => i.Subtotal)
                    })
                    .OrderByDescending(x => x.TotalQuantity)
                    .Take(5)
                    .AsNoTracking()
                    .ToListAsync();

                var categoryDist = await _context.Products
                    .GroupBy(p => p.Category!.CategoryName)
                    .Select(g => new { Category = g.Key, Count = g.Count() })
                    .AsNoTracking()
                    .ToListAsync();

                var viewModel = new DashboardViewModel
                {
                    TotalProducts = await _context.Products.CountAsync(),
                    TotalCategories = await _context.Categories.CountAsync(),
                    TotalSuppliers = await _context.Suppliers.CountAsync(),
                    TotalMechanics = await _context.Mechanics.CountAsync(),
                    LowStockCount = await _context.Products
                        .CountAsync(p => p.QuantityOnHand <= p.ReorderLevel && p.QuantityOnHand > 0),
                    OutOfStockCount = await _context.Products
                        .CountAsync(p => p.QuantityOnHand <= 0),
                    TodaySalesCount = todaySales.Count,
                    TodaySalesAmount = todaySalesAmount,
                    TodayProfit = todaySalesAmount - todayCOGS,
                    TotalInventoryValue = await _context.Products
                        .SumAsync(p => (decimal?)(p.Price * p.QuantityOnHand)) ?? 0,
                    TotalRevenue = totalRevenue,
                    TotalCOGS = totalCOGS,
                    TotalProfit = totalProfit,
                    ProfitMargin = profitMargin,
                    RecentLowStockProducts = await _context.Products
                        .Where(p => p.QuantityOnHand <= p.ReorderLevel && p.QuantityOnHand > 0)
                        .OrderBy(p => p.QuantityOnHand)
                        .Take(5)
                        .AsNoTracking()
                        .ToListAsync(),
                    RecentSales = await _context.SalesTransactions
                        .Include(t => t.Staff)
                        .OrderByDescending(t => t.TransactionDate)
                        .Take(5)
                        .AsNoTracking()
                        .ToListAsync(),
                    OngoingServices = await _context.ServiceTransactions
                        .Where(s => s.Status != "Completed")
                        .OrderByDescending(s => s.Date)
                        .Take(5)
                        .AsNoTracking()
                        .ToListAsync(),
                    ChartLabels = chartLabels,
                    ChartSalesData = chartSalesData,
                    ChartProfitData = chartProfitData,
                    CategoryLabels = categoryDist.Select(c => c.Category).ToList(),
                    CategoryCounts = categoryDist.Select(c => c.Count).ToList(),
                    TopSellingProducts = topSelling
                };

                return View(viewModel);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading the dashboard. Please try again.";
                return View(new DashboardViewModel());
            }
        }
    }
}
