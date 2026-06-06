using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class InventoryHistoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InventoryHistoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? searchString, string? transactionType, int? productId, DateTime? dateFrom, DateTime? dateTo, int page = 1)
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                int pageSize = 20;
                IQueryable<InventoryTransaction> query = _context.InventoryTransactions
                    .Include(t => t.Product)
                    .Include(t => t.Staff)
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    string s = searchString.ToLower();
                    query = query.Where(t => (t.Product != null && t.Product.ProductName.ToLower().Contains(s))
                        || (t.Remarks != null && t.Remarks.ToLower().Contains(s)));
                }

                if (!string.IsNullOrWhiteSpace(transactionType))
                    query = query.Where(t => t.TransactionType == transactionType);

                if (productId.HasValue && productId.Value > 0)
                    query = query.Where(t => t.ProductId == productId.Value);

                if (dateFrom.HasValue)
                    query = query.Where(t => t.TransactionDate >= dateFrom.Value);

                if (dateTo.HasValue)
                    query = query.Where(t => t.TransactionDate <= dateTo.Value.Date.AddDays(1));

                int total = await query.CountAsync();
                List<InventoryTransaction> items = await query
                    .OrderByDescending(t => t.TransactionDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["CurrentFilter"] = searchString;
                ViewData["TransactionType"] = transactionType;
                ViewData["ProductId"] = productId;
                ViewData["DateFrom"] = dateFrom?.ToString("yyyy-MM-dd");
                ViewData["DateTo"] = dateTo?.ToString("yyyy-MM-dd");

                ViewBag.Products = new SelectList(
                    await _context.Products.AsNoTracking().OrderBy(p => p.ProductName).ToListAsync(),
                    "ProductId", "ProductName", productId);

                return View(items);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading inventory history. Please try again.";
                return View(new List<InventoryTransaction>());
            }
        }
    }
}
