using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Hubs;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class ServiceTransactionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        public ServiceTransactionsController(ApplicationDbContext context, IHubContext<NotificationHub> hubContext) { _context = context; _hubContext = hubContext; }

        public async Task<IActionResult> Index(string? searchString, string? statusFilter, int page = 1)
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
                IQueryable<ServiceTransaction> query = _context.ServiceTransactions
                    .Include(t => t.Mechanic)
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    string s = searchString.ToLower();
                    query = query.Where(t => t.CustomerName.ToLower().Contains(s)
                        || (t.Make != null && t.Make.ToLower().Contains(s))
                        || (t.Model != null && t.Model.ToLower().Contains(s))
                        || (t.PlateNumber != null && t.PlateNumber.ToLower().Contains(s)));
                }

                if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All")
                {
                    query = query.Where(t => t.Status == statusFilter);
                }

                int total = await query.CountAsync();

                List<ServiceTransaction> transactions = await query
                    .OrderByDescending(t => t.Date)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["CurrentFilter"] = searchString;
                ViewData["StatusFilter"] = statusFilter;

                return View(transactions);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading service transactions. Please try again.";
                return View(new List<ServiceTransaction>());
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
                ViewBag.Mechanics = new SelectList(
                    await _context.Mechanics.AsNoTracking().OrderBy(m => m.MechanicName).ToListAsync(),
                    "MechanicId", "MechanicName");

                ViewBag.Products = new SelectList(
                    await _context.Products.AsNoTracking().Where(p => p.QuantityOnHand > 0).OrderBy(p => p.ProductName).ToListAsync(),
                    "ProductId", "ProductName");

                return View();
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading the service transaction form. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ServiceTransaction transaction)
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
                transaction.StaffId = staffId.Value;
                transaction.Status = "Pending";
                transaction.Date = DateTime.Now;

                // Validate required fields
                if (string.IsNullOrWhiteSpace(transaction.CustomerName))
                {
                    ModelState.AddModelError("CustomerName", "Customer name is required.");
                }

                if (transaction.ServiceFee < 0)
                {
                    ModelState.AddModelError("ServiceFee", "Service fee cannot be negative.");
                }

                if (transaction.MechanicId <= 0)
                {
                    ModelState.AddModelError("MechanicId", "Please select a mechanic.");
                }

                if (ModelState.IsValid)
                {
                    // Use a transaction to ensure data consistency
                    using var dbTransaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        List<(int ProductId, int Quantity)> parts = ParsePartsFromForm();

                        foreach (var part in parts)
                        {
                            Product? product = await _context.Products.FindAsync(part.ProductId);
                            if (product == null)
                            {
                                ModelState.AddModelError("", $"Product with ID {part.ProductId} not found.");
                                ViewBag.Mechanics = new SelectList(
                                    await _context.Mechanics.AsNoTracking().OrderBy(m => m.MechanicName).ToListAsync(),
                                    "MechanicId", "MechanicName", transaction.MechanicId);
                                ViewBag.Products = new SelectList(
                                    await _context.Products.AsNoTracking().Where(p => p.QuantityOnHand > 0).OrderBy(p => p.ProductName).ToListAsync(),
                                    "ProductId", "ProductName");
                                return View(transaction);
                            }

                            if (product.QuantityOnHand < part.Quantity)
                            {
                                ModelState.AddModelError("", $"Insufficient stock for {product.ProductName}. Available: {product.QuantityOnHand}, Requested: {part.Quantity}");
                                ViewBag.Mechanics = new SelectList(
                                    await _context.Mechanics.AsNoTracking().OrderBy(m => m.MechanicName).ToListAsync(),
                                    "MechanicId", "MechanicName", transaction.MechanicId);
                                ViewBag.Products = new SelectList(
                                    await _context.Products.AsNoTracking().Where(p => p.QuantityOnHand > 0).OrderBy(p => p.ProductName).ToListAsync(),
                                    "ProductId", "ProductName");
                                return View(transaction);
                            }

                            product.QuantityOnHand -= part.Quantity;
                            UpdateStockStatus(product);

                            transaction.PartsUsed.Add(new ServicePartUsed
                            {
                                ProductId = part.ProductId,
                                Quantity = part.Quantity
                            });
                        }

                        _context.ServiceTransactions.Add(transaction);
                        await _context.SaveChangesAsync();

                        // Record inventory transactions for parts used
                        foreach (var part in transaction.PartsUsed)
                        {
                            Product? usedProduct = await _context.Products.FindAsync(part.ProductId);
                            _context.InventoryTransactions.Add(new InventoryTransaction
                            {
                                ProductId = part.ProductId,
                                TransactionType = "ServiceUse",
                                Quantity = -part.Quantity,
                                UnitCost = usedProduct?.AverageCost ?? 0,
                                ReferenceId = transaction.ServiceTxnId,
                                ReferenceType = "ServiceTransaction",
                                StaffId = staffId,
                                TransactionDate = DateTime.Now,
                                Remarks = $"Service #{transaction.ServiceTxnId} - {transaction.CustomerName}"
                            });
                        }

                        _context.ActivityLogs.Add(new ActivityLog
                        {
                            Action = "Create",
                            Module = "ServiceTransaction",
                            Description = $"Created service transaction for {transaction.CustomerName} - Fee: {transaction.ServiceFee:N2}",
                            StaffId = staffId,
                            Timestamp = DateTime.Now
                        });
                        await _context.SaveChangesAsync();

                        await dbTransaction.CommitAsync();

                        await _hubContext.Clients.All.SendAsync("ReceiveNotification",
                            $"New service: {transaction.CustomerName} - ₱{transaction.ServiceFee:N2}", "success");
                        await _hubContext.Clients.All.SendAsync("DashboardUpdated");

                        TempData["SuccessMessage"] = "Service transaction created successfully.";
                        return RedirectToAction(nameof(Index));
                    }
                    catch
                    {
                        await dbTransaction.RollbackAsync();
                        throw; // Re-throw to be caught by outer catch block
                    }
                }

                ViewBag.Mechanics = new SelectList(
                    await _context.Mechanics.AsNoTracking().OrderBy(m => m.MechanicName).ToListAsync(),
                    "MechanicId", "MechanicName", transaction.MechanicId);
                ViewBag.Products = new SelectList(
                    await _context.Products.AsNoTracking().Where(p => p.QuantityOnHand > 0).OrderBy(p => p.ProductName).ToListAsync(),
                    "ProductId", "ProductName");

                return View(transaction);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while creating the service transaction. Please try again.";
                ViewBag.Mechanics = new SelectList(
                    await _context.Mechanics.AsNoTracking().OrderBy(m => m.MechanicName).ToListAsync(),
                    "MechanicId", "MechanicName", transaction.MechanicId);
                ViewBag.Products = new SelectList(
                    await _context.Products.AsNoTracking().Where(p => p.QuantityOnHand > 0).OrderBy(p => p.ProductName).ToListAsync(),
                    "ProductId", "ProductName");
                return View(transaction);
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
                ServiceTransaction? transaction = await _context.ServiceTransactions
                    .Include(t => t.PartsUsed).ThenInclude(p => p.Product)
                    .Include(t => t.Mechanic)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.ServiceTxnId == id);

                if (transaction == null) return NotFound();

                ViewBag.Mechanics = new SelectList(
                    await _context.Mechanics.AsNoTracking().OrderBy(m => m.MechanicName).ToListAsync(),
                    "MechanicId", "MechanicName", transaction.MechanicId);

                ViewBag.Products = new SelectList(
                    await _context.Products.AsNoTracking().Where(p => p.QuantityOnHand > 0).OrderBy(p => p.ProductName).ToListAsync(),
                    "ProductId", "ProductName");

                return View(transaction);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading the service transaction for editing. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ServiceTransaction transaction)
        {
            // Validate session
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            if (id != transaction.ServiceTxnId) return NotFound();

            try
            {
                ServiceTransaction? existing = await _context.ServiceTransactions
                    .Include(t => t.PartsUsed)
                    .FirstOrDefaultAsync(t => t.ServiceTxnId == id);

                if (existing == null) return NotFound();

                // Validate required fields
                if (string.IsNullOrWhiteSpace(transaction.CustomerName))
                {
                    ModelState.AddModelError("CustomerName", "Customer name is required.");
                }

                if (transaction.ServiceFee < 0)
                {
                    ModelState.AddModelError("ServiceFee", "Service fee cannot be negative.");
                }

                if (transaction.MechanicId <= 0)
                {
                    ModelState.AddModelError("MechanicId", "Please select a mechanic.");
                }

                if (ModelState.IsValid)
                {
                    // Use a transaction to ensure data consistency
                    using var dbTransaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        string oldStatus = existing.Status;
                        bool completed = oldStatus != "Completed" && transaction.Status == "Completed";

                        existing.CustomerName = transaction.CustomerName;
                        existing.Make = transaction.Make;
                        existing.Model = transaction.Model;
                        existing.Year = transaction.Year;
                        existing.PlateNumber = transaction.PlateNumber;
                        existing.ServiceDescription = transaction.ServiceDescription;
                        existing.ServiceFee = transaction.ServiceFee;
                        existing.Date = transaction.Date;
                        existing.Status = transaction.Status;
                        existing.MechanicId = transaction.MechanicId;

                        // Record reversals for old parts and restore stock
                        foreach (var oldPart in existing.PartsUsed)
                        {
                            Product? oldProduct = await _context.Products.FindAsync(oldPart.ProductId);
                            if (oldProduct != null)
                            {
                                oldProduct.QuantityOnHand += oldPart.Quantity;
                                UpdateStockStatus(oldProduct);
                            }

                            _context.InventoryTransactions.Add(new InventoryTransaction
                            {
                                ProductId = oldPart.ProductId,
                                TransactionType = "ServiceUseReversal",
                                Quantity = oldPart.Quantity,
                                UnitCost = 0,
                                ReferenceId = existing.ServiceTxnId,
                                ReferenceType = "ServiceTransaction",
                                StaffId = staffId,
                                TransactionDate = DateTime.Now,
                                Remarks = $"Service #{existing.ServiceTxnId} edit - restored part"
                            });
                        }
                        existing.PartsUsed.Clear();

                        List<(int ProductId, int Quantity)> newParts = ParsePartsFromForm();
                        foreach (var part in newParts)
                        {
                            Product? product = await _context.Products.FindAsync(part.ProductId);
                            if (product == null)
                            {
                                ModelState.AddModelError("", $"Product with ID {part.ProductId} not found.");
                                ViewBag.Mechanics = new SelectList(
                                    await _context.Mechanics.AsNoTracking().OrderBy(m => m.MechanicName).ToListAsync(),
                                    "MechanicId", "MechanicName", transaction.MechanicId);
                                ViewBag.Products = new SelectList(
                                    await _context.Products.AsNoTracking().Where(p => p.QuantityOnHand > 0).OrderBy(p => p.ProductName).ToListAsync(),
                                    "ProductId", "ProductName");
                                return View(transaction);
                            }

                            if (product.QuantityOnHand < part.Quantity)
                            {
                                ModelState.AddModelError("", $"Insufficient stock for {product.ProductName}. Available: {product.QuantityOnHand}, Requested: {part.Quantity}");
                                ViewBag.Mechanics = new SelectList(
                                    await _context.Mechanics.AsNoTracking().OrderBy(m => m.MechanicName).ToListAsync(),
                                    "MechanicId", "MechanicName", transaction.MechanicId);
                                ViewBag.Products = new SelectList(
                                    await _context.Products.AsNoTracking().Where(p => p.QuantityOnHand > 0).OrderBy(p => p.ProductName).ToListAsync(),
                                    "ProductId", "ProductName");
                                return View(transaction);
                            }

                            product.QuantityOnHand -= part.Quantity;
                            UpdateStockStatus(product);

                            existing.PartsUsed.Add(new ServicePartUsed
                            {
                                ProductId = part.ProductId,
                                Quantity = part.Quantity
                            });

                            _context.InventoryTransactions.Add(new InventoryTransaction
                            {
                                ProductId = part.ProductId,
                                TransactionType = "ServiceUse",
                                Quantity = -part.Quantity,
                                UnitCost = product.AverageCost,
                                ReferenceId = existing.ServiceTxnId,
                                ReferenceType = "ServiceTransaction",
                                StaffId = staffId,
                                TransactionDate = DateTime.Now,
                                Remarks = $"Service #{existing.ServiceTxnId} edit - used part"
                            });
                        }

                        string logDesc = $"Edited service transaction #{existing.ServiceTxnId} for {existing.CustomerName}";
                        if (completed) logDesc += " (Completed)";

                        _context.ActivityLogs.Add(new ActivityLog
                        {
                            Action = "Edit",
                            Module = "ServiceTransaction",
                            Description = logDesc,
                            StaffId = staffId,
                            Timestamp = DateTime.Now
                        });

                        await _context.SaveChangesAsync();
                        await dbTransaction.CommitAsync();

                        await _hubContext.Clients.All.SendAsync("ReceiveNotification",
                            $"Service #{existing.ServiceTxnId} updated - {existing.CustomerName}", "info");
                        await _hubContext.Clients.All.SendAsync("DashboardUpdated");

                        TempData["SuccessMessage"] = "Service transaction updated successfully.";
                        return RedirectToAction(nameof(Index));
                    }
                    catch
                    {
                        await dbTransaction.RollbackAsync();
                        throw; // Re-throw to be caught by outer catch block
                    }
                }

                ViewBag.Mechanics = new SelectList(
                    await _context.Mechanics.AsNoTracking().OrderBy(m => m.MechanicName).ToListAsync(),
                    "MechanicId", "MechanicName", transaction.MechanicId);
                ViewBag.Products = new SelectList(
                    await _context.Products.AsNoTracking().Where(p => p.QuantityOnHand > 0).OrderBy(p => p.ProductName).ToListAsync(),
                    "ProductId", "ProductName");

                return View(transaction);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while updating the service transaction. Please try again.";
                ViewBag.Mechanics = new SelectList(
                    await _context.Mechanics.AsNoTracking().OrderBy(m => m.MechanicName).ToListAsync(),
                    "MechanicId", "MechanicName", transaction.MechanicId);
                ViewBag.Products = new SelectList(
                    await _context.Products.AsNoTracking().Where(p => p.QuantityOnHand > 0).OrderBy(p => p.ProductName).ToListAsync(),
                    "ProductId", "ProductName");
                return View(transaction);
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
                ServiceTransaction? transaction = await _context.ServiceTransactions
                    .Include(t => t.Mechanic)
                    .Include(t => t.Staff)
                    .Include(t => t.PartsUsed).ThenInclude(p => p.Product)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.ServiceTxnId == id);

                if (transaction == null) return NotFound();
                return View(transaction);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading service transaction details. Please try again.";
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
                ServiceTransaction? transaction = await _context.ServiceTransactions
                    .Include(t => t.Mechanic)
                    .Include(t => t.Staff)
                    .Include(t => t.PartsUsed).ThenInclude(p => p.Product)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.ServiceTxnId == id);

                if (transaction == null) return NotFound();
                return View(transaction);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading the service transaction for deletion. Please try again.";
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
                ServiceTransaction? transaction = await _context.ServiceTransactions
                    .Include(t => t.PartsUsed).ThenInclude(p => p.Product)
                    .FirstOrDefaultAsync(t => t.ServiceTxnId == id);

                if (transaction == null) return NotFound();

                // Use a transaction to ensure data consistency
                using var dbTransaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    foreach (var part in transaction.PartsUsed)
                    {
                        if (part.Product != null)
                        {
                            part.Product.QuantityOnHand += part.Quantity;
                            UpdateStockStatus(part.Product);
                        }
                    }

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        Action = "Delete",
                        Module = "ServiceTransaction",
                        Description = $"Deleted service transaction #{transaction.ServiceTxnId} for {transaction.CustomerName}. Stock restored.",
                        StaffId = staffId,
                        Timestamp = DateTime.Now
                    });

                    _context.ServiceTransactions.Remove(transaction);
                    await _context.SaveChangesAsync();

                    await dbTransaction.CommitAsync();

                    await _hubContext.Clients.All.SendAsync("ReceiveNotification",
                        $"Service #{transaction.ServiceTxnId} cancelled - {transaction.CustomerName}", "warning");
                    await _hubContext.Clients.All.SendAsync("DashboardUpdated");

                    TempData["SuccessMessage"] = "Service transaction deleted successfully.";
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
                TempData["ErrorMessage"] = "An error occurred while deleting the service transaction. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        private List<(int ProductId, int Quantity)> ParsePartsFromForm()
        {
            var parts = new List<(int ProductId, int Quantity)>();
            for (int index = 0; index < 100; index++)
            {
                if (!Request.Form.ContainsKey($"Items[{index}].ProductId"))
                    continue;
                if (int.TryParse(Request.Form[$"Items[{index}].ProductId"], out int pid) && pid > 0 &&
                    int.TryParse(Request.Form[$"Items[{index}].Quantity"], out int qty) && qty > 0)
                {
                    parts.Add((pid, qty));
                }
            }
            return parts;
        }

        private static void UpdateStockStatus(Product product)
        {
            if (product.QuantityOnHand <= 0)
                product.StockStatus = "Out of Stock";
            else if (product.QuantityOnHand <= product.ReorderLevel)
                product.StockStatus = "Low Stock";
            else
                product.StockStatus = "Available";
        }
    }
}
