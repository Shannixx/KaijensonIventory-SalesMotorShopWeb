using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
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

        public async Task<IActionResult> Index(string? searchString, string? statusFilter, string? workStatusFilter, int page = 1)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;

            try
            {
                int pageSize = 10;
                var query = _context.Mechanics.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    string s = searchString;
                    query = query.Where(m =>
                        m.MechanicId.ToString().Contains(s) ||
                        m.MechanicName.Contains(s) ||
                        (m.Specialization != null && m.Specialization.Contains(s)) ||
                        (m.ContactNumber != null && m.ContactNumber.Contains(s)) ||
                        (m.EmailAddress != null && m.EmailAddress.Contains(s)) ||
                        (m.Address != null && m.Address.Contains(s)));
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

                return View(mechanics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading mechanics index.");
                TempData["ErrorMessage"] = "An error occurred while loading mechanics. Please try again.";
                return View(new List<Mechanic>());
            }
        }

        public async Task<IActionResult> Details(int? id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;
            if (id == null) return NotFound();

            try
            {
                var mechanic = await _context.Mechanics.AsNoTracking()
                    .Include(m => m.HiredByStaff)
                    .FirstOrDefaultAsync(m => m.MechanicId == id);
                if (mechanic == null) return NotFound();
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
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MechanicName,Specialization,ContactNumber,EmailAddress,Address,YearsOfExperience")] Mechanic mechanic)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;

            // Server-side validation (basic required fields handled by data annotations)
            if (ModelState.IsValid)
            {
                try
                {
                    // Set automatic fields
                    mechanic.Status = "Active";
                    mechanic.WorkStatus = "Available";
                    mechanic.DateHired = DateTime.UtcNow;
                    mechanic.HiredBy = GetCurrentStaffId();

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
                    return View(mechanic);
                }
            }
            return View(mechanic);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;
            if (id == null) return NotFound();

            try
            {
                var mechanic = await _context.Mechanics.FindAsync(id);
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
        public async Task<IActionResult> Edit(int id, [Bind("MechanicId,MechanicName,Specialization,ContactNumber,EmailAddress,Address,YearsOfExperience,Status")] Mechanic mechanic)
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
                    var existing = await _context.Mechanics.FirstOrDefaultAsync(m => m.MechanicId == id);
                    if (existing == null) return NotFound();

                    existing.MechanicName = mechanic.MechanicName;
                    existing.Specialization = mechanic.Specialization;
                    existing.ContactNumber = mechanic.ContactNumber;
                    existing.EmailAddress = mechanic.EmailAddress;
                    existing.Address = mechanic.Address;
                    existing.YearsOfExperience = mechanic.YearsOfExperience;
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

                    await _context.SaveChangesAsync();

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
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;
            if (id == null) return NotFound();

            try
            {
                var mechanic = await _context.Mechanics.AsNoTracking()
                    .Include(m => m.HiredByStaff)
                    .FirstOrDefaultAsync(m => m.MechanicId == id);
                if (mechanic == null) return NotFound();

                bool hasJobs = await _context.ServiceJobs.AnyAsync(j => j.MechanicId == id);
                if (hasJobs)
                {
                    TempData["ErrorMessage"] = "Cannot delete mechanic. This mechanic has associated service job records.";
                    return RedirectToAction(nameof(Index));
                }

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
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;

            try
            {
                var mechanic = await _context.Mechanics.FindAsync(id);
                if (mechanic == null) return NotFound();

                bool hasJobs = await _context.ServiceJobs.AnyAsync(j => j.MechanicId == id);
                if (hasJobs)
                {
                    TempData["ErrorMessage"] = "Cannot delete mechanic. This mechanic has associated service job records.";
                    return RedirectToAction(nameof(Index));
                }

                string name = mechanic.MechanicName;
                _context.Mechanics.Remove(mechanic);
                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    Action = "Delete Mechanic",
                    Module = "Mechanic",
                    Description = $"Deleted mechanic: {name}",
                    StaffId = GetCurrentStaffId(),
                    Timestamp = DateTime.Now
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Mechanic deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting mechanic Id {MechanicId}", id);
                TempData["ErrorMessage"] = "An error occurred while deleting the mechanic. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
