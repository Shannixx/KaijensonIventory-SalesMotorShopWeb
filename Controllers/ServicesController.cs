using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class ServicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ServicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? searchString, int page = 1)
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
                IQueryable<Service> query = _context.Services
                    .Include(s => s.Category)
                    .Include(s => s.Mechanic)
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    query = query.Where(s => s.ServiceName.Contains(searchString));
                }

                int total = await query.CountAsync();

                List<Service> services = await query
                    .OrderBy(s => s.ServiceName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["CurrentFilter"] = searchString;
                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);

                return View(services);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while loading services. Please try again.";
                return View(new List<Service>());
            }
        }

        public async Task<IActionResult> Details(int? id)
        {
            // Validate session
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            if (id == null) return NotFound();

            try
            {
                Service? service = await _context.Services
                    .Include(s => s.Category)
                    .Include(s => s.Mechanic)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ServiceId == id);

                if (service == null) return NotFound();

                return View(service);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while loading service details. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> Create()
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
                ViewBag.CategoryId = new SelectList(await _context.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync(), "CategoryId", "CategoryName");
                ViewBag.MechanicId = new SelectList(await _context.Mechanics.AsNoTracking().OrderBy(m => m.MechanicName).ToListAsync(), "MechanicId", "MechanicName");
                return View();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while loading the create service form. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ServiceName,ServicePrice,CategoryId,MechanicId")] Service service)
        {
            // Validate session
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Services.Add(service);
                    await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        Action = "Create",
                        Module = "Services",
                        Description = $"Created service: {service.ServiceName}",
                        StaffId = staffId,
                        Timestamp = DateTime.Now
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Service created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "An error occurred while creating the service. Please try again.";
                }
            }

            ViewBag.CategoryId = new SelectList(await _context.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync(), "CategoryId", "CategoryName", service.CategoryId);
            ViewBag.MechanicId = new SelectList(await _context.Mechanics.AsNoTracking().OrderBy(m => m.MechanicName).ToListAsync(), "MechanicId", "MechanicName", service.MechanicId);
            return View(service);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            // Validate session
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            if (id == null) return NotFound();

            try
            {
                Service? service = await _context.Services.FindAsync(id);
                if (service == null) return NotFound();

                ViewBag.CategoryId = new SelectList(await _context.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync(), "CategoryId", "CategoryName", service.CategoryId);
                ViewBag.MechanicId = new SelectList(await _context.Mechanics.AsNoTracking().OrderBy(m => m.MechanicName).ToListAsync(), "MechanicId", "MechanicName", service.MechanicId);
                return View(service);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while loading the service for editing. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ServiceId,ServiceName,ServicePrice,CategoryId,MechanicId")] Service service)
        {
            // Validate session
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            if (id != service.ServiceId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Services.Update(service);
                    await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        Action = "Edit",
                        Module = "Services",
                        Description = $"Edited service: {service.ServiceName}",
                        StaffId = staffId,
                        Timestamp = DateTime.Now
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Service updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Services.AnyAsync(s => s.ServiceId == id))
                        return NotFound();

                    TempData["ErrorMessage"] = "The service was modified by another user. Please try again.";
                    ViewBag.CategoryId = new SelectList(await _context.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync(), "CategoryId", "CategoryName", service.CategoryId);
                    ViewBag.MechanicId = new SelectList(await _context.Mechanics.AsNoTracking().OrderBy(m => m.MechanicName).ToListAsync(), "MechanicId", "MechanicName", service.MechanicId);
                    return View(service);
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "An error occurred while updating the service. Please try again.";
                }
            }

            ViewBag.CategoryId = new SelectList(await _context.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync(), "CategoryId", "CategoryName", service.CategoryId);
            ViewBag.MechanicId = new SelectList(await _context.Mechanics.AsNoTracking().OrderBy(m => m.MechanicName).ToListAsync(), "MechanicId", "MechanicName", service.MechanicId);
            return View(service);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            // Validate session
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            if (id == null) return NotFound();

            try
            {
                // Note: Service and ServiceTransaction are separate entities with no direct relationship
                // Service is a catalog item, ServiceTransaction is an actual service performed
                // So we don't check for related ServiceTransactions when deleting a Service

                Service? service = await _context.Services
                    .Include(s => s.Category)
                    .Include(s => s.Mechanic)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ServiceId == id);

                if (service == null) return NotFound();

                return View(service);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while loading the service for deletion. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
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
                // Note: Service and ServiceTransaction are separate entities with no direct relationship
                // Service is a catalog item, ServiceTransaction is an actual service performed
                // So we don't check for related ServiceTransactions when deleting a Service

                Service? service = await _context.Services.FindAsync(id);
                if (service == null) return NotFound();

                string name = service.ServiceName;

                _context.Services.Remove(service);
                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    Action = "Delete",
                    Module = "Services",
                    Description = $"Deleted service: {name}",
                    StaffId = staffId,
                    Timestamp = DateTime.Now
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Service deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the service. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
