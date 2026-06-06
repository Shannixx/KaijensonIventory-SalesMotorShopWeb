using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Hubs;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class StockInsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public StockInsController(ApplicationDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
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
                IQueryable<StockIn> query = _context.StockIns
                    .Include(s => s.Product)
                    .Include(s => s.Supplier)
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    string s = searchString.ToLower();
                    query = query.Where(st => st.Product!.ProductName.ToLower().Contains(s) || st.Supplier!.CompanyName.ToLower().Contains(s));
                }

                int total = await query.CountAsync();

                List<StockIn> items = await query
                    .OrderByDescending(s => s.DeliveryDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["CurrentFilter"] = searchString;

                return View(items);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading stock in records. Please try again.";
                return View(new List<StockIn>());
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
                var products = await _context.Products.AsNoTracking().OrderBy(p => p.ProductName).ToListAsync();
                ViewBag.Products = new SelectList(
                    products.Select(p => new { p.ProductId, Display = $"{p.Brand ?? "Unknown"} - {p.ProductName}" }).ToList(),
                    "ProductId", "Display");

                ViewBag.Suppliers = new SelectList(
                    await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                    "SupplierId", "CompanyName");

                return View();
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading the stock in form. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StockIn stockIn)
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
                if (stockIn.QuantityReceived <= 0)
                {
                    ModelState.AddModelError("QuantityReceived", "Quantity received must be greater than zero.");
                }

                if (stockIn.UnitCost < 0)
                {
                    ModelState.AddModelError("UnitCost", "Unit cost cannot be negative.");
                }

                if (stockIn.ProductId <= 0)
                {
                    ModelState.AddModelError("ProductId", "Please select a product.");
                }

                if (stockIn.SupplierId <= 0)
                {
                    ModelState.AddModelError("SupplierId", "Please select a supplier.");
                }

                if (ModelState.IsValid)
                {
                    Product? product = await _context.Products.FindAsync(stockIn.ProductId);
                    if (product == null)
                    {
                        ModelState.AddModelError("ProductId", "Selected product not found.");
                        var prodList = await _context.Products.AsNoTracking().OrderBy(p => p.ProductName).ToListAsync();
                        ViewBag.Products = new SelectList(
                            prodList.Select(p => new { p.ProductId, Display = $"{p.Brand ?? "Unknown"} - {p.ProductName}" }).ToList(),
                            "ProductId", "Display", stockIn.ProductId);
                        ViewBag.Suppliers = new SelectList(
                            await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                            "SupplierId", "CompanyName", stockIn.SupplierId);
                        return View(stockIn);
                    }

                    // Use a transaction to ensure data consistency
                    using var dbTransaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        int oldQty = product.QuantityOnHand;
                        decimal oldAvgCost = product.AverageCost;
                        product.QuantityOnHand += stockIn.QuantityReceived;
                        product.AverageCost = oldQty > 0
                            ? Math.Round(((oldAvgCost * oldQty) + (stockIn.UnitCost * stockIn.QuantityReceived)) / product.QuantityOnHand, 2)
                            : stockIn.UnitCost;
                        product.StockStatus = CalculateStockStatus(product.QuantityOnHand, product.ReorderLevel);

                        stockIn.StaffId = staffId.Value;
                        stockIn.DeliveryDate = stockIn.DeliveryDate.Date;

                        _context.StockIns.Add(stockIn);

                        _context.ActivityLogs.Add(new ActivityLog
                        {
                            StaffId = stockIn.StaffId,
                            Action = "Create StockIn",
                            Module = "StockIn",
                            Description = $"Received {stockIn.QuantityReceived} units of '{product.ProductName}' at {stockIn.UnitCost:N2} each from supplier"
                        });

                        await _context.SaveChangesAsync();

                        _context.InventoryTransactions.Add(new InventoryTransaction
                        {
                            ProductId = stockIn.ProductId,
                            TransactionType = "StockIn",
                            Quantity = stockIn.QuantityReceived,
                            UnitCost = stockIn.UnitCost,
                            ReferenceId = stockIn.StockInId,
                            ReferenceType = "StockIn",
                            StaffId = stockIn.StaffId,
                            TransactionDate = DateTime.Now,
                            Remarks = $"Stock in from {stockIn.Supplier?.CompanyName ?? "supplier"}"
                        });

                        await _context.SaveChangesAsync();
                        await dbTransaction.CommitAsync();

                        await _hubContext.Clients.All.SendAsync("ReceiveNotification",
                            $"Stock in: {stockIn.QuantityReceived} units of '{product.ProductName}' received", "success");
                        await _hubContext.Clients.All.SendAsync("StockUpdated",
                            product.ProductName, product.QuantityOnHand, product.StockStatus);

                        TempData["SuccessMessage"] = "Stock in record created successfully.";
                        return RedirectToAction(nameof(Index));
                    }
                    catch
                    {
                        await dbTransaction.RollbackAsync();
                        throw; // Re-throw to be caught by outer catch block
                    }
                }

                var prodList2 = await _context.Products.AsNoTracking().OrderBy(p => p.ProductName).ToListAsync();
                ViewBag.Products = new SelectList(
                    prodList2.Select(p => new { p.ProductId, Display = $"{p.Brand ?? "Unknown"} - {p.ProductName}" }).ToList(),
                    "ProductId", "Display", stockIn.ProductId);

                ViewBag.Suppliers = new SelectList(
                    await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                    "SupplierId", "CompanyName", stockIn.SupplierId);

                return View(stockIn);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while creating the stock in record. Please try again.";
                var prodList3 = await _context.Products.AsNoTracking().OrderBy(p => p.ProductName).ToListAsync();
                ViewBag.Products = new SelectList(
                    prodList3.Select(p => new { p.ProductId, Display = $"{p.Brand ?? "Unknown"} - {p.ProductName}" }).ToList(),
                    "ProductId", "Display", stockIn.ProductId);
                ViewBag.Suppliers = new SelectList(
                    await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                    "SupplierId", "CompanyName", stockIn.SupplierId);
                return View(stockIn);
            }
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
                StockIn? item = await _context.StockIns
                    .Include(s => s.Product)
                    .Include(s => s.Supplier)
                    .Include(s => s.Staff)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.StockInId == id);

                if (item == null) return NotFound();
                return View(item);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading stock in details. Please try again.";
                return RedirectToAction(nameof(Index));
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
