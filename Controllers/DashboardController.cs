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
            // Validate session
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var today = DateTime.Today;

                var viewModel = new DashboardViewModel
                {
                    TotalProducts = await _context.Products.CountAsync(),
                    TotalCategories = await _context.Categories.CountAsync(),
                    TotalSuppliers = await _context.Suppliers.CountAsync(),
                    TotalMechanics = await _context.Mechanics.CountAsync(),

                    LowStockCount = await _context.Products
                        .CountAsync(p => p.QuantityOnHand <= p.ReorderLevel && p.QuantityOnHand > 0),

                    TodaySalesAmount = await _context.SalesTransactions
                        .Where(t => t.TransactionDate.Date == today)
                        .SumAsync(t => (decimal?)t.TotalAmount) ?? 0,

                    TotalInventoryValue = await _context.Products
                        .SumAsync(p => (decimal?)(p.Price * p.QuantityOnHand)) ?? 0,

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
                };

                var sixMonthsAgo = today.AddMonths(-5);
                var monthlySales = await _context.SalesTransactions
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

                foreach (var ms in monthlySales)
                {
                    viewModel.ChartLabels.Add(new DateTime(ms.Year, ms.Month, 1).ToString("MMM yyyy"));
                    viewModel.ChartSalesData.Add(ms.Total);
                }

                var categoryDist = await _context.Products
                    .GroupBy(p => p.Category!.CategoryName)
                    .Select(g => new { Category = g.Key, Count = g.Count() })
                    .AsNoTracking()
                    .ToListAsync();

                foreach (var cd in categoryDist)
                {
                    viewModel.CategoryLabels.Add(cd.Category);
                    viewModel.CategoryCounts.Add(cd.Count);
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while loading the dashboard. Please try again.";
                return View(new DashboardViewModel());
            }
        }
    }
}
