using System.Linq;
using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CategoriesController(ApplicationDbContext context)
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
                IQueryable<Category> query = _context.Categories.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    query = query.Where(c => c.CategoryName.Contains(searchString));
                }

                int total = await query.CountAsync();

                List<Category> categories = await query
                    .OrderBy(c => c.CategoryName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["CurrentFilter"] = searchString;
                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                return View(categories);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while loading categories. Please try again.";
                return View(new List<Category>());
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
                var category = await _context.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CategoryId == id);

                if (category == null) return NotFound();

                return View(category);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while loading category details. Please try again.";
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
        public async Task<IActionResult> Create([Bind("CategoryName")] Category category)
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
                if (string.IsNullOrWhiteSpace(category.CategoryName))
                {
                    ModelState.AddModelError("CategoryName", "Category name is required.");
                }

                if (ModelState.IsValid)
                {
                    // Check if category already exists
                    bool exists = await _context.Categories.AnyAsync(c => c.CategoryName == category.CategoryName);
                    if (exists)
                    {
                        ModelState.AddModelError("CategoryName", "A category with this name already exists.");
                        return View(category);
                    }

                    _context.Categories.Add(category);
                    await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        StaffId = staffId,
                        Action = "Create Category",
                        Module = "Category",
                        Description = $"Created category '{category.CategoryName}'."
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Category '{category.CategoryName}' created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                return View(category);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while creating the category. Please try again.";
                return View(category);
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
                var category = await _context.Categories.FindAsync(id);
                if (category == null) return NotFound();

                return View(category);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while loading the category for editing. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("CategoryId,CategoryName")] Category category)
        {
            // Validate session
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            if (id != category.CategoryId) return NotFound();

            // Validate required fields
            if (string.IsNullOrWhiteSpace(category.CategoryName))
            {
                ModelState.AddModelError("CategoryName", "Category name is required.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Check if another category already exists with this name
                    bool exists = await _context.Categories.AnyAsync(c => c.CategoryName == category.CategoryName && c.CategoryId != id);
                    if (exists)
                    {
                        ModelState.AddModelError("CategoryName", "A category with this name already exists.");
                        return View(category);
                    }

                    _context.Categories.Update(category);
                    await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        StaffId = staffId,
                        Action = "Edit Category",
                        Module = "Category",
                        Description = $"Edited category '{category.CategoryName}'."
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Category '{category.CategoryName}' updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Categories.AnyAsync(c => c.CategoryId == category.CategoryId))
                        return NotFound();

                    TempData["ErrorMessage"] = "The category was modified by another user. Please try again.";
                    return View(category);
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "An error occurred while updating the category. Please try again.";
                    return View(category);
                }
            }
            return View(category);
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
                // Check if category has associated products
                bool hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id);
                if (hasProducts)
                {
                    TempData["ErrorMessage"] = "Cannot delete category. This category has associated products.";
                    return RedirectToAction(nameof(Index));
                }

                var category = await _context.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CategoryId == id);

                if (category == null) return NotFound();

                return View(category);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while loading the category for deletion. Please try again.";
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
                var category = await _context.Categories.FindAsync(id);
                if (category == null) return NotFound();

                // Check if category has associated products
                bool hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id);
                if (hasProducts)
                {
                    TempData["ErrorMessage"] = "Cannot delete category. This category has associated products.";
                    return RedirectToAction(nameof(Index));
                }

                string name = category.CategoryName;

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = staffId,
                    Action = "Delete Category",
                    Module = "Category",
                    Description = $"Deleted category '{name}'."
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Category '{name}' deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the category. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
