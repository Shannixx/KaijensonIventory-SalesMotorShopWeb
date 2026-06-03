using System.Text.Json;
using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class BackupsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public BackupsController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            // Validate session and role (Admin only)
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            string? staffRole = HttpContext.Session.GetString("StaffRole");
            
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }
            
            if (!string.Equals(staffRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }

            try
            {
                int pageSize = 10;
                IQueryable<Backup> query = _context.Backups.AsNoTracking();

                int total = await query.CountAsync();

                List<Backup> items = await query
                    .OrderByDescending(b => b.BackupDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);

                return View(items);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while loading backups. Please try again.";
                return View(new List<Backup>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create()
        {
            // Validate session and role (Admin only)
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            string? staffRole = HttpContext.Session.GetString("StaffRole");
            
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }
            
            if (!string.Equals(staffRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }

            try
            {
                string backupsDir = Path.Combine(_env.ContentRootPath, "App_Data", "Backups");
                Directory.CreateDirectory(backupsDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"backup_{timestamp}.json";
                string filePath = Path.Combine(backupsDir, fileName);

                var data = new
                {
                    Categories = await _context.Categories.AsNoTracking().ToListAsync(),
                    Suppliers = await _context.Suppliers.AsNoTracking().ToListAsync(),
                    Mechanics = await _context.Mechanics.AsNoTracking().ToListAsync(),
                    Products = await _context.Products.AsNoTracking().ToListAsync(),
                    Staff = await _context.Staff.AsNoTracking().ToListAsync(),
                    StockIns = await _context.StockIns.AsNoTracking().ToListAsync(),
                    Notifications = await _context.Notifications.AsNoTracking().ToListAsync(),
                    ActivityLogs = await _context.ActivityLogs.AsNoTracking().ToListAsync(),
                    BackedUpAt = DateTime.Now
                };

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                await System.IO.File.WriteAllTextAsync(filePath, json);

                _context.Backups.Add(new Backup
                {
                    BackupType = "Manual",
                    BackupFile = fileName,
                    BackupDate = DateTime.Now,
                    Status = "Success"
                });

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = staffId.Value,
                    Action = "Create Backup",
                    Module = "Backup",
                    Description = $"Manual backup created: {fileName}"
                });

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Backup created successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Backup failed: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            // Validate session and role (Admin only)
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            string? staffRole = HttpContext.Session.GetString("StaffRole");
            
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }
            
            if (!string.Equals(staffRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }

            try
            {
                Backup? backup = await _context.Backups.FindAsync(id);
                if (backup == null) return NotFound();

                TempData["InfoMessage"] = "Restore functionality is not yet implemented. This feature will allow restoring data from backup files.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while attempting to restore the backup. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            // Validate session and role (Admin only)
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            string? staffRole = HttpContext.Session.GetString("StaffRole");
            
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }
            
            if (!string.Equals(staffRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }

            try
            {
                Backup? backup = await _context.Backups.FindAsync(id);
                if (backup == null) return NotFound();

                string filePath = Path.Combine(_env.ContentRootPath, "App_Data", "Backups", backup.BackupFile);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                _context.Backups.Remove(backup);

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = staffId.Value,
                    Action = "Delete Backup",
                    Module = "Backup",
                    Description = $"Backup deleted: {backup.BackupFile}"
                });

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Backup deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the backup. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> Download(int id)
        {
            // Validate session and role (Admin only)
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            string? staffRole = HttpContext.Session.GetString("StaffRole");
            
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }
            
            if (!string.Equals(staffRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }

            try
            {
                Backup? backup = await _context.Backups.AsNoTracking().FirstOrDefaultAsync(b => b.BackupId == id);
                if (backup == null) return NotFound();

                string filePath = Path.Combine(_env.ContentRootPath, "App_Data", "Backups", backup.BackupFile);
                if (!System.IO.File.Exists(filePath)) return NotFound();

                byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                return File(fileBytes, "application/json", backup.BackupFile);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while downloading the backup. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
