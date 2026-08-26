using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class MechanicsController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MechanicsController> _logger;

        public MechanicsController(ApplicationDbContext context, ILogger<MechanicsController> logger)
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
                IQueryable<Mechanic> query = _context.Mechanics.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    string s = searchString;
                    query = query.Where(m =>
                        m.MechanicId.ToString().Contains(s) ||
                        m.MechanicName.Contains(s) ||
                        (m.Specialization != null && m.Specialization.Contains(s)) ||
                        (m.ContactNumber != null && m.ContactNumber.Contains(s)) ||
                        (m.Address != null && m.Address.Contains(s))
                    );
                }

                int total = await query.CountAsync();

                List<Mechanic> mechanics = await query
                    .OrderBy(m => m.MechanicId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["CurrentFilter"] = searchString;
                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);

                return View(mechanics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading mechanics.");
                TempData["ErrorMessage"] = "An error occurred while loading mechanics. Please try again.";
                return View(new List<Mechanic>());
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
                Mechanic? mechanic = await _context.Mechanics.AsNoTracking().FirstOrDefaultAsync(m => m.MechanicId == id);
                if (mechanic == null) return NotFound();

                return View(mechanic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading mechanic details. MechanicId: {MechanicId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading mechanic details. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        public IActionResult Create()
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MechanicName,Specialization,ContactNumber,Address")] Mechanic mechanic)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            // Server-side validation
            if (string.IsNullOrWhiteSpace(mechanic.MechanicName))
            {
                ModelState.AddModelError("MechanicName", "Mechanic name is required.");
            }
            if (string.IsNullOrWhiteSpace(mechanic.Specialization))
            {
                ModelState.AddModelError("Specialization", "Specialization is required.");
            }
            if (string.IsNullOrWhiteSpace(mechanic.ContactNumber))
            {
                ModelState.AddModelError("ContactNumber", "Contact number is required.");
            }
            if (string.IsNullOrWhiteSpace(mechanic.Address))
            {
                ModelState.AddModelError("Address", "Address is required.");
            }

            if (ModelState.IsValid)
            {
                try
                {
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
                    _logger.LogError(ex, "Error occurred while creating mechanic.");
                    TempData["ErrorMessage"] = "An error occurred while creating the mechanic. Please try again.";
                    return View(mechanic);
                }
            }

            return View(mechanic);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            if (id == null) return NotFound();

            try
            {
                Mechanic? mechanic = await _context.Mechanics.FindAsync(id);
                if (mechanic == null) return NotFound();

                return View(mechanic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading mechanic for editing. MechanicId: {MechanicId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading mechanic for editing. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MechanicId,MechanicName,Specialization,ContactNumber,Address")] Mechanic mechanic)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            if (id != mechanic.MechanicId) return NotFound();

            // Server-side validation
            if (string.IsNullOrWhiteSpace(mechanic.MechanicName))
            {
                ModelState.AddModelError("MechanicName", "Mechanic name is required.");
            }
            if (string.IsNullOrWhiteSpace(mechanic.Specialization))
            {
                ModelState.AddModelError("Specialization", "Specialization is required.");
            }
            if (string.IsNullOrWhiteSpace(mechanic.ContactNumber))
            {
                ModelState.AddModelError("ContactNumber", "Contact number is required.");
            }
            if (string.IsNullOrWhiteSpace(mechanic.Address))
            {
                ModelState.AddModelError("Address", "Address is required.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Mechanics.Update(mechanic);
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
                    _logger.LogWarning(ex, "Concurrency conflict while updating mechanic. MechanicId: {MechanicId}", id);
                    if (!await _context.Mechanics.AnyAsync(m => m.MechanicId == id))
                        return NotFound();

                    TempData["ErrorMessage"] = "The mechanic was modified by another user. Please try again.";
                    return View(mechanic);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while updating mechanic. MechanicId: {MechanicId}", id);
                    TempData["ErrorMessage"] = "An error occurred while updating the mechanic. Please try again.";
                    return View(mechanic);
                }
            }

            return View(mechanic);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            if (id == null) return NotFound();

            try
            {
                Mechanic? mechanic = await _context.Mechanics.AsNoTracking().FirstOrDefaultAsync(m => m.MechanicId == id);
                if (mechanic == null) return NotFound();

                bool hasServices = await _context.ServiceJobs.AnyAsync(j => j.MechanicId == id);
                if (hasServices)
                {
                    TempData["ErrorMessage"] = "Cannot delete mechanic. This mechanic has associated service job records.";
                    return RedirectToAction(nameof(Index));
                }

                return View(mechanic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading mechanic for deletion. MechanicId: {MechanicId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading mechanic for deletion. Please try again.";
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
                Mechanic? mechanic = await _context.Mechanics.FindAsync(id);
                if (mechanic == null) return NotFound();

                bool hasServices = await _context.ServiceJobs.AnyAsync(j => j.MechanicId == id);
                if (hasServices)
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
                _logger.LogError(ex, "Error occurred while deleting mechanic. MechanicId: {MechanicId}", id);
                TempData["ErrorMessage"] = "An error occurred while deleting the mechanic. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

    }
}
