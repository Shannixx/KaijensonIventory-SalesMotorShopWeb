using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class ServicesController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ServicesController> _logger;

        public ServicesController(ApplicationDbContext context, ILogger<ServicesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? searchString, int page = 1)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            try
            {
                int pageSize = 10;
                IQueryable<Service> query = _context.Services
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    query = query.Where(s => s.ServiceName.Contains(searchString));
                }

                int total = await query.CountAsync();

                List<Service> services = await query
                    .OrderBy(s => s.ServiceId)
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
                _logger.LogError(ex, "Error occurred while loading services.");
                TempData["ErrorMessage"] = "An error occurred while loading services. Please try again.";
                return View(new List<Service>());
            }
        }

        public async Task<IActionResult> Details(int? id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            if (id == null) return NotFound();

            try
            {
                Service? service = await _context.Services
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ServiceId == id);

                if (service == null) return NotFound();

                return View(service);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading service details. ServiceId: {ServiceId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading service details. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // Creation happens through the "Add Service" modal on the Index page;
        // this GET only exists to redirect any direct navigation back to the list.
        public IActionResult Create()
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ServiceName,ServicePrice,Description,DurationMinutes")] Service service)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            // Server-side validation
            if (string.IsNullOrWhiteSpace(service.ServiceName))
            {
                ModelState.AddModelError("ServiceName", "Service name is required.");
            }
            if (service.ServicePrice < 0)
            {
                ModelState.AddModelError("ServicePrice", "Price cannot be negative.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Set audit fields and defaults
                    service.Status = "Active";
                    service.CreatedAt = DateTime.UtcNow;
                    service.CreatedBy = GetCurrentStaffId();
                    // Duration will be provided by user input
                    // CategoryId stays null: the Add Service form only collects ServiceName and ServicePrice.
                    _context.Services.Add(service);
                    await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        Action = "Create Service",
                        Module = "Service",
                        Description = $"Created service: {service.ServiceName}",
                        StaffId = GetCurrentStaffId(),
                        Timestamp = DateTime.Now
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Service created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while creating service.");
                    TempData["ErrorMessage"] = "An error occurred while creating the service. Please try again.";
                }
            }
            else if (!TempData.ContainsKey("ErrorMessage"))
            {
                TempData["ErrorMessage"] = "Please provide a valid service name and price.";
            }

            // No full-page create form exists: the Add Service modal on Index owns this POST.
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            if (id == null) return NotFound();

            try
            {
                Service? service = await _context.Services.FindAsync(id);
                if (service == null) return NotFound();

                return View(service);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading service for editing. ServiceId: {ServiceId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the service for editing. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ServiceId,ServiceName,ServicePrice,DurationMinutes,Status,Description")] Service service)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            if (id != service.ServiceId) return NotFound();

            // Server-side validation
            if (string.IsNullOrWhiteSpace(service.ServiceName))
            {
                ModelState.AddModelError("ServiceName", "Service name is required.");
            }
            if (service.ServicePrice < 0)
            {
                ModelState.AddModelError("ServicePrice", "Price cannot be negative.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    Service? existing = await _context.Services.FindAsync(id);
                    if (existing == null) return NotFound();
existing.ServiceName = service.ServiceName;
                     existing.ServicePrice = service.ServicePrice;
                     existing.DurationMinutes = service.DurationMinutes;
                     existing.Status = service.Status;
                     // Persist description changes
                     existing.Description = service.Description;
                     await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        Action = "Edit Service",
                        Module = "Service",
                        Description = $"Edited service: {service.ServiceName}",
                        StaffId = GetCurrentStaffId(),
                        Timestamp = DateTime.Now
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Service updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning(ex, "Concurrency conflict while updating service. ServiceId: {ServiceId}", id);
                    if (!await _context.Services.AnyAsync(s => s.ServiceId == id))
                        return NotFound();

                    TempData["ErrorMessage"] = "The service was modified by another user. Please try again.";
                    return View(service);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while updating service. ServiceId: {ServiceId}", id);
                    TempData["ErrorMessage"] = "An error occurred while updating the service. Please try again.";
                }
            }

            return View(service);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            if (id == null) return NotFound();

            try
            {
                // Note: Service and ServiceTransaction are separate entities with no direct relationship
                // Service is a catalog item, ServiceTransaction is an actual service performed
                // So we don't check for related ServiceTransactions when deleting a Service

                Service? service = await _context.Services
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ServiceId == id);

                if (service == null) return NotFound();

                return View(service);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading service for deletion. ServiceId: {ServiceId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the service for deletion. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            try
            {
                // Note: Service and ServiceTransaction are separate entities with no direct relationship
                // Service is a catalog item, ServiceTransaction is an actual service performed
                // So we don't check for related ServiceTransactions when deleting a Service

                Service? service = await _context.Services.FindAsync(id);
                if (service == null) return NotFound();

                // Prevent deletion if there are existing ServiceJobs referencing this service
                bool hasJobs = await _context.ServiceJobs.AnyAsync(j => j.ServiceId == id);
                if (hasJobs)
                {
                    TempData["ErrorMessage"] = "This service cannot be deleted because it has existing service records.";
                    return RedirectToAction(nameof(Index));
                }

                string name = service.ServiceName;

                _context.Services.Remove(service);
                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    Action = "Delete Service",
                    Module = "Service",
                    Description = $"Deleted service: {name}",
                    StaffId = GetCurrentStaffId(),
                    Timestamp = DateTime.Now
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Service deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting service. ServiceId: {ServiceId}", id);
                TempData["ErrorMessage"] = "An error occurred while deleting the service. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
