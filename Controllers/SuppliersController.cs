using System.Linq;
using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class SuppliersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SuppliersController(ApplicationDbContext context)
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
                IQueryable<Supplier> query = _context.Suppliers.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    query = query.Where(s => s.CompanyName.Contains(searchString) ||
                                             s.ContactPerson!.Contains(searchString) ||
                                             s.ContactNumber!.Contains(searchString));
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
                TempData["ErrorMessage"] = "An error occurred while loading suppliers. Please try again.";
                return View(new List<Supplier>());
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
                var supplier = await _context.Suppliers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SupplierId == id);

                if (supplier == null) return NotFound();

                return View(supplier);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while loading supplier details. Please try again.";
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
        public async Task<IActionResult> Create([Bind("CompanyName,ContactPerson,ContactNumber,Address")] Supplier supplier)
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
                // Validate required fields
                if (string.IsNullOrWhiteSpace(supplier.CompanyName))
                {
                    ModelState.AddModelError("CompanyName", "Company name is required.");
                }

                if (ModelState.IsValid)
                {
                    _context.Suppliers.Add(supplier);
                    await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        StaffId = staffId,
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
                TempData["ErrorMessage"] = "An error occurred while creating the supplier. Please try again.";
                return View(supplier);
            }
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
                var supplier = await _context.Suppliers.FindAsync(id);
                if (supplier == null) return NotFound();

                return View(supplier);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while loading the supplier for editing. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("SupplierId,CompanyName,ContactPerson,ContactNumber,Address")] Supplier supplier)
        {
            // Validate session
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            if (id != supplier.SupplierId) return NotFound();

            // Validate required fields
            if (string.IsNullOrWhiteSpace(supplier.CompanyName))
            {
                ModelState.AddModelError("CompanyName", "Company name is required.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Suppliers.Update(supplier);
                    await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        StaffId = staffId,
                        Action = "Edit Supplier",
                        Module = "Supplier",
                        Description = $"Edited supplier '{supplier.CompanyName}'."
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Supplier '{supplier.CompanyName}' updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Suppliers.AnyAsync(s => s.SupplierId == supplier.SupplierId))
                        return NotFound();

                    TempData["ErrorMessage"] = "The supplier was modified by another user. Please try again.";
                    return View(supplier);
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "An error occurred while updating the supplier. Please try again.";
                    return View(supplier);
                }
            }
            return View(supplier);
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
                // Check if supplier has associated products or stock ins
                bool hasProducts = await _context.Products.AnyAsync(p => p.SupplierId == id);
                bool hasStockIns = await _context.StockIns.AnyAsync(si => si.SupplierId == id);
                
                if (hasProducts || hasStockIns)
                {
                    TempData["ErrorMessage"] = "Cannot delete supplier. This supplier has associated products or stock records.";
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
                TempData["ErrorMessage"] = "An error occurred while loading the supplier for deletion. Please try again.";
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
                var supplier = await _context.Suppliers.FindAsync(id);
                if (supplier == null) return NotFound();

                // Check if supplier has associated products or stock ins
                bool hasProducts = await _context.Products.AnyAsync(p => p.SupplierId == id);
                bool hasStockIns = await _context.StockIns.AnyAsync(si => si.SupplierId == id);
                
                if (hasProducts || hasStockIns)
                {
                    TempData["ErrorMessage"] = "Cannot delete supplier. This supplier has associated products or stock records.";
                    return RedirectToAction(nameof(Index));
                }

                string name = supplier.CompanyName;

                _context.Suppliers.Remove(supplier);
                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = staffId,
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
                TempData["ErrorMessage"] = "An error occurred while deleting the supplier. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
