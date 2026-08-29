using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class BrandsController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BrandsController> _logger;

        public BrandsController(ApplicationDbContext context, ILogger<BrandsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private int? GetStaffId()
        {
            return HttpContext.Session.GetInt32("StaffId");
        }

        private IActionResult? CheckAccess()
        {
            if (!GetStaffId().HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }
            if (!IsOwnerOrManager())
            {
                TempData["ErrorMessage"] = "Access denied. Admin or Manager privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }
            return null;
        }

        private async Task<List<SelectListItem>> GetSupplierOptionsAsync(int? selectedId = null)
        {
            return await _context.Suppliers
                .OrderBy(s => s.CompanyName)
                .Select(s => new SelectListItem
                {
                    Value = s.SupplierId.ToString(),
                    Text = s.CompanyName,
                    Selected = selectedId.HasValue && s.SupplierId == selectedId.Value
                })
                .ToListAsync();
        }

        public async Task<IActionResult> Index(string? searchString, string? statusFilter, int page = 1)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            try
            {
                int pageSize = 10;
                IQueryable<Brand> query = _context.Brands.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    string s = searchString.ToLower();
                    query = query.Where(b => b.BrandName.ToLower().Contains(s) ||
                                             b.CountryOrigin.ToLower().Contains(s));
                }

                if (!string.IsNullOrWhiteSpace(statusFilter) &&
                    (statusFilter == "Active" || statusFilter == "Inactive"))
                {
                    query = query.Where(b => b.Status == statusFilter);
                }

                int total = await query.CountAsync();

                List<Brand> brands = await query
                    .OrderBy(b => b.BrandId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var brandNames = brands.Select(b => b.BrandName).ToList();

                var productCounts = await _context.Products
                    .Where(p => p.Brand != null && brandNames.Contains(p.Brand))
                    .GroupBy(p => p.Brand!)
                    .Select(g => new { Brand = g.Key, Count = g.Count() })
                    .ToListAsync();

                ViewData["ProductCounts"] = productCounts.ToDictionary(k => k.Brand, v => v.Count);

                ViewData["CurrentFilter"] = searchString;
                ViewData["StatusFilter"] = statusFilter;
                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewBag.CanDelete = IsAdmin();
                return View(brands);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading brands.");
                TempData["ErrorMessage"] = "An error occurred while loading brands. Please try again.";
                return View(new List<Brand>());
            }
        }

        public async Task<IActionResult> Details(int? id)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            if (id == null) return NotFound();

            try
            {
                var brand = await _context.Brands
                    .Include(b => b.Supplier)
                    .Include(b => b.CreatedByStaff)
                    .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BrandId == id);
                 if (brand == null) return NotFound();

                var productCount = await _context.Products.CountAsync(p => p.Brand != null && p.Brand == brand.BrandName);
                ViewBag.ProductCount = productCount;

                return View(brand);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading brand details. BrandId: {BrandId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading brand details. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> Create()
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            var model = new BrandFormViewModel
            {
                Status = "Active",
                Suppliers = await GetSupplierOptionsAsync()
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BrandFormViewModel model)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            try
            {
                // ModelState validation via data annotations will be performed automatically.
                if (!ModelState.IsValid)
                {
                    // Ensure supplier list is populated before returning view.
                    model.Suppliers = await GetSupplierOptionsAsync(model.SupplierId);
                    return View(model);
                }

                // Duplicate name check
                bool exists = await _context.Brands.AnyAsync(b => b.BrandName == model.BrandName.Trim());
                if (exists)
                {
                    ModelState.AddModelError(nameof(model.BrandName), "A brand with this name already exists.");
                    model.Suppliers = await GetSupplierOptionsAsync(model.SupplierId);
                    return View(model);
                }

                var brand = new Brand
                {
                    BrandName = model.BrandName.Trim(),
                    Description = model.Description,
                    CountryOrigin = model.CountryOrigin.Trim(),
                    Status = "Active",
                    SupplierId = model.SupplierId,
                    CreatedBy = GetStaffId(),
                    CreatedAt = DateTime.UtcNow
                };

                _context.Brands.Add(brand);
                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = GetStaffId(),
                    Action = "Add",
                    Module = "Brand",
                    Description = $"Added brand: {brand.BrandName} ({brand.CountryOrigin}, {brand.Status})",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Brand '{brand.BrandName}' created successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating brand.");
                TempData["ErrorMessage"] = "An error occurred while creating the brand. Please try again.";
                model.Suppliers = await GetSupplierOptionsAsync(model.SupplierId);
                return View(model);
            }
        }

public async Task<IActionResult> Edit(int? id)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            if (id == null) return NotFound();

            try
            {
                var brand = await _context.Brands
                    .Include(b => b.Supplier)
                    .Include(b => b.CreatedByStaff)
                    .FirstOrDefaultAsync(b => b.BrandId == id);
                if (brand == null) return NotFound();

                var model = new BrandFormViewModel
                {
                    BrandId = brand.BrandId,
                    BrandName = brand.BrandName,
                    Description = brand.Description,
                    CountryOrigin = brand.CountryOrigin,
                    Status = brand.Status,
                    SupplierId = brand.SupplierId,
                    CreatedByName = brand.CreatedByStaff?.StaffName ?? "System",
                    CreatedAt = brand.CreatedAt,
                    Suppliers = await GetSupplierOptionsAsync(brand.SupplierId)
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading brand for editing. BrandId: {BrandId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the brand for editing. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

[HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BrandFormViewModel model)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            if (id != model.BrandId) return NotFound();

            // Basic validation (DataAnnotations already applied)
            if (string.IsNullOrWhiteSpace(model.BrandName))
                ModelState.AddModelError(nameof(model.BrandName), "Brand name is required.");
            if (string.IsNullOrWhiteSpace(model.CountryOrigin))
                ModelState.AddModelError(nameof(model.CountryOrigin), "Country of origin is required.");
            if (model.Status != "Active" && model.Status != "Inactive")
                ModelState.AddModelError(nameof(model.Status), "Status must be 'Active' or 'Inactive'.");

            if (!ModelState.IsValid)
            {
                model.Suppliers = await GetSupplierOptionsAsync(model.SupplierId);
                return View(model);
            }

            try
            {
                // Duplicate name check excluding current
                bool exists = await _context.Brands.AnyAsync(b => b.BrandName == model.BrandName.Trim() && b.BrandId != id);
                if (exists)
                {
                    ModelState.AddModelError(nameof(model.BrandName), "A brand with this name already exists.");
                    model.Suppliers = await GetSupplierOptionsAsync(model.SupplierId);
                    return View(model);
                }

                var existing = await _context.Brands.FindAsync(id);
                if (existing == null) return NotFound();

                // Check if BrandName change is allowed
                if (!string.Equals(existing.BrandName, model.BrandName.Trim(), StringComparison.Ordinal))
                {
                    int prodCount = await _context.Products.CountAsync(p => p.Brand != null && p.Brand == existing.BrandName);
                    if (prodCount > 0)
                    {
                        ModelState.AddModelError(nameof(model.BrandName), "This brand name cannot be changed because products are assigned to it.");
                        model.Suppliers = await GetSupplierOptionsAsync(model.SupplierId);
                        return View(model);
                    }
                }

                // Update allowed fields only
                existing.BrandName = model.BrandName.Trim();
                existing.Description = model.Description;
                existing.CountryOrigin = model.CountryOrigin.Trim();
                existing.Status = model.Status;
                existing.SupplierId = model.SupplierId;

                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = GetStaffId(),
                    Action = "Edit",
                    Module = "Brand",
                    Description = $"Edited brand: {model.BrandName} ({model.CountryOrigin}, {model.Status})",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Brand '{model.BrandName}' updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict while updating brand. BrandId: {BrandId}", id);
                if (!await _context.Brands.AnyAsync(b => b.BrandId == model.BrandId))
                    return NotFound();

                TempData["ErrorMessage"] = "The brand was modified by another user. Please try again.";
                model.Suppliers = await GetSupplierOptionsAsync(model.SupplierId);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating brand. BrandId: {BrandId}", id);
                TempData["ErrorMessage"] = "An error occurred while updating the brand. Please try again.";
                model.Suppliers = await GetSupplierOptionsAsync(model.SupplierId);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null)
            {
                return accessCheck;
            }

            if (!IsAdmin())
                return Forbid();

            try
            {
                var brand = await _context.Brands.FindAsync(id);
                if (brand == null)
                {
                    TempData["ErrorMessage"] = "The brand could not be found.";
                    return RedirectToAction(nameof(Index));
                }

                int productCount = await _context.Products.CountAsync(p => p.Brand == brand.BrandName);
                if (productCount > 0)
                {
                    TempData["ErrorMessage"] = "This brand cannot be deleted because products are assigned to it.";
                    return RedirectToAction(nameof(Index));
                }

                string name = brand.BrandName;

                _context.Brands.Remove(brand);
                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = GetStaffId(),
                    Action = "Delete",
                    Module = "Brand",
                    Description = $"Deleted brand: {name}",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Brand '{name}' deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting brand. BrandId: {BrandId}", id);
                TempData["ErrorMessage"] = "The brand could not be deleted because it is still referenced by other records.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
