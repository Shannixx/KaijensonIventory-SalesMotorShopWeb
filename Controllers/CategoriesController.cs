using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class CategoriesController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CategoriesController> _logger;

        public CategoriesController(ApplicationDbContext context, ILogger<CategoriesController> logger)
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

        public async Task<IActionResult> Index(string? searchString, int page = 1)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            try
            {
                int pageSize = 10;
                IQueryable<Category> query = _context.Categories.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    query = query.Where(c => c.CategoryName.Contains(searchString));
                }

                int total = await query.CountAsync();

                List<Category> categories = await query
                    .OrderBy(c => c.CategoryId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["CurrentFilter"] = searchString;
                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewBag.CanDelete = IsAdmin();
                return View(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading categories.");
                TempData["ErrorMessage"] = "An error occurred while loading categories. Please try again.";
                return View(new List<Category>());
            }
        }

        public async Task<IActionResult> Details(int? id)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            if (id == null) return NotFound();

            try
            {
                var category = await _context.Categories
                    .AsNoTracking()
                    .Include(c => c.CreatedByStaff)
                    .FirstOrDefaultAsync(c => c.CategoryId == id);
                if (category == null) return NotFound();
                return View(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading category details. CategoryId: {CategoryId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading category details. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        public IActionResult Create()
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            try
            {
                if (string.IsNullOrWhiteSpace(category.CategoryName))
                {
                    ModelState.AddModelError("CategoryName", "Category name is required.");
                }
                else
                {
                    category.CategoryName = category.CategoryName.Trim();
                }

                if (!string.IsNullOrWhiteSpace(category.Description))
                {
                    category.Description = category.Description.Trim();
                }

                if (ModelState.IsValid)
                {
                    bool exists = await _context.Categories.AnyAsync(c => c.CategoryName == category.CategoryName);
                    if (exists)
                    {
                        ModelState.AddModelError("CategoryName", "A category with this name already exists.");
                        return View(category);
                    }

                    category.CreatedBy = GetStaffId();
                    category.CreatedAt = DateTime.UtcNow;

                    _context.Categories.Add(category);
                    await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        StaffId = GetStaffId(),
                        Action = "Add",
                        Module = "Category",
                        Description = $"Added category: {category.CategoryName}",
                        Timestamp = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Category '{category.CategoryName}' created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                return View(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating category.");
                TempData["ErrorMessage"] = "An error occurred while creating the category. Please try again.";
                return View(category);
            }
        }

        public async Task<IActionResult> Edit(int? id)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            if (id == null) return NotFound();

            try
            {
                var category = await _context.Categories
                    .AsNoTracking()
                    .Include(c => c.CreatedByStaff)
                    .FirstOrDefaultAsync(c => c.CategoryId == id);
                if (category == null) return NotFound();
                return View(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading category for editing. CategoryId: {CategoryId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the category for editing. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category category)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            if (id != category.CategoryId) return NotFound();

            if (string.IsNullOrWhiteSpace(category.CategoryName))
            {
                ModelState.AddModelError("CategoryName", "Category name is required.");
            }
            else
            {
                category.CategoryName = category.CategoryName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(category.Description))
            {
                category.Description = category.Description.Trim();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    bool exists = await _context.Categories.AnyAsync(c => c.CategoryName == category.CategoryName && c.CategoryId != id);
                    if (exists)
                    {
                        ModelState.AddModelError("CategoryName", "A category with this name already exists.");
                        return View(category);
                    }

                    var existing = await _context.Categories.FindAsync(id);
                    if (existing == null) return NotFound();

                    existing.CategoryName = category.CategoryName;
                    existing.Description = category.Description;

                    await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        StaffId = GetStaffId(),
                        Action = "Edit",
                        Module = "Category",
                        Description = $"Edited category: {existing.CategoryName}",
                        Timestamp = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Category '{existing.CategoryName}' updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning(ex, "Concurrency conflict while updating category. CategoryId: {CategoryId}", id);
                    if (!await _context.Categories.AnyAsync(c => c.CategoryId == category.CategoryId))
                        return NotFound();

                    TempData["ErrorMessage"] = "The category was modified by another user. Please try again.";
                    return View(category);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while updating category. CategoryId: {CategoryId}", id);
                    TempData["ErrorMessage"] = "An error occurred while updating the category. Please try again.";
                    return View(category);
                }
            }
            return View(category);
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
                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                {
                    TempData["ErrorMessage"] = "The category could not be found.";
                    return RedirectToAction(nameof(Index));
                }

                int productCount = await _context.Products.CountAsync(p => p.CategoryId == id);
                if (productCount > 0)
                {
                    TempData["ErrorMessage"] = $"\"{category.CategoryName}\" is currently being used by one or more products. Please reassign those records before deleting this category.";
                    return RedirectToAction(nameof(Index));
                }

                int serviceCount = await _context.Services.CountAsync(s => s.CategoryId == id);
                if (serviceCount > 0)
                {
                    TempData["ErrorMessage"] = $"\"{category.CategoryName}\" is currently being used by one or more services. Please reassign those records before deleting this category.";
                    return RedirectToAction(nameof(Index));
                }

                string name = category.CategoryName;

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = GetStaffId(),
                    Action = "Delete",
                    Module = "Category",
                    Description = $"Deleted category: {name}",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Category '{name}' deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting category. CategoryId: {CategoryId}", id);
                TempData["ErrorMessage"] = "The category could not be deleted because it is still referenced by other records.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}