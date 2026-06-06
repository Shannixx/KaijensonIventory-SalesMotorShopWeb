using System.Linq;
using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Hubs;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class SalesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly PdfExportService _pdf;

        public SalesController(ApplicationDbContext context, IHubContext<NotificationHub> hubContext, PdfExportService pdf)
        {
            _context = context;
            _hubContext = hubContext;
            _pdf = pdf;
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
                IQueryable<SalesTransaction> query = _context.SalesTransactions
                    .Include(t => t.Staff)
                    .Include(t => t.SalesItems).ThenInclude(i => i.Product)
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    string s = searchString.ToLower();
                    query = query.Where(t =>
                        t.InvoiceNumber.ToLower().Contains(s) ||
                        t.CustomerName.ToLower().Contains(s) ||
                        t.SalesItems.Any(i => i.Product!.ProductName.ToLower().Contains(s)));
                }

                int total = await query.CountAsync();

                var transactions = await query
                    .OrderByDescending(t => t.TransactionDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["CurrentFilter"] = searchString;

                return View(transactions);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading sales transactions. Please try again.";
                return View(new List<SalesTransaction>());
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
                ViewBag.InvoiceNumber = $"INV-{DateTime.Now:yyyyMMddHHmmss}";
                ViewBag.ProductsList = await _context.Products
                    .Where(p => p.QuantityOnHand > 0)
                    .OrderBy(p => p.ProductName)
                    .ToListAsync();
                return View();
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading the sales form. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SalesTransaction model, List<SalesItem> items)
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
                if (string.IsNullOrWhiteSpace(model.CustomerName))
                {
                    ModelState.AddModelError("CustomerName", "Customer name is required.");
                }

                if (items == null || items.Count == 0)
                {
                    ModelState.AddModelError("", "At least one item is required.");
                }

                if (model.AmountPaid < 0)
                {
                    ModelState.AddModelError("AmountPaid", "Amount paid cannot be negative.");
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.InvoiceNumber = $"INV-{DateTime.Now:yyyyMMddHHmmss}";
                    ViewBag.ProductsList = await _context.Products.Where(p => p.QuantityOnHand > 0).OrderBy(p => p.ProductName).ToListAsync();
                    return View(model);
                }

                items ??= new List<SalesItem>();
                model.InvoiceNumber = $"INV-{DateTime.Now:yyyyMMddHHmmss}";
                model.TransactionDate = DateTime.Now;
                model.StaffId = staffId.Value;

                decimal total = 0;
                var salesItems = new List<SalesItem>();

                // Use a transaction to ensure data consistency
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    foreach (var item in items)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product == null)
                        {
                            ModelState.AddModelError("", $"Product with ID {item.ProductId} not found.");
                            ViewBag.InvoiceNumber = model.InvoiceNumber;
                            ViewBag.ProductsList = await _context.Products.Where(p => p.QuantityOnHand > 0).OrderBy(p => p.ProductName).ToListAsync();
                            return View(model);
                        }

                        if (item.Quantity < 1)
                        {
                            ModelState.AddModelError("", $"Invalid quantity for {product.ProductName}.");
                            ViewBag.InvoiceNumber = model.InvoiceNumber;
                            ViewBag.ProductsList = await _context.Products.Where(p => p.QuantityOnHand > 0).OrderBy(p => p.ProductName).ToListAsync();
                            return View(model);
                        }

                        if (product.QuantityOnHand < item.Quantity)
                        {
                            ModelState.AddModelError("", $"Insufficient stock for {product.ProductName}. Available: {product.QuantityOnHand}.");
                            ViewBag.InvoiceNumber = model.InvoiceNumber;
                            ViewBag.ProductsList = await _context.Products.Where(p => p.QuantityOnHand > 0).OrderBy(p => p.ProductName).ToListAsync();
                            return View(model);
                        }

                        decimal unitPrice = product.Price;
                        decimal subtotal = unitPrice * item.Quantity;
                        total += subtotal;

                        product.QuantityOnHand -= item.Quantity;
                        product.StockStatus = CalculateStockStatus(product.QuantityOnHand, product.ReorderLevel);

                        salesItems.Add(new SalesItem
                        {
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            UnitPrice = unitPrice,
                            Subtotal = subtotal
                        });

                        if (product.QuantityOnHand <= 0)
                        {
                            _context.Notifications.Add(new Notification
                            {
                                ProductId = product.ProductId,
                                AlertType = "Stock Alert",
                                Message = $"{product.ProductName} is now Out of Stock.",
                                CreatedAt = DateTime.Now
                            });
                        }
                        else if (product.QuantityOnHand <= product.ReorderLevel)
                        {
                            _context.Notifications.Add(new Notification
                            {
                                ProductId = product.ProductId,
                                AlertType = "Low Stock",
                                Message = $"{product.ProductName} is running low ({product.QuantityOnHand} left).",
                                CreatedAt = DateTime.Now
                            });
                        }
                    }

                    model.TotalAmount = total;
                    model.Change = model.AmountPaid - total;
                    model.SalesItems = salesItems;

                    _context.SalesTransactions.Add(model);
                    await _context.SaveChangesAsync();

                    foreach (var si in salesItems)
                    {
                        Product? soldProduct = await _context.Products.FindAsync(si.ProductId);
                        _context.InventoryTransactions.Add(new InventoryTransaction
                        {
                            ProductId = si.ProductId,
                            TransactionType = "Sale",
                            Quantity = -si.Quantity,
                            UnitCost = soldProduct?.AverageCost ?? 0,
                            ReferenceId = model.TransactionId,
                            ReferenceType = "Sale",
                            StaffId = model.StaffId,
                            TransactionDate = DateTime.Now,
                            Remarks = $"Sale {model.InvoiceNumber}"
                        });
                    }

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        StaffId = model.StaffId,
                        Action = "Create Sale",
                        Module = "Sales",
                        Description = $"Sale {model.InvoiceNumber} - {model.CustomerName}, Total: {model.TotalAmount:N2}"
                    });
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    await _hubContext.Clients.All.SendAsync("ReceiveNotification",
                        $"New sale: {model.InvoiceNumber} - {model.CustomerName} (₱{model.TotalAmount:N2})", "success");
                    await _hubContext.Clients.All.SendAsync("DashboardUpdated");

                    TempData["Success"] = "Sale completed successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw; // Re-throw to be caught by outer catch block
                }
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while processing the sale. Please try again.";
                ViewBag.InvoiceNumber = $"INV-{DateTime.Now:yyyyMMddHHmmss}";
                ViewBag.ProductsList = await _context.Products.Where(p => p.QuantityOnHand > 0).OrderBy(p => p.ProductName).ToListAsync();
                return View(model);
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
                var transaction = await _context.SalesTransactions
                    .Include(t => t.Staff)
                    .Include(t => t.SalesItems).ThenInclude(i => i.Product)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TransactionId == id);

                if (transaction == null) return NotFound();
                return View(transaction);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading sale details. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> Receipt(int id)
        {
            try
            {
                var transaction = await _context.SalesTransactions
                    .Include(t => t.Staff)
                    .Include(t => t.SalesItems).ThenInclude(i => i.Product)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TransactionId == id);

                if (transaction == null) return NotFound();
                return View(transaction);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading receipt. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> DownloadPdfReceipt(int id)
        {
            var transaction = await _context.SalesTransactions
                .Include(t => t.Staff)
                .Include(t => t.SalesItems).ThenInclude(i => i.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TransactionId == id);

            if (transaction == null) return NotFound();

            byte[] pdf = _pdf.GenerateSalesReceipt(transaction);
            var fileName = $"Receipt_{transaction.InvoiceNumber}.pdf";
            return File(pdf, "application/pdf", fileName);
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
                var transaction = await _context.SalesTransactions
                    .Include(t => t.Staff)
                    .Include(t => t.SalesItems).ThenInclude(i => i.Product)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TransactionId == id);

                if (transaction == null) return NotFound();
                return View(transaction);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading sale for deletion. Please try again.";
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
                var transaction = await _context.SalesTransactions
                    .Include(t => t.SalesItems).ThenInclude(i => i.Product)
                    .FirstOrDefaultAsync(t => t.TransactionId == id);

                if (transaction == null) return NotFound();

                // Use a transaction to ensure data consistency
                using var dbTransaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    foreach (var item in transaction.SalesItems)
                    {
                        if (item.Product != null)
                        {
                            item.Product.QuantityOnHand += item.Quantity;
                            item.Product.StockStatus = CalculateStockStatus(item.Product.QuantityOnHand, item.Product.ReorderLevel);

                            _context.InventoryTransactions.Add(new InventoryTransaction
                            {
                                ProductId = item.ProductId,
                                TransactionType = "SaleReversal",
                                Quantity = item.Quantity,
                                UnitCost = item.Product.AverageCost,
                                ReferenceId = transaction.TransactionId,
                                ReferenceType = "Sale",
                                StaffId = staffId.Value,
                                TransactionDate = DateTime.Now,
                                Remarks = $"Sale {transaction.InvoiceNumber} cancelled, {item.Quantity} units returned"
                            });
                        }
                    }

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        StaffId = staffId.Value,
                        Action = "Cancel Sale",
                        Module = "Sales",
                        Description = $"Sale {transaction.InvoiceNumber} cancelled, stock restored."
                    });

                    _context.SalesTransactions.Remove(transaction);
                    await _context.SaveChangesAsync();

                    await dbTransaction.CommitAsync();

                    TempData["Success"] = "Sale cancelled and stock restored.";
                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    await dbTransaction.RollbackAsync();
                    throw; // Re-throw to be caught by outer catch block
                }
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while cancelling the sale. Please try again.";
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
