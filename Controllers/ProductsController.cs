using System.Linq;
using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ProductsController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<IActionResult> Index(string? searchString, int? categoryId, int page = 1)
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
                IQueryable<Product> query = _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    string s = searchString.ToLower();
                    query = query.Where(p => p.ProductName.ToLower().Contains(s)
                        || (p.Description != null && p.Description.ToLower().Contains(s))
                        || (p.Brand != null && p.Brand.ToLower().Contains(s))
                        || (p.PartNumber != null && p.PartNumber.ToLower().Contains(s)));
                }

                if (categoryId.HasValue && categoryId.Value > 0)
                {
                    query = query.Where(p => p.CategoryId == categoryId.Value);
                }

                int total = await query.CountAsync();

                List<Product> products = await query
                    .OrderBy(p => p.ProductName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["CurrentFilter"] = searchString;
                ViewData["CategoryId"] = categoryId;

                ViewBag.Categories = new SelectList(
                    await _context.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync(),
                    "CategoryId", "CategoryName", categoryId);

                return View(products);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading products. Please try again.";
                return View(new List<Product>());
            }
        }

        public async Task<IActionResult> Create()
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
                ViewBag.Categories = new SelectList(
                    await _context.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync(),
                    "CategoryId", "CategoryName");

                ViewBag.Suppliers = new SelectList(
                    await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                    "SupplierId", "CompanyName");

                return View();
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading the product creation form. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
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
                if (string.IsNullOrWhiteSpace(product.ProductName))
                {
                    ModelState.AddModelError("ProductName", "Product name is required.");
                }

                if (product.Price < 0)
                {
                    ModelState.AddModelError("Price", "Price cannot be negative.");
                }

                if (product.QuantityOnHand < 0)
                {
                    ModelState.AddModelError("QuantityOnHand", "Quantity cannot be negative.");
                }

                if (product.ReorderLevel < 0)
                {
                    ModelState.AddModelError("ReorderLevel", "Reorder level cannot be negative.");
                }

                if (product.CategoryId <= 0)
                {
                    ModelState.AddModelError("CategoryId", "Please select a category.");
                }

                if (product.SupplierId <= 0)
                {
                    ModelState.AddModelError("SupplierId", "Please select a supplier.");
                }

                if (ModelState.IsValid)
                {
                    // Check duplicate product name
                    bool nameExists = await _context.Products.AnyAsync(p => p.ProductName == product.ProductName);
                    if (nameExists)
                    {
                        ModelState.AddModelError("ProductName", "A product with this name already exists.");
                        ViewBag.Categories = new SelectList(
                            await _context.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync(),
                            "CategoryId", "CategoryName", product.CategoryId);
                        ViewBag.Suppliers = new SelectList(
                            await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                            "SupplierId", "CompanyName", product.SupplierId);
                        return View(product);
                    }

                    // Check duplicate part number
                    if (!string.IsNullOrWhiteSpace(product.PartNumber))
                    {
                        bool partExists = await _context.Products.AnyAsync(p => p.PartNumber == product.PartNumber);
                        if (partExists)
                        {
                            ModelState.AddModelError("PartNumber", "A product with this part number already exists.");
                            ViewBag.Categories = new SelectList(
                                await _context.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync(),
                                "CategoryId", "CategoryName", product.CategoryId);
                            ViewBag.Suppliers = new SelectList(
                                await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                                "SupplierId", "CompanyName", product.SupplierId);
                            return View(product);
                        }
                    }

                    try
                    {
                        product.ImagePath = await SaveImageAsync(imageFile);
                        product.StockStatus = CalculateStockStatus(product.QuantityOnHand, product.ReorderLevel);
                        product.CreatedAt = DateTime.Now;
                        _context.Products.Add(product);
                        await _context.SaveChangesAsync();

                        _context.ActivityLogs.Add(new ActivityLog
                        {
                            StaffId = staffId,
                            Action = "Create Product",
                            Module = "Product",
                            Description = $"Product {product.ProductName} - Qty: {product.QuantityOnHand}, Price: {product.Price}"
                        });
                        await _context.SaveChangesAsync();

                        TempData["Success"] = "Product created successfully.";
                        return RedirectToAction(nameof(Index));
                    }
                    catch
                    {
                        TempData["ErrorMessage"] = "An error occurred while creating the product. Please try again.";
                    }
                }

                ViewBag.Categories = new SelectList(
                    await _context.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync(),
                    "CategoryId", "CategoryName", product.CategoryId);

                ViewBag.Suppliers = new SelectList(
                    await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                    "SupplierId", "CompanyName", product.SupplierId);

                return View(product);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while creating the product. Please try again.";
                ViewBag.Categories = new SelectList(
                    await _context.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync(),
                    "CategoryId", "CategoryName", product.CategoryId);

                ViewBag.Suppliers = new SelectList(
                    await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                    "SupplierId", "CompanyName", product.SupplierId);

                return View(product);
            }
        }

        public async Task<IActionResult> Edit(int id)
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
                Product? product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == id);
                if (product == null) return NotFound();

                ViewBag.Categories = new SelectList(
                    await _context.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync(),
                    "CategoryId", "CategoryName", product.CategoryId);

                ViewBag.Suppliers = new SelectList(
                    await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                    "SupplierId", "CompanyName", product.SupplierId);

                return View(product);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading the product for editing. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product, IFormFile? imageFile)
        {
            // Validate session
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            if (id != product.ProductId) return NotFound();

            // Validate required fields
            if (string.IsNullOrWhiteSpace(product.ProductName))
            {
                ModelState.AddModelError("ProductName", "Product name is required.");
            }

            if (product.Price < 0)
            {
                ModelState.AddModelError("Price", "Price cannot be negative.");
            }

            if (product.QuantityOnHand < 0)
            {
                ModelState.AddModelError("QuantityOnHand", "Quantity cannot be negative.");
            }

            if (product.ReorderLevel < 0)
            {
                ModelState.AddModelError("ReorderLevel", "Reorder level cannot be negative.");
            }

            if (product.CategoryId <= 0)
            {
                ModelState.AddModelError("CategoryId", "Please select a category.");
            }

            if (product.SupplierId <= 0)
            {
                ModelState.AddModelError("SupplierId", "Please select a supplier.");
            }

            if (ModelState.IsValid)
                {
                    // Check duplicate product name (exclude self)
                    bool nameExists = await _context.Products.AnyAsync(p => p.ProductName == product.ProductName && p.ProductId != id);
                    if (nameExists)
                    {
                        ModelState.AddModelError("ProductName", "A product with this name already exists.");
                        ViewBag.Categories = new SelectList(
                            await _context.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync(),
                            "CategoryId", "CategoryName", product.CategoryId);
                        ViewBag.Suppliers = new SelectList(
                            await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                            "SupplierId", "CompanyName", product.SupplierId);
                        return View(product);
                    }

                    // Check duplicate part number (exclude self)
                    if (!string.IsNullOrWhiteSpace(product.PartNumber))
                    {
                        bool partExists = await _context.Products.AnyAsync(p => p.PartNumber == product.PartNumber && p.ProductId != id);
                        if (partExists)
                        {
                            ModelState.AddModelError("PartNumber", "A product with this part number already exists.");
                            ViewBag.Categories = new SelectList(
                                await _context.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync(),
                                "CategoryId", "CategoryName", product.CategoryId);
                            ViewBag.Suppliers = new SelectList(
                                await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                                "SupplierId", "CompanyName", product.SupplierId);
                            return View(product);
                        }
                    }

                    try
                    {
                        Product? existing = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == id);
                        if (existing == null) return NotFound();

                        if (imageFile != null)
                        {
                            DeleteImageFile(existing.ImagePath);
                            product.ImagePath = await SaveImageAsync(imageFile);
                        }
                        else
                        {
                            product.ImagePath = existing.ImagePath;
                        }

                        product.StockStatus = CalculateStockStatus(product.QuantityOnHand, product.ReorderLevel);
                        product.CreatedAt = existing.CreatedAt;

                        _context.Products.Update(product);

                        _context.ActivityLogs.Add(new ActivityLog
                        {
                            StaffId = staffId,
                            Action = "Edit Product",
                            Module = "Product",
                            Description = $"Product {product.ProductName} - Qty: {product.QuantityOnHand}, Price: {product.Price}"
                        });

                        await _context.SaveChangesAsync();
                        TempData["Success"] = "Product updated successfully.";
                        return RedirectToAction(nameof(Index));
                    }
                    catch
                    {
                        TempData["ErrorMessage"] = "An error occurred while updating the product. Please try again.";
                    }
                }

            ViewBag.Categories = new SelectList(
                await _context.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync(),
                "CategoryId", "CategoryName", product.CategoryId);

            ViewBag.Suppliers = new SelectList(
                await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                "SupplierId", "CompanyName", product.SupplierId);

            return View(product);
        }

        public async Task<IActionResult> Details(int id)
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
                Product? product = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ProductId == id);

                if (product == null) return NotFound();
                return View(product);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading product details. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> Delete(int id)
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
                Product? product = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ProductId == id);

                if (product == null) return NotFound();
                
                // Check if product has any sales or service transactions
                bool hasSales = await _context.SalesItems.AnyAsync(si => si.ProductId == id);
                bool hasServiceParts = await _context.ServicePartsUsed.AnyAsync(sp => sp.ProductId == id);
                bool hasStockIns = await _context.StockIns.AnyAsync(si => si.ProductId == id);

                if (hasSales || hasServiceParts || hasStockIns)
                {
                    TempData["ErrorMessage"] = "Cannot delete product. This product has existing transactions.";
                    return RedirectToAction(nameof(Index));
                }

                return View(product);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading the product for deletion. Please try again.";
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
                Product? product = await _context.Products.FindAsync(id);
                if (product == null) return NotFound();

                // Check if product has any sales or service transactions
                bool hasSales = await _context.SalesItems.AnyAsync(si => si.ProductId == id);
                bool hasServiceParts = await _context.ServicePartsUsed.AnyAsync(sp => sp.ProductId == id);
                bool hasStockIns = await _context.StockIns.AnyAsync(si => si.ProductId == id);

                if (hasSales || hasServiceParts || hasStockIns)
                {
                    TempData["ErrorMessage"] = "Cannot delete product. This product has existing transactions.";
                    return RedirectToAction(nameof(Index));
                }

                DeleteImageFile(product.ImagePath);

                _context.Products.Remove(product);

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = staffId,
                    Action = "Delete Product",
                    Module = "Product",
                    Description = $"Product {product.ProductName} deleted"
                });

                await _context.SaveChangesAsync();
                TempData["Success"] = "Product deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the product. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        private async Task<string?> SaveImageAsync(IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0) return null;

            // Validate file size (max 5MB)
            if (imageFile.Length > 5 * 1024 * 1024)
            {
                throw new InvalidOperationException("Image file size must be less than 5MB.");
            }

            // Validate file type
            string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
            string ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                throw new InvalidOperationException("Only JPG, JPEG, PNG, and GIF files are allowed.");
            }

            string uploads = Path.Combine(_env.WebRootPath, "uploads", "products");
            Directory.CreateDirectory(uploads);

            string fileName = Guid.NewGuid() + ext;
            string filePath = Path.Combine(uploads, fileName);

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                return "~/uploads/products/" + fileName;
            }
            catch
            {
                // Log the exception in a real application
                throw new InvalidOperationException("Failed to save image file. Please try again.");
            }
        }

        private void DeleteImageFile(string? imagePath)
        {
            if (string.IsNullOrEmpty(imagePath)) return;

            string fileName = Path.GetFileName(imagePath);
            string filePath = Path.Combine(_env.WebRootPath, "uploads", "products", fileName);

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }

        private static string CalculateStockStatus(int qty, int reorder)
        {
            if (qty <= 0) return "Out of Stock";
            if (qty <= reorder) return "Low Stock";
            return "Available";
        }
    }
}
