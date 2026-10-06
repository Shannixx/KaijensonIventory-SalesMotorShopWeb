using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class MechanicsController : BaseController
{
    // Existing code omitted for brevity
    // ---------------------------------------------------------------------
    // Removed manual ToggleWorkStatus action – work status is now managed automatically.
    // Existing members follow below

        private readonly ApplicationDbContext _context;
        private readonly ILogger<MechanicsController> _logger;

        public MechanicsController(ApplicationDbContext context, ILogger<MechanicsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? searchString, string? statusFilter, string? workStatusFilter, int page = 1, bool archived = false)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;
            if (archived && !IsAdmin()) return Forbid();

            try
            {
                int pageSize = 10;
                var query = _context.Mechanics.AsNoTracking()
                    .Where(m => archived ? m.IsDeleted : !m.IsDeleted);

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    string s = searchString;
                    query = query.Where(m =>
                        m.MechanicId.ToString().Contains(s) ||
                        m.MechanicName.Contains(s) ||
                        (m.Specialization != null && m.Specialization.Contains(s)) ||
                        (m.ContactNumber != null && m.ContactNumber.Contains(s)));
                }

                if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All")
                {
                    query = query.Where(m => m.Status == statusFilter);
                }

                if (!string.IsNullOrWhiteSpace(workStatusFilter) && workStatusFilter != "All")
                {
                    query = query.Where(m => m.WorkStatus == workStatusFilter);
                }

                int total = await query.CountAsync();

                var mechanics = await query
                    .OrderBy(m => m.MechanicId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["CurrentFilter"] = searchString;
                ViewData["StatusFilter"] = statusFilter;
                ViewData["WorkStatusFilter"] = workStatusFilter;
                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewBag.ShowArchived = archived;

                return View(mechanics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading mechanics index.");
                TempData["ErrorMessage"] = "An error occurred while loading mechanics. Please try again.";
                return View(new List<Mechanic>());
            }
        }

        public async Task<IActionResult> Details(int? id, bool archived = false)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;
            if (archived && !IsAdmin()) return Forbid();
            if (id == null) return NotFound();

            try
            {
                var mechanic = await _context.Mechanics.AsNoTracking()
                    .Include(m => m.HiredByStaff)
                    .FirstOrDefaultAsync(m => m.MechanicId == id && (archived ? m.IsDeleted : !m.IsDeleted));
                if (mechanic == null) return NotFound();
                ViewBag.ShowArchived = archived;
                return View(mechanic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading mechanic details. Id: {MechanicId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading mechanic details. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        public IActionResult Create()
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;
            return View(new MechanicCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MechanicCreateViewModel model)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;

            // Validate only fields accepted from the form; employment/audit data is server-owned.
            if (ModelState.IsValid)
            {
                try
                {
                    var mechanic = new Mechanic
                    {
                        MechanicName = model.MechanicName,
                        Specialization = model.Specialization,
                        ContactNumber = model.ContactNumber,
                        Status = "Active",
                        WorkStatus = "Available",
                        DateHired = DateTime.UtcNow,
                        HiredBy = GetCurrentStaffId()
                    };

                    _context.Mechanics.Add(mechanic);
                    await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        Action = "Create Mechanic",
                        Module = "Mechanic",
                        Description = $"Created mechanic: {mechanic.MechanicName}",
                        StaffId = GetCurrentStaffId(),
                        Timestamp = DateTime.Now
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Mechanic created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating mechanic.");
                    TempData["ErrorMessage"] = "An error occurred while creating the mechanic. Please try again.";
                    return View(model);
                }
            }
            return View(model);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;
            if (id == null) return NotFound();

            try
            {
                var mechanic = await _context.Mechanics.FirstOrDefaultAsync(m => m.MechanicId == id && !m.IsDeleted);
                if (mechanic == null) return NotFound();
                return View(mechanic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading mechanic for edit. Id: {MechanicId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the mechanic. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MechanicId,MechanicName,Specialization,ContactNumber,Status")] Mechanic mechanic)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;
            var authRedirect = RedirectIfNotOwnerOrManager();
            if (authRedirect != null) return authRedirect;
            if (id != mechanic.MechanicId) return NotFound();
            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.Mechanics.FirstOrDefaultAsync(m => m.MechanicId == id && !m.IsDeleted);
                    if (existing == null) return NotFound();

                    existing.MechanicName = mechanic.MechanicName;
                    existing.Specialization = mechanic.Specialization;
                    existing.ContactNumber = mechanic.ContactNumber;
// Capture previous employment status before change
var previousStatus = existing.Status;

// Apply new status
existing.Status = mechanic.Status;

// Adjust WorkStatus based on transition rules
if (existing.Status == "Inactive")
{
    // Inactive mechanics must be Unavailable
    existing.WorkStatus = "Unavailable";
}
else // Active
{
    if (previousStatus == "Inactive")
    {
        // Reactivation: determine work status based on active service jobs
        bool hasActiveJob = await _context.ServiceJobs.AnyAsync(j => j.MechanicId == existing.MechanicId && j.Status == ServiceJob.StatusStillWorking);
        existing.WorkStatus = hasActiveJob ? "Working" : "Available";
    }
    else
    {
        // Preserve existing work status, but ensure not Unavailable
        if (existing.WorkStatus == "Unavailable")
            existing.WorkStatus = "Available";
    }
}

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        Action = "Edit Mechanic",
                        Module = "Mechanic",
                        Description = $"Edited mechanic: {mechanic.MechanicName}",
                        StaffId = GetCurrentStaffId(),
                        Timestamp = DateTime.Now
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Mechanic updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning(ex, "Concurrency error editing mechanic Id {MechanicId}", id);
                    if (!await _context.Mechanics.AnyAsync(m => m.MechanicId == id))
                        return NotFound();
                    TempData["ErrorMessage"] = "The mechanic was modified by another user. Please try again.";
                    return View(mechanic);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error editing mechanic Id {MechanicId}", id);
                    TempData["ErrorMessage"] = "An error occurred while updating the mechanic. Please try again.";
                    return View(mechanic);
                }
            }
            return View(mechanic);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;
            if (id == null) return NotFound();

            try
            {
                var mechanic = await _context.Mechanics.AsNoTracking()
                    .Include(m => m.HiredByStaff)
                    .FirstOrDefaultAsync(m => m.MechanicId == id && !m.IsDeleted);
                if (mechanic == null) return NotFound();

                return View(mechanic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading mechanic for delete. Id: {MechanicId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the mechanic. Please try again.";
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
                var mechanic = await _context.Mechanics.FirstOrDefaultAsync(m => m.MechanicId == id && !m.IsDeleted);
                if (mechanic == null) return NotFound();

                string name = mechanic.MechanicName;
                mechanic.IsDeleted = true;
                mechanic.DeletedAt = DateTime.UtcNow;
                mechanic.DeletedBy = GetCurrentStaffId();
                _context.ActivityLogs.Add(new ActivityLog
                {
                    Action = "Archive Mechanic",
                    Module = "Mechanic",
                    Description = $"Archived mechanic: {name} ({mechanic.MechanicId})",
                    StaffId = GetCurrentStaffId(),
                    Timestamp = DateTime.Now
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Mechanic archived successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting mechanic Id {MechanicId}", id);
                TempData["ErrorMessage"] = "An error occurred while deleting the mechanic. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            var mechanic = await _context.Mechanics.FirstOrDefaultAsync(m => m.MechanicId == id && m.IsDeleted);
            if (mechanic == null) return NotFound();

            mechanic.IsDeleted = false;
            mechanic.DeletedAt = null;
            mechanic.DeletedBy = null;
            _context.ActivityLogs.Add(new ActivityLog
            {
                Action = "Restore Mechanic",
                Module = "Mechanic",
                Description = $"Restored mechanic: {mechanic.MechanicName} ({mechanic.MechanicId})",
                StaffId = GetCurrentStaffId(),
                Timestamp = DateTime.Now
            });
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Mechanic restored successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
