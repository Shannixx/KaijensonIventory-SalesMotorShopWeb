using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class DemoDataController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DemoDataController> _logger;

        public DemoDataController(ApplicationDbContext context, ILogger<DemoDataController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // POST: /DemoData/Reset
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reset()
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            try
            {
                // Delete dependent data in order to avoid FK violations
                _context.ServiceHistories.RemoveRange(_context.ServiceHistories);
                _context.ServiceJobs.RemoveRange(_context.ServiceJobs);
                _context.Services.RemoveRange(_context.Services);
                _context.Products.RemoveRange(_context.Products);
                _context.DeliveryItems.RemoveRange(_context.DeliveryItems);
                _context.Deliveries.RemoveRange(_context.Deliveries);
                _context.PurchaseOrderItems.RemoveRange(_context.PurchaseOrderItems);
                _context.PurchaseOrders.RemoveRange(_context.PurchaseOrders);
                _context.SalesItems.RemoveRange(_context.SalesItems);
                _context.SalesTransactions.RemoveRange(_context.SalesTransactions);
                _context.Mechanics.RemoveRange(_context.Mechanics);
                _context.Brands.RemoveRange(_context.Brands);
                _context.Suppliers.RemoveRange(_context.Suppliers);
                _context.Categories.RemoveRange(_context.Categories);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Demo data has been cleared.";
                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting demo data.");
                TempData["ErrorMessage"] = "An error occurred while resetting demo data.";
                return RedirectToAction("Index", "Dashboard");
            }
        }
    }
}
