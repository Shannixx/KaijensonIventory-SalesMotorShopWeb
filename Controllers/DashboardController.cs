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
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(ApplicationDbContext context, ILogger<DashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

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
                        .CountAsync(p => p.QuantityOnHand > 0 && p.QuantityOnHand < 5),
                    OutOfStockCount = await _context.Products
                        .CountAsync(p => p.QuantityOnHand <= 0),
                    LowStockRequireReorder = await _context.Products
                        .CountAsync(p => p.QuantityOnHand > 0 && p.QuantityOnHand < 5),
                    TotalInventoryValue = await _context.Products
                        .SumAsync(p => (decimal?)(p.Price * p.QuantityOnHand)) ?? 0,
                    TotalCategories = await _context.Categories.CountAsync(),
                    TotalSuppliers = await _context.Suppliers.CountAsync(),
                    TotalMechanics = await _context.Mechanics.CountAsync(),
                    PendingPOCount = await _context.PurchaseOrders
                        .CountAsync(p => p.Status == "Pending"),
                    RecentLowStockProducts = await _context.Products
                        .Where(p => p.QuantityOnHand > 0 && p.QuantityOnHand < 5)
                        .OrderBy(p => p.QuantityOnHand)
                        .Take(5)
                        .AsNoTracking()
                        .ToListAsync(),
                    RecentPurchaseOrders = await _context.PurchaseOrders
                        .Include(p => p.Supplier)
                        .OrderByDescending(p => p.CreatedDate)
                        .Take(5)
                        .AsNoTracking()
                        .ToListAsync(),
                    CategoryLabels = categoryDist.Select(c => c.Category).ToList(),
                    CategoryCounts = categoryDist.Select(c => c.Count).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading the dashboard.");
                TempData["ErrorMessage"] = "An error occurred while loading the dashboard. Please try again.";
                return View(new DashboardViewModel());
            }
        }
    }
}
