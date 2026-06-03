using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            // Validate session
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            return View();
        }

        public async Task<IActionResult> SalesSummary(DateTime? dateFrom, DateTime? dateTo, int page = 1)
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
                int pageSize = 10;
                IQueryable<SalesTransaction> query = _context.SalesTransactions
                    .Include(t => t.Staff)
                    .AsNoTracking();

                if (dateFrom.HasValue)
                    query = query.Where(t => t.TransactionDate >= dateFrom.Value);
                if (dateTo.HasValue)
                    query = query.Where(t => t.TransactionDate <= dateTo.Value.Date.AddDays(1));

                int total = await query.CountAsync();
                var items = await query
                    .OrderByDescending(t => t.TransactionDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["DateFrom"] = dateFrom?.ToString("yyyy-MM-dd");
                ViewData["DateTo"] = dateTo?.ToString("yyyy-MM-dd");
                return View(items);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while generating the sales summary report. Please try again.";
                return View(new List<SalesTransaction>());
            }
        }

        public async Task<IActionResult> InventorySummary(int? categoryId, int page = 1)
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
                int pageSize = 10;
                IQueryable<Product> query = _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .AsNoTracking();

                if (categoryId.HasValue && categoryId.Value > 0)
                    query = query.Where(p => p.CategoryId == categoryId.Value);

                int total = await query.CountAsync();
                var items = await query
                    .OrderBy(p => p.ProductName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["CategoryId"] = categoryId;

                ViewBag.Categories = new SelectList(
                    await _context.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync(),
                    "CategoryId", "CategoryName", categoryId);

                return View(items);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while generating the inventory summary report. Please try again.";
                return View(new List<Product>());
            }
        }

        public async Task<IActionResult> ServiceSummary(DateTime? dateFrom, DateTime? dateTo, string? status, int page = 1)
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
                int pageSize = 10;
                IQueryable<ServiceTransaction> query = _context.ServiceTransactions
                    .Include(s => s.Mechanic)
                    .AsNoTracking();

                if (dateFrom.HasValue)
                    query = query.Where(s => s.Date >= dateFrom.Value);
                if (dateTo.HasValue)
                    query = query.Where(s => s.Date <= dateTo.Value.Date.AddDays(1));
                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(s => s.Status == status);

                int total = await query.CountAsync();
                var items = await query
                    .OrderByDescending(s => s.Date)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["DateFrom"] = dateFrom?.ToString("yyyy-MM-dd");
                ViewData["DateTo"] = dateTo?.ToString("yyyy-MM-dd");
                ViewData["Status"] = status;
                return View(items);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while generating the service summary report. Please try again.";
                return View(new List<ServiceTransaction>());
            }
        }

        public async Task<IActionResult> ActivitySummary(DateTime? dateFrom, DateTime? dateTo, string? module, int page = 1)
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
                int pageSize = 20;
                IQueryable<ActivityLog> query = _context.ActivityLogs
                    .Include(l => l.Staff)
                    .AsNoTracking();

                if (dateFrom.HasValue)
                    query = query.Where(l => l.Timestamp >= dateFrom.Value);
                if (dateTo.HasValue)
                    query = query.Where(l => l.Timestamp <= dateTo.Value.Date.AddDays(1));
                if (!string.IsNullOrWhiteSpace(module))
                    query = query.Where(l => l.Module == module);

                int total = await query.CountAsync();
                var items = await query
                    .OrderByDescending(l => l.Timestamp)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["DateFrom"] = dateFrom?.ToString("yyyy-MM-dd");
                ViewData["DateTo"] = dateTo?.ToString("yyyy-MM-dd");
                ViewData["Module"] = module;

                ViewBag.Modules = new SelectList(
                    await _context.ActivityLogs.Select(l => l.Module).Distinct().OrderBy(m => m).ToListAsync(),
                    module);

                return View(items);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while generating the activity summary report. Please try again.";
                return View(new List<ActivityLog>());
            }
        }
    }
}
