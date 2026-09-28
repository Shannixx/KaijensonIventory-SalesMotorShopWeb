using System.Linq;
using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class SuppliersController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SuppliersController> _logger;

        public SuppliersController(ApplicationDbContext context, ILogger<SuppliersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? searchString, string? statusFilter, int page = 1, bool archived = false)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;
            if (archived && !IsAdmin()) return Forbid();

            try
            {
                int pageSize = 10;
                IQueryable<Supplier> query = _context.Suppliers.AsNoTracking()
                    .Where(s => archived ? s.IsDeleted : !s.IsDeleted);

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    query = query.Where(s => s.CompanyName.Contains(searchString) ||
                                             (s.ContactPerson != null && s.ContactPerson.Contains(searchString)) ||
                                             (s.ContactNumber != null && s.ContactNumber.Contains(searchString)) ||
                                             (s.EmailAddress != null && s.EmailAddress.Contains(searchString)));
                }

                // Status filter
                if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All")
                {
                    query = query.Where(s => s.Status == statusFilter);
                }

                int total = await query.CountAsync();


                List<Supplier> suppliers = await query
                    .OrderBy(s => s.SupplierId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["CurrentFilter"] = searchString;
                ViewData["StatusFilter"] = statusFilter;
                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewBag.ShowArchived = archived;
                return View(suppliers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading suppliers.");
                TempData["ErrorMessage"] = "An error occurred while loading suppliers. Please try again.";
                return View(new List<Supplier>());
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
                var supplier = await _context.Suppliers
                    .Include(s => s.Products.Where(p => !p.IsDeleted))
                    .Include(s => s.CreatedByStaff)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SupplierId == id && (archived ? s.IsDeleted : !s.IsDeleted));

                if (supplier == null) return NotFound();

                ViewBag.ShowArchived = archived;
                ViewBag.TotalProducts = supplier.Products.Count;

                return View(supplier);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading supplier details. SupplierId: {SupplierId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading supplier details. Please try again.";
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
        public async Task<IActionResult> Create([Bind("CompanyName,ContactPerson,ContactNumber,Address,EmailAddress")] Supplier supplier)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(supplier.CompanyName))
                {
                    ModelState.AddModelError("CompanyName", "Company name is required.");
                }
                if (string.IsNullOrWhiteSpace(supplier.ContactPerson))
                {
                    ModelState.AddModelError("ContactPerson", "Contact person is required.");
                }
                if (string.IsNullOrWhiteSpace(supplier.ContactNumber))
                {
                    ModelState.AddModelError("ContactNumber", "Contact number is required.");
                }
                if (string.IsNullOrWhiteSpace(supplier.Address))
                {
                    ModelState.AddModelError("Address", "Address is required.");
                }

                if (ModelState.IsValid)
                {
                    // Check duplicate company name
                    bool exists = await _context.Suppliers.AnyAsync(s => !s.IsDeleted && s.CompanyName == supplier.CompanyName);
                    if (exists)
                    {
                        ModelState.AddModelError("CompanyName", "A supplier with this company name already exists.");
                        return View(supplier);
                    }

                    // Set audit fields and default status
                    supplier.Status = "Active";
                    supplier.CreatedAt = DateTime.UtcNow;
                    supplier.CreatedBy = GetCurrentStaffId();
                    _context.Suppliers.Add(supplier);
                    await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        StaffId = GetCurrentStaffId(),
                        Action = "Create Supplier",
                        Module = "Supplier",
                        Description = $"Created supplier '{supplier.CompanyName}'."
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Supplier '{supplier.CompanyName}' created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                return View(supplier);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating supplier.");
                TempData["ErrorMessage"] = "An error occurred while creating the supplier. Please try again.";
                return View(supplier);
            }
        }

        public async Task<IActionResult> Edit(int? id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            if (id == null) return NotFound();

            try
            {
                var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == id && !s.IsDeleted);
                if (supplier == null) return NotFound();

                return View(supplier);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading supplier for editing. SupplierId: {SupplierId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the supplier for editing. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("SupplierId,CompanyName,ContactPerson,ContactNumber,Address,EmailAddress,Status")] Supplier supplier)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            if (id != supplier.SupplierId) return NotFound();

            // Validate required fields
            if (string.IsNullOrWhiteSpace(supplier.CompanyName))
            {
                ModelState.AddModelError("CompanyName", "Company name is required.");
            }

            if (ModelState.IsValid)
                {
                    // Check duplicate company name (exclude self)
                    bool exists = await _context.Suppliers.AnyAsync(s => !s.IsDeleted && s.CompanyName == supplier.CompanyName && s.SupplierId != id);
                    if (exists)
                    {
                        ModelState.AddModelError("CompanyName", "A supplier with this company name already exists.");
                        return View(supplier);
                    }

                    try
                    {
                        var existing = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == id && !s.IsDeleted);
                        if (existing == null) return NotFound();

                        // Update editable fields only
                        existing.CompanyName = supplier.CompanyName;
                        existing.ContactPerson = supplier.ContactPerson;
                        existing.ContactNumber = supplier.ContactNumber;
                        existing.Address = supplier.Address;
                        existing.EmailAddress = supplier.EmailAddress;
                        existing.Status = supplier.Status;

                        await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        StaffId = GetCurrentStaffId(),
                        Action = "Edit Supplier",
                        Module = "Supplier",
                        Description = $"Edited supplier '{supplier.CompanyName}'."
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Supplier '{supplier.CompanyName}' updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning(ex, "Concurrency conflict while updating supplier. SupplierId: {SupplierId}", id);
                    if (!await _context.Suppliers.AnyAsync(s => s.SupplierId == supplier.SupplierId))
                        return NotFound();

                    TempData["ErrorMessage"] = "The supplier was modified by another user. Please try again.";
                    return View(supplier);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while updating supplier. SupplierId: {SupplierId}", id);
                    TempData["ErrorMessage"] = "An error occurred while updating the supplier. Please try again.";
                    return View(supplier);
                }
            }
            return View(supplier);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            if (id == null) return NotFound();

            try
            {
                var supplier = await _context.Suppliers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SupplierId == id && !s.IsDeleted);

                if (supplier == null) return NotFound();

                return View(supplier);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading supplier for archive. SupplierId: {SupplierId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the supplier for archive. Please try again.";
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
                var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == id && !s.IsDeleted);
                if (supplier == null) return NotFound();

                string name = supplier.CompanyName;

                supplier.IsDeleted = true;
                supplier.DeletedAt = DateTime.UtcNow;
                supplier.DeletedBy = GetCurrentStaffId();
                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = GetCurrentStaffId(),
                    Action = "Archive Supplier",
                    Module = "Supplier",
                    Description = $"Archived supplier '{name}' ({supplier.SupplierId})."
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Supplier '{name}' archived successfully.";
                return RedirectToAction(nameof(Index));
            }


            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting supplier. SupplierId: {SupplierId}", id);
                TempData["ErrorMessage"] = "An error occurred while deleting the supplier. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == id && s.IsDeleted);
            if (supplier == null) return NotFound();

            supplier.IsDeleted = false;
            supplier.DeletedAt = null;
            supplier.DeletedBy = null;
            _context.ActivityLogs.Add(new ActivityLog
            {
                StaffId = GetCurrentStaffId(),
                Action = "Restore Supplier",
                Module = "Supplier",
                Description = $"Restored supplier '{supplier.CompanyName}' ({supplier.SupplierId})."
            });
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Supplier '{supplier.CompanyName}' restored successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;
            var authRedirect = RedirectIfNotOwnerOrManager();
            if (authRedirect != null) return authRedirect;

            var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == id && !s.IsDeleted);
            if (supplier == null) return NotFound();

            // Toggle status
            supplier.Status = supplier.Status == "Active" ? "Inactive" : "Active";
            await _context.SaveChangesAsync();

            // Log activity
            _context.ActivityLogs.Add(new ActivityLog
            {
                StaffId = GetCurrentStaffId(),
                Action = "Toggle Supplier Status",
                Module = "Supplier",
                Description = $"Toggled status of '{supplier.CompanyName}' to {supplier.Status}."
            });
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Supplier status updated successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
