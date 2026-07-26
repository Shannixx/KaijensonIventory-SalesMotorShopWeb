using System.Linq;
using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
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

        public async Task<IActionResult> Index(string? searchString, int page = 1)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            try
            {
                int pageSize = 10;
                IQueryable<Supplier> query = _context.Suppliers.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    query = query.Where(s => s.CompanyName.Contains(searchString) ||
                                             (s.ContactPerson != null && s.ContactPerson.Contains(searchString)) ||
                                             (s.ContactNumber != null && s.ContactNumber.Contains(searchString)));
                }

                int total = await query.CountAsync();

                List<Supplier> suppliers = await query
                    .OrderBy(s => s.CompanyName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["CurrentFilter"] = searchString;
                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                return View(suppliers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading suppliers.");
                TempData["ErrorMessage"] = "An error occurred while loading suppliers. Please try again.";
                return View(new List<Supplier>());
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
                var supplier = await _context.Suppliers
                    .Include(s => s.Products)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SupplierId == id);

                if (supplier == null) return NotFound();

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
        public async Task<IActionResult> Create([Bind("CompanyName,ContactPerson,ContactNumber,Address")] Supplier supplier)
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

                if (ModelState.IsValid)
                {
                    // Check duplicate company name
                    bool exists = await _context.Suppliers.AnyAsync(s => s.CompanyName == supplier.CompanyName);
                    if (exists)
                    {
                        ModelState.AddModelError("CompanyName", "A supplier with this company name already exists.");
                        return View(supplier);
                    }

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
                var supplier = await _context.Suppliers.FindAsync(id);
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
        public async Task<IActionResult> Edit(int id, [Bind("SupplierId,CompanyName,ContactPerson,ContactNumber,Address")] Supplier supplier)
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
                    bool exists = await _context.Suppliers.AnyAsync(s => s.CompanyName == supplier.CompanyName && s.SupplierId != id);
                    if (exists)
                    {
                        ModelState.AddModelError("CompanyName", "A supplier with this company name already exists.");
                        return View(supplier);
                    }

                    try
                    {
                        _context.Suppliers.Update(supplier);
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
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            if (id == null) return NotFound();

            try
            {
                // Check if supplier has associated products
                bool hasProducts = await _context.Products.AnyAsync(p => p.SupplierId == id);

                if (hasProducts)
                {
                    TempData["ErrorMessage"] = "Cannot delete supplier. This supplier has associated products.";
                    return RedirectToAction(nameof(Index));
                }

                var supplier = await _context.Suppliers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SupplierId == id);

                if (supplier == null) return NotFound();

                return View(supplier);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading supplier for deletion. SupplierId: {SupplierId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the supplier for deletion. Please try again.";
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
                var supplier = await _context.Suppliers.FindAsync(id);
                if (supplier == null) return NotFound();

                // Check if supplier has associated products
                bool hasProducts = await _context.Products.AnyAsync(p => p.SupplierId == id);

                if (hasProducts)
                {
                    TempData["ErrorMessage"] = "Cannot delete supplier. This supplier has associated products.";
                    return RedirectToAction(nameof(Index));
                }

                string name = supplier.CompanyName;

                _context.Suppliers.Remove(supplier);
                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = GetCurrentStaffId(),
                    Action = "Delete Supplier",
                    Module = "Supplier",
                    Description = $"Deleted supplier '{name}'."
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Supplier '{name}' deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting supplier. SupplierId: {SupplierId}", id);
                TempData["ErrorMessage"] = "An error occurred while deleting the supplier. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
