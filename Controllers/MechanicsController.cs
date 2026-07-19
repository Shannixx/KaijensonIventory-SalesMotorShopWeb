using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class MechanicsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MechanicsController(ApplicationDbContext context)
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
                    .OrderBy(m => m.MechanicName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["CurrentFilter"] = searchString;
                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);

                return View(mechanics);
            }
            catch
            {
                // Log the exception in a real application
                TempData["ErrorMessage"] = "An error occurred while loading mechanics. Please try again.";
                return View(new List<Mechanic>());
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
                Mechanic? mechanic = await _context.Mechanics.AsNoTracking().FirstOrDefaultAsync(m => m.MechanicId == id);
                if (mechanic == null) return NotFound();

                return View(mechanic);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading mechanic details. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        public IActionResult Create()
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MechanicName,Specialization,ContactNumber,Address")] Mechanic mechanic)
        {
            // Validate session
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            // Server-side validation
            if (string.IsNullOrWhiteSpace(mechanic.MechanicName))
            {
                ModelState.AddModelError("MechanicName", "Mechanic name is required.");
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
                        StaffId = staffId,
                        Timestamp = DateTime.Now
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Mechanic created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    TempData["ErrorMessage"] = "An error occurred while creating the mechanic. Please try again.";
                    return View(mechanic);
                }
            }

            return View(mechanic);
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
                Mechanic? mechanic = await _context.Mechanics.FindAsync(id);
                if (mechanic == null) return NotFound();

                return View(mechanic);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading mechanic for editing. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MechanicId,MechanicName,Specialization,ContactNumber,Address")] Mechanic mechanic)
        {
            // Validate session
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            if (id != mechanic.MechanicId) return NotFound();

            // Server-side validation
            if (string.IsNullOrWhiteSpace(mechanic.MechanicName))
            {
                ModelState.AddModelError("MechanicName", "Mechanic name is required.");
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
                        StaffId = staffId,
                        Timestamp = DateTime.Now
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Mechanic updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Mechanics.AnyAsync(m => m.MechanicId == id))
                        return NotFound();

                    TempData["ErrorMessage"] = "The mechanic was modified by another user. Please try again.";
                    return View(mechanic);
                }
                catch
                {
                    TempData["ErrorMessage"] = "An error occurred while updating the mechanic. Please try again.";
                    return View(mechanic);
                }
            }

            return View(mechanic);
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
                Mechanic? mechanic = await _context.Mechanics.AsNoTracking().FirstOrDefaultAsync(m => m.MechanicId == id);
                if (mechanic == null) return NotFound();

                bool hasServices = await _context.Services.AnyAsync(s => s.MechanicId == id);
                if (hasServices)
                {
                    TempData["ErrorMessage"] = "Cannot delete mechanic. This mechanic has associated service records.";
                    return RedirectToAction(nameof(Index));
                }

                return View(mechanic);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading mechanic for deletion. Please try again.";
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
                Mechanic? mechanic = await _context.Mechanics.FindAsync(id);
                if (mechanic == null) return NotFound();

                bool hasServices = await _context.Services.AnyAsync(s => s.MechanicId == id);
                if (hasServices)
                {
                    TempData["ErrorMessage"] = "Cannot delete mechanic. This mechanic has associated service records.";
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
                    StaffId = staffId,
                    Timestamp = DateTime.Now
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Mechanic deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the mechanic. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

    }
}
