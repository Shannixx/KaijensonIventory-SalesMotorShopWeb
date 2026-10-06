using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
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

        public async Task<IActionResult> Index(string? searchString, int page = 1, bool archived = false)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;
            if (archived && !IsAdmin()) return Forbid();

            try
            {
                int pageSize = 10;
                IQueryable<Service> query = _context.Services
                    .AsNoTracking()
                    .Where(s => archived ? s.IsDeleted : !s.IsDeleted);

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
                ViewBag.ShowArchived = archived;

                return View(services);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading services.");
                TempData["ErrorMessage"] = "An error occurred while loading services. Please try again.";
                return View(new List<Service>());
            }
        }

        public async Task<IActionResult> Details(int? id, bool archived = false)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;
            if (archived && !IsAdmin()) return Forbid();

            if (id == null) return NotFound();

            try
            {
                Service? service = await _context.Services
                    .AsNoTracking()
                    .Include(s => s.CreatedByStaff)
                    .FirstOrDefaultAsync(s => s.ServiceId == id && (archived ? s.IsDeleted : !s.IsDeleted));

                if (service == null) return NotFound();
                ViewBag.ShowArchived = archived;
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
            if (!IsAdmin()) return StatusCode(StatusCodes.Status403Forbidden);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ServiceName,ServicePrice")] ServiceFormViewModel model)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;
            if (!IsAdmin()) return StatusCode(StatusCodes.Status403Forbidden);

            // The shared form model requires a type for Edit, but this modal always
            // creates base services. Status and type cannot be posted to this action.
            model.IsAddOn = false;
            model.Status = "Active";
            ModelState.Remove(nameof(ServiceFormViewModel.IsAddOn));
            if (ModelState.IsValid && model.ServicePrice is decimal price && decimal.Round(price, 2) != price)
                ModelState.AddModelError(nameof(ServiceFormViewModel.ServicePrice), "Price must have no more than two decimal places.");

            if (ModelState.IsValid)
            {
                try
                {
                    // The Service model provides the base-service and Active defaults.
                    var service = new Service
                    {
                        ServiceName = model.ServiceName.Trim(),
                        ServicePrice = model.ServicePrice!.Value,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = GetCurrentStaffId()
                    };
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
                var errors = ModelState.Values.SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage)
                    .Where(message => !string.IsNullOrWhiteSpace(message));
                var errorMessage = string.Join(" ", errors);
                TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(errorMessage)
                    ? "Please provide a valid service name and price."
                    : errorMessage;
            }

            // No full-page create form exists: the Add Service modal on Index owns this POST.
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;
            if (!IsAdmin()) return StatusCode(StatusCodes.Status403Forbidden);

            if (id == null) return NotFound();

            try
            {
                Service? service = await _context.Services.FirstOrDefaultAsync(s => s.ServiceId == id && !s.IsDeleted);
                if (service == null) return NotFound();

                return View(new ServiceFormViewModel
                {
                    ServiceId = service.ServiceId,
                    ServiceName = service.ServiceName,
                    ServicePrice = service.ServicePrice,
                    Status = service.Status,
                    IsAddOn = service.IsAddOn
                });
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
        public async Task<IActionResult> Edit(int id, [Bind("ServiceId,ServiceName,ServicePrice,Status,IsAddOn")] ServiceFormViewModel model)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;
            if (!IsAdmin()) return StatusCode(StatusCodes.Status403Forbidden);

            if (id != model.ServiceId) return NotFound();

            try
            {
                Service? existing = await _context.Services.FirstOrDefaultAsync(s => s.ServiceId == id && !s.IsDeleted);
                if (existing == null) return NotFound();

                // The hidden type is only a consistency check, never an editable entity field.
                if (model.IsAddOn != existing.IsAddOn)
                {
                    return BadRequest("Service type cannot be changed after creation.");
                }

                if (!ModelState.IsValid) return View(model);

                existing.ServiceName = model.ServiceName.Trim();
                existing.ServicePrice = model.ServicePrice!.Value;
                existing.Status = model.Status;

                _context.ActivityLogs.Add(new ActivityLog
                {
                    Action = "Edit Service",
                    Module = "Service",
                    Description = $"Edited service: {existing.ServiceName}",
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
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating service. ServiceId: {ServiceId}", id);
                TempData["ErrorMessage"] = "An error occurred while updating the service. Please try again.";
            }

            return View(model);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            if (id == null) return NotFound();

            try
            {
                Service? service = await _context.Services
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ServiceId == id && !s.IsDeleted && (s.Status == "Active" || IsAdmin()));

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
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            try
            {
                Service? service = await _context.Services.FirstOrDefaultAsync(s => s.ServiceId == id && !s.IsDeleted);
                if (service == null) return NotFound();

                string name = service.ServiceName;

                service.IsDeleted = true;
                service.DeletedAt = DateTime.UtcNow;
                service.DeletedBy = GetCurrentStaffId();
                _context.ActivityLogs.Add(new ActivityLog
                {
                    Action = "Archive Service",
                    Module = "Service",
                    Description = $"Archived service: {name} ({service.ServiceId})",
                    StaffId = GetCurrentStaffId(),
                    Timestamp = DateTime.Now
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Service archived successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting service. ServiceId: {ServiceId}", id);
                TempData["ErrorMessage"] = "An error occurred while deleting the service. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            var service = await _context.Services.FirstOrDefaultAsync(s => s.ServiceId == id && s.IsDeleted);
            if (service == null) return NotFound();

            service.IsDeleted = false;
            service.DeletedAt = null;
            service.DeletedBy = null;
            _context.ActivityLogs.Add(new ActivityLog
            {
                Action = "Restore Service",
                Module = "Service",
                Description = $"Restored service: {service.ServiceName} ({service.ServiceId})",
                StaffId = GetCurrentStaffId(),
                Timestamp = DateTime.Now
            });
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Service restored successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
