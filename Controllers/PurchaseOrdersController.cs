using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class PurchaseOrdersController : BaseController
    {
        private readonly ApplicationDbContext _context;

        public PurchaseOrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool IsOwnerOrManager()
        {
            string? role = HttpContext.Session.GetString("StaffRole");
            return string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        private IActionResult? CheckAccess()
        {
            if (!IsSessionValid())
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }
            if (!IsOwnerOrManager())
            {
                TempData["ErrorMessage"] = "Access denied. Owner or Manager privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }
            return null;
        }

        public async Task<IActionResult> Index(string? searchString, string? statusFilter, int page = 1)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            try
            {
                int pageSize = 10;
                IQueryable<PurchaseOrder> query = _context.PurchaseOrders
                    .Include(p => p.Supplier)
                    .Include(p => p.Staff)
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    string s = searchString.ToLower();
                    query = query.Where(p =>
                        p.PurchaseOrderNumber.ToLower().Contains(s) ||
                        p.Supplier!.CompanyName.ToLower().Contains(s));
                }

                if (!string.IsNullOrWhiteSpace(statusFilter) &&
                    (statusFilter == "Pending" || statusFilter == "Approved" ||
                     statusFilter == "Delivered" || statusFilter == "Cancelled"))
                {
                    query = query.Where(p => p.Status == statusFilter);
                }

                int total = await query.CountAsync();

                List<PurchaseOrder> orders = await query
                    .OrderByDescending(p => p.CreatedDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["CurrentFilter"] = searchString;
                ViewData["StatusFilter"] = statusFilter;
                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);

                return View(orders);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading purchase orders. Please try again.";
                return View(new List<PurchaseOrder>());
            }
        }

        public async Task<IActionResult> Create()
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            try
            {
                ViewBag.Suppliers = new SelectList(
                    await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                    "SupplierId", "CompanyName");

                ViewBag.Products = await _context.Products
                    .AsNoTracking()
                    .OrderBy(p => p.ProductName)
                    .Select(p => new { p.ProductId, p.ProductName, p.Brand, p.QuantityOnHand, p.Price })
                    .ToListAsync();

                return View(new PurchaseOrderViewModel());
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading the form. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PurchaseOrderViewModel viewModel)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            try
            {
                if (viewModel.SupplierId <= 0)
                {
                    ModelState.AddModelError("SupplierId", "Please select a supplier.");
                }

                if (viewModel.Items == null || viewModel.Items.Count == 0 || viewModel.Items.All(i => i.ProductId <= 0))
                {
                    ModelState.AddModelError("Items", "Please add at least one product.");
                }
                else
                {
                    var duplicateProducts = viewModel.Items
                        .Where(i => i.ProductId > 0)
                        .GroupBy(i => i.ProductId)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key)
                        .ToList();

                    if (duplicateProducts.Any())
                    {
                        ModelState.AddModelError("Items", "Duplicate products are not allowed.");
                    }

                    for (int i = 0; i < viewModel.Items.Count; i++)
                    {
                        var item = viewModel.Items[i];
                        if (item.ProductId > 0 && item.Quantity <= 0)
                        {
                            ModelState.AddModelError($"Items[{i}].Quantity", "Quantity must be greater than 0.");
                        }
                    }
                }

                if (ModelState.IsValid)
                {
                    string poNumber = await GeneratePONumberAsync();

                    var productIds = (viewModel.Items ?? [])
                        .Where(i => i != null && i.ProductId > 0)
                        .Select(i => i.ProductId)
                        .Distinct()
                        .ToList();

                    var products = await _context.Products
                        .AsNoTracking()
                        .Where(p => productIds.Contains(p.ProductId))
                        .ToDictionaryAsync(p => p.ProductId, p => p.Price);

                    var order = new PurchaseOrder
                    {
                        PurchaseOrderNumber = poNumber,
                        SupplierId = viewModel.SupplierId,
                        OrderDate = viewModel.OrderDate,
                        ExpectedDeliveryDate = viewModel.ExpectedDeliveryDate,
                        Status = "Pending",
                        Remarks = viewModel.Remarks,
                        CreatedBy = GetCurrentStaffId(),
                        CreatedDate = DateTime.Now
                    };

                    decimal totalAmount = 0;

                    foreach (var item in (viewModel.Items ?? []).Where(i => i != null && i.ProductId > 0))
                    {
                        var productPrice = products.GetValueOrDefault(item.ProductId, 0);
                        var poItem = new PurchaseOrderItem
                        {
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            Price = productPrice,
                            Subtotal = item.Quantity * productPrice
                        };
                        order.Items.Add(poItem);
                        totalAmount += poItem.Subtotal;
                    }

                    order.TotalAmount = totalAmount;

                    _context.PurchaseOrders.Add(order);
                    await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        StaffId = GetCurrentStaffId(),
                        Action = "Create Purchase Order",
                        Module = "PurchaseOrder",
                        Description = $"Created PO {poNumber} - Total: {totalAmount:N2}"
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Purchase Order {poNumber} created successfully.";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while creating the purchase order. Please try again.";
            }

            ViewBag.Suppliers = new SelectList(
                await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                "SupplierId", "CompanyName", viewModel.SupplierId);

            ViewBag.Products = await _context.Products
                .AsNoTracking()
                .OrderBy(p => p.ProductName)
                .Select(p => new { p.ProductId, p.ProductName, p.Brand, p.QuantityOnHand, p.Price })
                .ToListAsync();

            return View(viewModel);
        }

        public async Task<IActionResult> Details(int id)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            try
            {
                var order = await _context.PurchaseOrders
                    .Include(p => p.Supplier)
                    .Include(p => p.Staff)
                    .Include(p => p.Items).ThenInclude(i => i.Product)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PurchaseOrderId == id);

                if (order == null) return NotFound();

                var viewModel = new PurchaseOrderViewModel
                {
                    PurchaseOrderId = order.PurchaseOrderId,
                    PurchaseOrderNumber = order.PurchaseOrderNumber,
                    SupplierId = order.SupplierId,
                    SupplierName = order.Supplier?.CompanyName,
                    ContactPerson = order.Supplier?.ContactPerson,
                    ContactNumber = order.Supplier?.ContactNumber,
                    SupplierAddress = order.Supplier?.Address,
                    OrderDate = order.OrderDate,
                    ExpectedDeliveryDate = order.ExpectedDeliveryDate,
                    Status = order.Status,
                    TotalAmount = order.TotalAmount,
                    Remarks = order.Remarks,
                    CreatedByName = order.Staff?.StaffName,
                    CreatedDate = order.CreatedDate,
                    UpdatedDate = order.UpdatedDate,
                    Items = order.Items.Select(i => new PurchaseOrderItemViewModel
                    {
                        PurchaseOrderItemId = i.PurchaseOrderItemId,
                        ProductId = i.ProductId,
                        ProductName = i.Product?.ProductName,
                        Brand = i.Product?.Brand,
                        CurrentStock = i.Product?.QuantityOnHand ?? 0,
                        Quantity = i.Quantity,
                        Price = i.Price,
                        Subtotal = i.Subtotal
                    }).ToList()
                };

                return View(viewModel);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading purchase order details. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            try
            {
                var order = await _context.PurchaseOrders
                    .Include(p => p.Supplier)
                    .Include(p => p.Items).ThenInclude(i => i.Product)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PurchaseOrderId == id);

                if (order == null) return NotFound();

                if (order.Status != "Pending")
                {
                    TempData["ErrorMessage"] = "Only pending purchase orders can be edited.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var viewModel = new PurchaseOrderViewModel
                {
                    PurchaseOrderId = order.PurchaseOrderId,
                    PurchaseOrderNumber = order.PurchaseOrderNumber,
                    SupplierId = order.SupplierId,
                    OrderDate = order.OrderDate,
                    ExpectedDeliveryDate = order.ExpectedDeliveryDate,
                    Status = order.Status,
                    TotalAmount = order.TotalAmount,
                    Remarks = order.Remarks,
                    Items = order.Items.Select(i => new PurchaseOrderItemViewModel
                    {
                        PurchaseOrderItemId = i.PurchaseOrderItemId,
                        ProductId = i.ProductId,
                        ProductName = i.Product?.ProductName,
                        Brand = i.Product?.Brand,
                        CurrentStock = i.Product?.QuantityOnHand ?? 0,
                        Quantity = i.Quantity,
                        Price = i.Price,
                        Subtotal = i.Subtotal
                    }).ToList()
                };

                ViewBag.Suppliers = new SelectList(
                    await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                    "SupplierId", "CompanyName", order.SupplierId);

                ViewBag.Products = await _context.Products
                    .AsNoTracking()
                    .OrderBy(p => p.ProductName)
                    .Select(p => new { p.ProductId, p.ProductName, p.Brand, p.QuantityOnHand, p.Price })
                    .ToListAsync();

                return View(viewModel);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading the purchase order for editing. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PurchaseOrderViewModel viewModel)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            if (id != viewModel.PurchaseOrderId) return NotFound();

            try
            {
                var order = await _context.PurchaseOrders
                    .Include(p => p.Items)
                    .FirstOrDefaultAsync(p => p.PurchaseOrderId == id);

                if (order == null) return NotFound();

                if (order.Status != "Pending")
                {
                    TempData["ErrorMessage"] = "Only pending purchase orders can be edited.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                if (viewModel.SupplierId <= 0)
                {
                    ModelState.AddModelError("SupplierId", "Please select a supplier.");
                }

                if (viewModel.Items == null || viewModel.Items.Count == 0 || viewModel.Items.All(i => i.ProductId <= 0))
                {
                    ModelState.AddModelError("Items", "Please add at least one product.");
                }
                else
                {
                    var duplicateProducts = viewModel.Items
                        .Where(i => i.ProductId > 0)
                        .GroupBy(i => i.ProductId)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key)
                        .ToList();

                    if (duplicateProducts.Any())
                    {
                        ModelState.AddModelError("Items", "Duplicate products are not allowed.");
                    }

                    for (int i = 0; i < viewModel.Items.Count; i++)
                    {
                        var item = viewModel.Items[i];
                        if (item.ProductId > 0 && item.Quantity <= 0)
                        {
                            ModelState.AddModelError($"Items[{i}].Quantity", "Quantity must be greater than 0.");
                        }
                    }
                }

                if (ModelState.IsValid)
                {
                    order.SupplierId = viewModel.SupplierId;
                    order.OrderDate = viewModel.OrderDate;
                    order.ExpectedDeliveryDate = viewModel.ExpectedDeliveryDate;
                    order.Remarks = viewModel.Remarks;
                    order.UpdatedDate = DateTime.Now;

                    var productIds = (viewModel.Items ?? [])
                        .Where(i => i != null && i.ProductId > 0)
                        .Select(i => i.ProductId)
                        .Distinct()
                        .ToList();

                    var products = await _context.Products
                        .AsNoTracking()
                        .Where(p => productIds.Contains(p.ProductId))
                        .ToDictionaryAsync(p => p.ProductId, p => p.Price);

                    _context.PurchaseOrderItems.RemoveRange(order.Items);

                    decimal totalAmount = 0;

                    foreach (var item in (viewModel.Items ?? []).Where(i => i != null && i.ProductId > 0))
                    {
                        var productPrice = products.GetValueOrDefault(item.ProductId, 0);
                        var poItem = new PurchaseOrderItem
                        {
                            PurchaseOrderId = order.PurchaseOrderId,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            Price = productPrice,
                            Subtotal = item.Quantity * productPrice
                        };
                        _context.PurchaseOrderItems.Add(poItem);
                        totalAmount += poItem.Subtotal;
                    }

                    order.TotalAmount = totalAmount;

                    await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        StaffId = GetCurrentStaffId(),
                        Action = "Edit Purchase Order",
                        Module = "PurchaseOrder",
                        Description = $"Edited PO {order.PurchaseOrderNumber} - Total: {totalAmount:N2}"
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Purchase Order {order.PurchaseOrderNumber} updated successfully.";
                    return RedirectToAction(nameof(Details), new { id = order.PurchaseOrderId });
                }
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while updating the purchase order. Please try again.";
            }

            ViewBag.Suppliers = new SelectList(
                await _context.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName).ToListAsync(),
                "SupplierId", "CompanyName", viewModel.SupplierId);

            ViewBag.Products = await _context.Products
                .AsNoTracking()
                .OrderBy(p => p.ProductName)
                .Select(p => new { p.ProductId, p.ProductName, p.Brand, p.QuantityOnHand, p.Price })
                .ToListAsync();

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            try
            {
                var order = await _context.PurchaseOrders
                    .Include(p => p.Items)
                    .FirstOrDefaultAsync(p => p.PurchaseOrderId == id);

                if (order == null)
                {
                    TempData["ErrorMessage"] = "The purchase order could not be found.";
                    return RedirectToAction(nameof(Index));
                }

                if (order.Status != "Pending")
                {
                    TempData["ErrorMessage"] = "Only pending purchase orders can be deleted.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                string poNumber = order.PurchaseOrderNumber;

                _context.PurchaseOrderItems.RemoveRange(order.Items);
                _context.PurchaseOrders.Remove(order);
                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = GetCurrentStaffId(),
                    Action = "Delete Purchase Order",
                    Module = "PurchaseOrder",
                    Description = $"Deleted PO {poNumber}"
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Purchase Order {poNumber} deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the purchase order. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            try
            {
                var order = await _context.PurchaseOrders
                    .Include(p => p.Items).ThenInclude(i => i.Product)
                    .FirstOrDefaultAsync(p => p.PurchaseOrderId == id);

                if (order == null)
                {
                    TempData["ErrorMessage"] = "The purchase order could not be found.";
                    return RedirectToAction(nameof(Index));
                }

                string oldStatus = order.Status;
                string[] validStatuses = { "Pending", "Approved", "Delivered", "Cancelled" };

                if (!validStatuses.Contains(status))
                {
                    TempData["ErrorMessage"] = "Invalid status.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                bool validTransition = (oldStatus == "Pending" && (status == "Approved" || status == "Cancelled")) ||
                                       (oldStatus == "Approved" && status == "Delivered");

                if (!validTransition)
                {
                    TempData["ErrorMessage"] = $"Cannot change status from '{oldStatus}' to '{status}'.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                order.Status = status;
                order.UpdatedDate = DateTime.Now;

                if (status == "Delivered")
                {
                    foreach (var item in order.Items)
                    {
                        if (item.Product != null)
                        {
                            item.Product.QuantityOnHand += item.Quantity;
                            item.Product.LastStockInDate = DateTime.Now;
                            item.Product.AverageCost = CalculateNewAverageCost(
                                item.Product.QuantityOnHand - item.Quantity,
                                item.Product.AverageCost,
                                item.Quantity,
                                item.Price);
                            item.Product.StockStatus = CalculateStockStatus(
                                item.Product.QuantityOnHand, item.Product.ReorderLevel);
                        }
                    }
                }

                await _context.SaveChangesAsync();

                string actionLabel = status switch
                {
                    "Approved" => "Approve Purchase Order",
                    "Delivered" => "Deliver Purchase Order",
                    "Cancelled" => "Cancel Purchase Order",
                    _ => "Update Status"
                };

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = GetCurrentStaffId(),
                    Action = actionLabel,
                    Module = "PurchaseOrder",
                    Description = $"{actionLabel} - PO {order.PurchaseOrderNumber} ({oldStatus} -> {status})"
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Purchase Order {order.PurchaseOrderNumber} status updated to '{status}'.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while updating the purchase order status. Please try again.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetSupplierInfo(int id)
        {
            var supplier = await _context.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SupplierId == id);

            if (supplier == null)
                return Json(new { contactPerson = "", contactNumber = "", address = "" });

            return Json(new
            {
                contactPerson = supplier.ContactPerson ?? "",
                contactNumber = supplier.ContactNumber ?? "",
                address = supplier.Address ?? ""
            });
        }

        [HttpGet]
        public async Task<JsonResult> GetProductInfo(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
                return Json(new { productName = "", brand = "", stock = 0, price = 0 });

            return Json(new
            {
                productName = product.ProductName ?? "",
                brand = product.Brand ?? "",
                stock = product.QuantityOnHand,
                price = product.Price
            });
        }

        public async Task<IActionResult> Print(int id)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            try
            {
                var order = await _context.PurchaseOrders
                    .Include(p => p.Supplier)
                    .Include(p => p.Staff)
                    .Include(p => p.Items).ThenInclude(i => i.Product)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PurchaseOrderId == id);

                if (order == null) return NotFound();

                byte[] pdfBytes = GeneratePurchaseOrderPdfBytes(order);

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = GetCurrentStaffId(),
                    Action = "Print Purchase Order",
                    Module = "PurchaseOrder",
                    Description = $"Printed PO {order.PurchaseOrderNumber}"
                });
                await _context.SaveChangesAsync();

                return File(pdfBytes, "application/pdf", $"PO-{order.PurchaseOrderNumber}.pdf");
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while generating the PDF. Please try again.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        private static byte[] GeneratePurchaseOrderPdfBytes(PurchaseOrder order)
        {
            using var ms = new MemoryStream();
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(c =>
                    {
                        c.Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Column(headerCol =>
                                {
                                    headerCol.Item().Text("KAIJENSON MOTOR SHOP").FontSize(18).Bold();
                                    headerCol.Item().Text("Purchase Order").FontSize(14);
                                });
                                row.ConstantItem(200).Column(infoCol =>
                                {
                                    infoCol.Item().AlignRight().Text($"PO #: {order.PurchaseOrderNumber}").Bold();
                                    infoCol.Item().AlignRight().Text($"Date: {order.OrderDate:MMM dd, yyyy}");
                                    if (order.ExpectedDeliveryDate.HasValue)
                                        infoCol.Item().AlignRight().Text($"Expected Delivery: {order.ExpectedDeliveryDate:MMM dd, yyyy}");
                                    infoCol.Item().AlignRight().Text($"Status: {order.Status}");
                                });
                            });
                            col.Item().PaddingVertical(10).LineHorizontal(1);
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Column(supplierCol =>
                                {
                                    supplierCol.Item().Text("Supplier Information").Bold().FontSize(11);
                                    supplierCol.Item().Text(order.Supplier?.CompanyName ?? "");
                                    supplierCol.Item().Text($"Contact: {order.Supplier?.ContactPerson ?? "N/A"}");
                                    supplierCol.Item().Text($"Phone: {order.Supplier?.ContactNumber ?? "N/A"}");
                                    supplierCol.Item().Text($"Address: {order.Supplier?.Address ?? "N/A"}");
                                });
                            });
                            col.Item().PaddingVertical(10).LineHorizontal(1);
                        });
                    });

                    page.Content().Element(c =>
                    {
                        c.Column(col =>
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(25);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.ConstantColumn(55);
                                    columns.ConstantColumn(65);
                                    columns.ConstantColumn(90);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background("#FF7F11").Padding(5).Text("#")
                                        .FontColor("#fff").FontSize(10).Bold();
                                    header.Cell().Background("#FF7F11").Padding(5).Text("Product")
                                        .FontColor("#fff").FontSize(10).Bold();
                                    header.Cell().Background("#FF7F11").Padding(5).Text("Brand")
                                        .FontColor("#fff").FontSize(10).Bold();
                                    header.Cell().Background("#FF7F11").Padding(5).AlignRight().Text("Price")
                                        .FontColor("#fff").FontSize(10).Bold();
                                    header.Cell().Background("#FF7F11").Padding(5).AlignRight().Text("Qty")
                                        .FontColor("#fff").FontSize(10).Bold();
                                    header.Cell().Background("#FF7F11").Padding(5).AlignRight().Text("Subtotal")
                                        .FontColor("#fff").FontSize(10).Bold();
                                });

                                int index = 1;
                                foreach (var item in order.Items)
                                {
                                    table.Cell().Padding(5).Text(index.ToString()).FontSize(10);
                                    table.Cell().Padding(5).Text(item.Product?.ProductName ?? "").FontSize(10);
                                    table.Cell().Padding(5).Text(item.Product?.Brand ?? "").FontSize(10);
                                    table.Cell().Padding(5).AlignRight().Text(item.Price.ToString("N2")).FontSize(10);
                                    table.Cell().Padding(5).AlignRight().Text(item.Quantity.ToString()).FontSize(10);
                                    table.Cell().Padding(5).AlignRight().Text(item.Subtotal.ToString("N2")).FontSize(10);
                                    index++;
                                }
                            });

                            col.Item().PaddingTop(10).AlignRight().Text($"Total Amount: {order.TotalAmount:N2}")
                                .Bold().FontSize(14);

                            if (!string.IsNullOrWhiteSpace(order.Remarks))
                            {
                                col.Item().PaddingTop(15).Text("Remarks:").Bold().FontSize(10);
                                col.Item().Text(order.Remarks).FontSize(10);
                            }

                            col.Item().PaddingTop(30).Row(row =>
                            {
                                row.RelativeItem().Column(sigCol =>
                                {
                                    sigCol.Item().Text("Prepared By:").Bold().FontSize(10);
                                    sigCol.Item().PaddingTop(25).Text(order.Staff?.StaffName ?? "");
                                    sigCol.Item().LineHorizontal(1);
                                });
                                row.ConstantItem(50);
                                row.RelativeItem().Column(sigCol =>
                                {
                                    sigCol.Item().Text("Approved By:").Bold().FontSize(10);
                                    sigCol.Item().PaddingTop(25).Text("________________________");
                                    sigCol.Item().LineHorizontal(1);
                                });
                            });
                        });
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                    });
                });
            }).GeneratePdf(ms);
            return ms.ToArray();
        }

        private static decimal CalculateNewAverageCost(decimal oldQty, decimal oldAvgCost, decimal newQty, decimal newUnitCost)
        {
            if (oldQty + newQty == 0) return newUnitCost;
            return ((oldQty * oldAvgCost) + (newQty * newUnitCost)) / (oldQty + newQty);
        }

        private static string CalculateStockStatus(int qty, int reorder)
        {
            if (qty <= 0) return "Out of Stock";
            if (qty <= reorder) return "Low Stock";
            return "Available";
        }

        private async Task<string> GeneratePONumberAsync()
        {
            string prefix = "PO-" + DateTime.Now.ToString("yyyyMMdd") + "-";
            string? lastNumber = await _context.PurchaseOrders
                .Where(p => p.PurchaseOrderNumber.StartsWith(prefix))
                .OrderByDescending(p => p.PurchaseOrderNumber)
                .Select(p => p.PurchaseOrderNumber)
                .FirstOrDefaultAsync();

            int nextSeq = 1;
            if (lastNumber != null)
            {
                string lastSeq = lastNumber[prefix.Length..];
                int.TryParse(lastSeq, out nextSeq);
                nextSeq++;
            }

            return prefix + nextSeq.ToString("D4");
        }
    }
}
