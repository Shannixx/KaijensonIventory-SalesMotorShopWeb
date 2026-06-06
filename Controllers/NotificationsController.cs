using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotificationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? searchString, string? alertType, bool? isRead, int page = 1)
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
                IQueryable<Notification> query = _context.Notifications
                    .Include(n => n.Product)
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                    query = query.Where(n => n.Message.Contains(searchString));

                if (!string.IsNullOrWhiteSpace(alertType))
                    query = query.Where(n => n.AlertType == alertType);

                if (isRead.HasValue)
                    query = query.Where(n => n.IsRead == isRead.Value);

                int total = await query.CountAsync();

                List<Notification> items = await query
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["CurrentFilter"] = searchString;
                ViewData["AlertType"] = alertType;
                ViewData["IsRead"] = isRead;

                return View(items);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading notifications. Please try again.";
                return View(new List<Notification>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
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
                Notification? notif = await _context.Notifications.FindAsync(id);
                if (notif == null) return NotFound();

                notif.IsRead = true;
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while marking the notification as read. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
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
                await _context.Notifications
                    .Where(n => !n.IsRead)
                    .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

                TempData["SuccessMessage"] = "All notifications marked as read.";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while marking all notifications as read. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetUnreadCount()
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue) return Json(0);

            try
            {
                int count = await _context.Notifications.CountAsync(n => !n.IsRead);
                return Json(count);
            }
            catch
            {
                return Json(0);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
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
                Notification? notif = await _context.Notifications.FindAsync(id);
                if (notif == null) return NotFound();

                _context.Notifications.Remove(notif);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Notification deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the notification. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
