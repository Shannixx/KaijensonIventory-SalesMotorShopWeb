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
            catch (Exception ex)
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
            catch (Exception ex)
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

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Mechanics.Add(mechanic);
                    await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        Action = "Create",
                        Module = "Mechanics",
                        Description = $"Created mechanic: {mechanic.MechanicName}",
                        StaffId = staffId,
                        Timestamp = DateTime.Now
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Mechanic created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
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
            catch (Exception ex)
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

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Mechanics.Update(mechanic);
                    await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        Action = "Edit",
                        Module = "Mechanics",
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
                catch (Exception ex)
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
                // Check if mechanic has any service transactions
                bool hasServiceTransactions = await _context.ServiceTransactions.AnyAsync(st => st.MechanicId == id);
                if (hasServiceTransactions)
                {
                    TempData["ErrorMessage"] = "Cannot delete mechanic. This mechanic has existing service transactions.";
                    return RedirectToAction(nameof(Index));
                }

                Mechanic? mechanic = await _context.Mechanics.AsNoTracking().FirstOrDefaultAsync(m => m.MechanicId == id);
                if (mechanic == null) return NotFound();

                return View(mechanic);
            }
            catch (Exception ex)
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
                // Check if mechanic has any service transactions
                bool hasServiceTransactions = await _context.ServiceTransactions.AnyAsync(st => st.MechanicId == id);
                if (hasServiceTransactions)
                {
                    TempData["ErrorMessage"] = "Cannot delete mechanic. This mechanic has existing service transactions.";
                    return RedirectToAction(nameof(Index));
                }

                Mechanic? mechanic = await _context.Mechanics.FindAsync(id);
                if (mechanic == null) return NotFound();

                string name = mechanic.MechanicName;

                _context.Mechanics.Remove(mechanic);
                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    Action = "Delete",
                    Module = "Mechanics",
                    Description = $"Deleted mechanic: {name}",
                    StaffId = staffId,
                    Timestamp = DateTime.Now
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Mechanic deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the mechanic. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSelected([FromForm] int[] ids)
        {
            // Validate session
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
                return Json(new { success = false, message = "Session expired. Please log in again." });

            if (ids == null || ids.Length == 0)
                return Json(new { success = false, message = "No items selected." });

            try
            {
                var referencedIds = await _context.ServiceTransactions
                    .Where(st => ids.Contains(st.MechanicId))
                    .Select(st => st.MechanicId)
                    .Distinct()
                    .ToListAsync();

                var validIds = ids.Where(id => !referencedIds.Contains(id)).ToArray();
                var skipped = referencedIds.Intersect(ids).Count();

                if (validIds.Length == 0)
                    return Json(new { success = false, message = "All selected mechanics have existing service transactions and cannot be deleted." });

                var mechanics = await _context.Mechanics.Where(m => validIds.Contains(m.MechanicId)).ToListAsync();

                // Log each deletion
                foreach (var m in mechanics)
                {
                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        Action = "Delete",
                        Module = "Mechanics",
                        Description = $"Deleted mechanic: {m.MechanicName}",
                        StaffId = staffId,
                        Timestamp = DateTime.Now
                    });
                }

                _context.Mechanics.RemoveRange(mechanics);
                await _context.SaveChangesAsync();

                string msg = $"{mechanics.Count} mechanic(s) deleted successfully.";
                if (skipped > 0)
                    msg += $" {skipped} mechanic(s) skipped (have existing service transactions).";

                return Json(new { success = true, message = msg });
            }
            catch (Exception ex)
            {
                // Log the exception (in a real app, use a proper logger)
                return Json(new { success = false, message = "An error occurred while deleting mechanics. Please try again." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearAll()
        {
            // Validate session
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
                return Json(new { success = false, message = "Session expired. Please log in again." });

            try
            {
                var referencedIds = await _context.ServiceTransactions
                    .Select(st => st.MechanicId)
                    .Distinct()
                    .ToListAsync();

                var toDelete = await _context.Mechanics
                    .Where(m => !referencedIds.Contains(m.MechanicId))
                    .ToListAsync();

                var skipped = await _context.Mechanics.CountAsync(m => referencedIds.Contains(m.MechanicId));

                if (toDelete.Count == 0)
                    return Json(new { success = false, message = "No mechanics can be deleted. All mechanics have existing service transaction references." });

                // Log each deletion
                foreach (var m in toDelete)
                {
                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        Action = "Delete",
                        Module = "Mechanics",
                        Description = $"Deleted mechanic via Clear All: {m.MechanicName}",
                        StaffId = staffId,
                        Timestamp = DateTime.Now
                    });
                }

                _context.Mechanics.RemoveRange(toDelete);
                await _context.SaveChangesAsync();

                string msg = $"All {toDelete.Count} mechanic(s) cleared successfully.";
                if (skipped > 0)
                    msg += $" {skipped} mechanic(s) skipped (have existing service transaction references).";

                return Json(new { success = true, message = msg });
            }
            catch (Exception ex)
            {
                // Log the exception (in a real app, use a proper logger)
                return Json(new { success = false, message = "An error occurred while clearing mechanics. Please try again." });
            }
        }
    }
}
