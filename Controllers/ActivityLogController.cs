using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class ActivityLogController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ActivityLogController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? searchString, string? module, DateTime? dateFrom, DateTime? dateTo, int page = 1)
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

                if (!string.IsNullOrWhiteSpace(searchString))
                    query = query.Where(l => l.Description!.Contains(searchString) || l.Action.Contains(searchString));

                if (!string.IsNullOrWhiteSpace(module))
                    query = query.Where(l => l.Module == module);

                if (dateFrom.HasValue)
                    query = query.Where(l => l.Timestamp >= dateFrom.Value);
                if (dateTo.HasValue)
                    query = query.Where(l => l.Timestamp <= dateTo.Value.Date.AddDays(1));

                int total = await query.CountAsync();
                List<ActivityLog> logs = await query
                    .OrderByDescending(l => l.Timestamp)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["CurrentFilter"] = searchString;
                ViewData["Module"] = module;
                ViewData["DateFrom"] = dateFrom?.ToString("yyyy-MM-dd");
                ViewData["DateTo"] = dateTo?.ToString("yyyy-MM-dd");
                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);

                ViewBag.Modules = new SelectList(
                    await _context.ActivityLogs.Select(l => l.Module).Distinct().OrderBy(m => m).ToListAsync(),
                    module);

                return View(logs);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while loading activity logs. Please try again.";
                return View(new List<ActivityLog>());
            }
        }
    }
}
