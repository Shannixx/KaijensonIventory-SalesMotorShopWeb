using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class DashboardController : BaseController
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
                var categoryDist = await _context.Products
                    .GroupBy(p => p.Category!.CategoryName)
                    .Select(g => new { Category = g.Key, Count = g.Count() })
                    .AsNoTracking()
                    .ToListAsync();

                var viewModel = new DashboardViewModel
                {
                    TotalProducts = await _context.Products.CountAsync(),
                    LowStockCount = await _context.Products
                        .CountAsync(p => p.QuantityOnHand <= p.ReorderLevel && p.QuantityOnHand > 0),
                    OutOfStockCount = await _context.Products
                        .CountAsync(p => p.QuantityOnHand <= 0),
                    LowStockRequireReorder = await _context.Products
                        .CountAsync(p => p.QuantityOnHand <= p.ReorderLevel),
                    TotalInventoryValue = await _context.Products
                        .SumAsync(p => (decimal?)(p.Price * p.QuantityOnHand)) ?? 0,
                    TotalCategories = await _context.Categories.CountAsync(),
                    TotalSuppliers = await _context.Suppliers.CountAsync(),
                    TotalMechanics = await _context.Mechanics.CountAsync(),
                    RecentLowStockProducts = await _context.Products
                        .Where(p => p.QuantityOnHand <= p.ReorderLevel && p.QuantityOnHand > 0)
                        .OrderBy(p => p.QuantityOnHand)
                        .Take(5)
                        .AsNoTracking()
                        .ToListAsync(),
                    CategoryLabels = categoryDist.Select(c => c.Category).ToList(),
                    CategoryCounts = categoryDist.Select(c => c.Count).ToList()
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
