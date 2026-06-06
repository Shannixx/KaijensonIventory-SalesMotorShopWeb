using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class PurchaseOrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PurchaseOrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? searchString, string? statusFilter, int page = 1)
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                int pageSize = 10;
                IQueryable<PurchaseOrder> query = _context.PurchaseOrders
                    .Include(p => p.Supplier)
                    .Include(p => p.Staff)
                    .Include(p => p.Items)
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    string s = searchString.ToLower();
                    query = query.Where(p => p.PONumber.ToLower().Contains(s) || p.Supplier!.CompanyName.ToLower().Contains(s));
                }

                if (!string.IsNullOrWhiteSpace(statusFilter))
                {
                    query = query.Where(p => p.Status == statusFilter);
                }

                int total = await query.CountAsync();

                var orders = await query
                    .OrderByDescending(p => p.OrderDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["CurrentFilter"] = searchString;
                ViewData["StatusFilter"] = statusFilter;

                return View(orders);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading purchase orders.";
                return View(new List<PurchaseOrder>());
            }
        }

        public async Task<IActionResult> Create()
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                ViewBag.PONumber = $"PO-{DateTime.Now:yyyyMMddHHmmss}";
                ViewBag.Suppliers = new SelectList(
                    await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                    "SupplierId", "CompanyName");
                ViewBag.Products = await _context.Products
                    .Include(p => p.Supplier)
                    .OrderBy(p => p.ProductName)
                    .AsNoTracking()
                    .ToListAsync();
                return View();
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading the purchase order form.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PurchaseOrder model, List<PurchaseOrderItem> items)
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                if (items == null || items.Count == 0)
                {
                    ModelState.AddModelError("", "At least one item is required.");
                }

                if (model.SupplierId <= 0)
                {
                    ModelState.AddModelError("SupplierId", "Please select a supplier.");
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.PONumber = $"PO-{DateTime.Now:yyyyMMddHHmmss}";
                    ViewBag.Suppliers = new SelectList(
                        await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                        "SupplierId", "CompanyName", model.SupplierId);
                    ViewBag.Products = await _context.Products
                        .Include(p => p.Supplier)
                        .OrderBy(p => p.ProductName)
                        .AsNoTracking()
                        .ToListAsync();
                    return View(model);
                }

                model.PONumber = $"PO-{DateTime.Now:yyyyMMddHHmmss}";
                model.StaffId = staffId.Value;
                model.Status = "Draft";
                model.TotalAmount = items?.Sum(i => i.Total) ?? 0;

                _context.PurchaseOrders.Add(model);
                await _context.SaveChangesAsync();

                foreach (var item in items)
                {
                    item.PurchaseOrderId = model.PurchaseOrderId;
                    item.Total = item.Quantity * item.UnitCost;
                    _context.PurchaseOrderItems.Add(item);
                }

                await _context.SaveChangesAsync();

                model.TotalAmount = items.Sum(i => i.Total);
                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = staffId,
                    Action = "Create Purchase Order",
                    Module = "PurchaseOrder",
                    Description = $"Created PO #{model.PONumber} for supplier {model.Supplier?.CompanyName ?? "Unknown"} - Total: {model.TotalAmount:N2}"
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Purchase Order #{model.PONumber} created successfully.";
                return RedirectToAction(nameof(Details), new { id = model.PurchaseOrderId });
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while creating the purchase order.";
                ViewBag.PONumber = $"PO-{DateTime.Now:yyyyMMddHHmmss}";
                ViewBag.Suppliers = new SelectList(
                    await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                    "SupplierId", "CompanyName", model.SupplierId);
                ViewBag.Products = await _context.Products
                    .Include(p => p.Supplier)
                    .OrderBy(p => p.ProductName)
                    .AsNoTracking()
                    .ToListAsync();
                return View(model);
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var order = await _context.PurchaseOrders
                    .Include(p => p.Supplier)
                    .Include(p => p.Staff)
                    .Include(p => p.Items).ThenInclude(i => i.Product)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PurchaseOrderId == id);

                if (order == null) return NotFound();

                return View(order);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading purchase order details.";
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var order = await _context.PurchaseOrders
                    .Include(p => p.Items)
                    .FirstOrDefaultAsync(p => p.PurchaseOrderId == id);

                if (order == null) return NotFound();

                if (order.Status != "Draft")
                {
                    TempData["ErrorMessage"] = "Only draft purchase orders can be edited.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                ViewBag.Suppliers = new SelectList(
                    await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                    "SupplierId", "CompanyName", order.SupplierId);
                ViewBag.Products = await _context.Products
                    .Include(p => p.Supplier)
                    .OrderBy(p => p.ProductName)
                    .AsNoTracking()
                    .ToListAsync();

                return View(order);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading the purchase order for editing.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PurchaseOrder model, List<PurchaseOrderItem> items)
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            if (id != model.PurchaseOrderId) return NotFound();

            try
            {
                var existing = await _context.PurchaseOrders
                    .Include(p => p.Items)
                    .FirstOrDefaultAsync(p => p.PurchaseOrderId == id);

                if (existing == null) return NotFound();

                if (existing.Status != "Draft")
                {
                    TempData["ErrorMessage"] = "Only draft purchase orders can be edited.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                if (items == null || items.Count == 0)
                {
                    ModelState.AddModelError("", "At least one item is required.");
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.Suppliers = new SelectList(
                        await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                        "SupplierId", "CompanyName", model.SupplierId);
                    ViewBag.Products = await _context.Products
                        .Include(p => p.Supplier)
                        .OrderBy(p => p.ProductName)
                        .AsNoTracking()
                        .ToListAsync();
                    return View(model);
                }

                existing.SupplierId = model.SupplierId;
                existing.ExpectedDate = model.ExpectedDate;
                existing.Notes = model.Notes;

                _context.PurchaseOrderItems.RemoveRange(existing.Items ?? new List<PurchaseOrderItem>());

                foreach (var item in items ?? new List<PurchaseOrderItem>())
                {
                    item.PurchaseOrderId = id;
                    item.Total = item.Quantity * item.UnitCost;
                    _context.PurchaseOrderItems.Add(item);
                }

                existing.TotalAmount = items.Sum(i => i.Total);
                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = staffId,
                    Action = "Edit Purchase Order",
                    Module = "PurchaseOrder",
                    Description = $"Edited PO #{existing.PONumber}"
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Purchase Order #{existing.PONumber} updated successfully.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while updating the purchase order.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var order = await _context.PurchaseOrders
                    .Include(p => p.Items)
                    .FirstOrDefaultAsync(p => p.PurchaseOrderId == id);

                if (order == null) return NotFound();

                string[] validStatuses = { "Draft", "Ordered", "Partially Received", "Received", "Cancelled" };
                if (!validStatuses.Contains(status))
                {
                    TempData["ErrorMessage"] = "Invalid status.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                string oldStatus = order.Status;
                order.Status = status;

                if (status == "Received")
                {
                    foreach (var item in order.Items)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            int oldQty = product.QuantityOnHand;
                            decimal oldAvgCost = product.AverageCost;
                            product.QuantityOnHand += item.Quantity;
                            product.AverageCost = oldQty > 0
                                ? Math.Round(((oldAvgCost * oldQty) + (item.UnitCost * item.Quantity)) / product.QuantityOnHand, 2)
                                : item.UnitCost;
                            product.StockStatus = CalculateStockStatus(product.QuantityOnHand, product.ReorderLevel);
                            product.LastStockInDate = DateTime.Now;

                            _context.InventoryTransactions.Add(new InventoryTransaction
                            {
                                ProductId = item.ProductId,
                                TransactionType = "PO Receiving",
                                Quantity = item.Quantity,
                                UnitCost = item.UnitCost,
                                ReferenceId = order.PurchaseOrderId,
                                ReferenceType = "PurchaseOrder",
                                StaffId = staffId,
                                TransactionDate = DateTime.Now,
                                Remarks = $"PO #{order.PONumber} - {product.ProductName}"
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = staffId,
                    Action = "Update PO Status",
                    Module = "PurchaseOrder",
                    Description = $"PO #{order.PONumber} status changed from {oldStatus} to {status}"
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"PO #{order.PONumber} status updated to {status}.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while updating the purchase order status.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var order = await _context.PurchaseOrders
                    .Include(p => p.Items)
                    .FirstOrDefaultAsync(p => p.PurchaseOrderId == id);

                if (order == null) return NotFound();

                if (order.Status != "Draft" && order.Status != "Cancelled")
                {
                    TempData["ErrorMessage"] = "Only draft or cancelled purchase orders can be deleted.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                string poNumber = order.PONumber;
                _context.PurchaseOrderItems.RemoveRange(order.Items);
                _context.PurchaseOrders.Remove(order);
                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = staffId,
                    Action = "Delete Purchase Order",
                    Module = "PurchaseOrder",
                    Description = $"Deleted PO #{poNumber}"
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Purchase Order #{poNumber} deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the purchase order.";
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> CreateFromLowStock(int? productId)
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                ViewBag.PONumber = $"PO-{DateTime.Now:yyyyMMddHHmmss}";
                ViewBag.Suppliers = new SelectList(
                    await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                    "SupplierId", "CompanyName");

                var lowStockProducts = await _context.Products
                    .Where(p => p.QuantityOnHand <= p.ReorderLevel)
                    .Include(p => p.Supplier)
                    .OrderBy(p => p.ProductName)
                    .AsNoTracking()
                    .ToListAsync();

                ViewBag.LowStockProducts = lowStockProducts;

                var preselected = productId.HasValue
                    ? lowStockProducts.FirstOrDefault(p => p.ProductId == productId.Value)
                    : null;

                if (preselected != null)
                {
                    ViewBag.PreselectedSupplierId = preselected.SupplierId;
                }

                return View(lowStockProducts);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFromLowStock(int supplierId, List<int> productIds, List<int> quantities, List<decimal> unitCosts)
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                if (productIds == null || productIds.Count == 0)
                {
                    TempData["ErrorMessage"] = "Please select at least one product.";
                    return RedirectToAction(nameof(CreateFromLowStock));
                }

                var order = new PurchaseOrder
                {
                    PONumber = $"PO-{DateTime.Now:yyyyMMddHHmmss}",
                    SupplierId = supplierId,
                    Status = "Draft",
                    StaffId = staffId.Value
                };

                _context.PurchaseOrders.Add(order);
                await _context.SaveChangesAsync();

                for (int i = 0; i < productIds.Count; i++)
                {
                    var item = new PurchaseOrderItem
                    {
                        PurchaseOrderId = order.PurchaseOrderId,
                        ProductId = productIds[i],
                        Quantity = quantities[i],
                        UnitCost = unitCosts[i],
                        Total = quantities[i] * unitCosts[i]
                    };
                    _context.PurchaseOrderItems.Add(item);
                }

                await _context.SaveChangesAsync();

                order.TotalAmount = order.Items.Sum(i => i.Total);
                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = staffId,
                    Action = "Create Purchase Order from Low Stock",
                    Module = "PurchaseOrder",
                    Description = $"Created PO #{order.PONumber} from low stock alert"
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Purchase Order #{order.PONumber} created from low stock items.";
                return RedirectToAction(nameof(Details), new { id = order.PurchaseOrderId });
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while creating the purchase order.";
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
