using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.Services;
using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using KaijensonIventory_SalesMotorShopWeb.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using System.Globalization;
using System.Collections.Generic;
using QuestPDF.Infrastructure;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class SalesController : BaseController
    {
        private readonly IProductService _productService;
        private readonly ISaleService _saleService;
        private readonly ILogger<SalesController> _logger;
        private readonly ApplicationDbContext _context;

        public SalesController(IProductService productService, ISaleService saleService, ApplicationDbContext context, ILogger<SalesController> logger)
        {
            _productService = productService;
            _saleService = saleService;
            _context = context;
            _logger = logger;
        }

        // GET: /Sales/Search
        public async Task<IActionResult> Search(string? query, bool reorder = false, int? productId = null)
        {
            var accessRedirect = RedirectIfNotAuthenticated();
            if (accessRedirect != null) return accessRedirect;

var results = await _productService.GetPagedAsync(query, null, 1);

// Reorder mode handling
if (reorder && productId.HasValue)
{
    var product = await _productService.GetByIdAsync(productId.Value);
    if (product == null)
    {
        TempData["ErrorMessage"] = "Product not found for reorder.";
        return RedirectToAction(nameof(Index), "Products");
    }
    if (product.QuantityOnHand > product.ReorderLevel)
    {
        TempData["ErrorMessage"] = "Product stock is still above the reorder level.";
        return RedirectToAction(nameof(Index), "Products");
    }
if (product.SupplierId <= 0)
            {
                TempData["ErrorMessage"] = "Please assign a supplier to this product before creating a purchase order.";
                return RedirectToAction(nameof(Index), "Products");
            }
            // Ensure supplier is active
            if (product.Supplier?.Status != "Active")
            {
                TempData["ErrorMessage"] = "This product cannot be reordered because its supplier is inactive.";
                return RedirectToAction(nameof(Index), "Products");
            }
    ViewBag.IsReorder = true;
    ViewBag.ReorderProduct = product;

    // In reorder mode, do not load or persist cart
    var emptyCart = new CartViewModel();
    var reorderViewModel = new CustomerPurchaseOrderViewModel
    {
        Products = results,
        Cart = emptyCart,
        SearchQuery = query
    };
    return View(reorderViewModel);
}

// Normal mode: load cart and refresh data
var cart = HttpContext.Session.GetObject<CartViewModel>("Cart") ?? new CartViewModel();
foreach (var item in cart.Items)
{
    var product = await _productService.GetByIdAsync(item.ProductId);
    if (product != null)
    {
        item.ProductName = product.ProductName;
        item.UnitPrice = product.Price;
        item.Subtotal = product.Price * item.Quantity;
        item.IsSerialized = product.IsSerialized;
    }
    else
    {
        item.ProductName = "[Removed]";
        item.UnitPrice = 0m;
        item.Subtotal = 0m;
    }
}
HttpContext.Session.SetObject("Cart", cart);

            var viewModel = new CustomerPurchaseOrderViewModel
            {
                Products = results,
                Cart = cart,
                SearchQuery = query
            };
            return View(viewModel);
        }

        // POST: /Sales/AddToCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int productId, int quantity, string? query = null)
        {
            var accessRedirect = RedirectIfNotAuthenticated();
            if (accessRedirect != null) return accessRedirect;

            // Basic quantity validation
            if (quantity < 1)
            {
                TempData["ErrorMessage"] = "Quantity must be greater than zero.";
                return RedirectToAction(nameof(Search), new { query });
            }

            // Load product to validate existence and stock
            var product = await _productService.GetByIdAsync(productId);
            if (product == null)
            {
                TempData["ErrorMessage"] = "Product not found.";
                return RedirectToAction(nameof(Search), new { query });
            }

            if (quantity > product.QuantityOnHand)
            {
                TempData["ErrorMessage"] = "Requested quantity exceeds available stock.";
                return RedirectToAction(nameof(Search), new { query });
            }

            // Retrieve or create cart from session
            var cart = HttpContext.Session.GetObject<CartViewModel>("Cart") ?? new CartViewModel();

            // Add or update item using SET behavior
            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (existingItem != null)
            {
                existingItem.Quantity = quantity; // set total desired quantity
                existingItem.IsSerialized = product.IsSerialized;
            }
            else
            {
                cart.Items.Add(new SaleItemViewModel { ProductId = productId, Quantity = quantity, IsSerialized = product.IsSerialized });
            }

            HttpContext.Session.SetObject("Cart", cart);
            return RedirectToAction(nameof(Search), new { query });
        }

        // GET: /Sales/Cart
        public async Task<IActionResult> Cart()
        {
            var accessRedirect = RedirectIfNotAuthenticated();
            if (accessRedirect != null) return accessRedirect;

            var cart = HttpContext.Session.GetObject<CartViewModel>("Cart") ?? new CartViewModel();

            // Populate display fields by reloading product data
            foreach (var item in cart.Items)
            {
                var product = await _productService.GetByIdAsync(item.ProductId);
                if (product != null)
                {
                    item.ProductName = product.ProductName;
                    item.UnitPrice = product.Price;
                    item.Subtotal = product.Price * item.Quantity;
                    item.IsSerialized = product.IsSerialized;
                }
                else
                {
                    item.ProductName = "[Removed]";
                    item.UnitPrice = 0m;
                    item.Subtotal = 0m;
                }
            }

            // Persist any updates back to session
            HttpContext.Session.SetObject("Cart", cart);

            return View(cart);
        }

        // GET: /Sales/Confirm
        public async Task<IActionResult> Confirm()
        {
            var accessRedirect = RedirectIfNotAuthenticated();
            if (accessRedirect != null) return accessRedirect;

            var cart = HttpContext.Session.GetObject<CartViewModel>("Cart") ?? new CartViewModel();
            // Populate display fields by reloading product data
            foreach (var item in cart.Items)
            {
                var product = await _productService.GetByIdAsync(item.ProductId);
                if (product != null)
                {
                    item.ProductName = product.ProductName;
                    item.UnitPrice = product.Price;
                    item.Subtotal = product.Price * item.Quantity;
                    item.IsSerialized = product.IsSerialized;
                }
                else
                {
                    item.ProductName = "[Removed]";
                    item.UnitPrice = 0m;
                    item.Subtotal = 0m;
                }
            }
            // Generate a checkout key and store it in TempData for the payment view
            var checkoutKey = Guid.NewGuid().ToString("N");
            TempData["CheckoutKey"] = checkoutKey;
            ViewBag.CheckoutKey = checkoutKey;
            return View(cart);
        }

        // POST: /Sales/ProcessPayment
        // New actions for cart quantity management
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DecreaseQuantity(int productId, string? query = null)
        {
            var accessRedirect = RedirectIfNotAuthenticated();
            if (accessRedirect != null) return accessRedirect;

            var cart = HttpContext.Session.GetObject<CartViewModel>("Cart") ?? new CartViewModel();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                if (item.Quantity > 1)
                {
                    item.Quantity -= 1;
                }
                else
                {
                    cart.Items.Remove(item);
                }
                // Clear any stored serial numbers for this product since quantity changed
                cart.SerialNumbers.Remove(productId);
                HttpContext.Session.SetObject("Cart", cart);
            }
            return RedirectToAction(nameof(Search), new { query });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IncreaseQuantity(int productId, string? query = null)
        {
            var accessRedirect = RedirectIfNotAuthenticated();
            if (accessRedirect != null) return accessRedirect;

            var cart = HttpContext.Session.GetObject<CartViewModel>("Cart") ?? new CartViewModel();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                var product = await _productService.GetByIdAsync(productId);
                if (product != null && item.Quantity < product.QuantityOnHand)
                {
                    item.Quantity += 1;
                }
                else
                {
                    TempData["ErrorMessage"] = "Cannot increase quantity beyond available stock.";
                }
                // Clear any stored serial numbers for this product since quantity changed
                cart.SerialNumbers.Remove(productId);
                HttpContext.Session.SetObject("Cart", cart);
            }
            return RedirectToAction(nameof(Search), new { query });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSerialNumbers([FromForm] Dictionary<int, List<string>> SerialNumbers, string? query = null)
        {
            var accessRedirect = RedirectIfNotAuthenticated();
            if (accessRedirect != null) return accessRedirect;

            var cart = HttpContext.Session.GetObject<CartViewModel>("Cart") ?? new CartViewModel();

            if (SerialNumbers == null)
            {
                TempData["ErrorMessage"] = "Serial numbers were not provided.";
                return RedirectToAction(nameof(Search), new { query });
            }

            var cleaned = new Dictionary<int, List<string>>();
            var errors = new List<string>();

foreach (var kvp in SerialNumbers)
                {
                    var productId = kvp.Key;
                    var serialList = kvp.Value.Select(s => s?.Trim() ?? string.Empty).ToList();

                    var cartItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);
                    if (cartItem == null)
                    {
                        errors.Add($"Product {productId} is not in the cart.");
                        continue;
                    }

                    // Verify product serialization from database
                    var product = await _productService.GetByIdAsync(productId);
                    bool isSerialized = product?.IsSerialized ?? false;
                    // Update cart item flag to stay in sync
                    cartItem.IsSerialized = isSerialized;

                    if (!isSerialized)
                    {
                        // Ensure any stale serial numbers are cleared
                        cart.SerialNumbers.Remove(productId);
                        // Non-serialized product: no serial numbers required
                        continue;
                    }

                    if (serialList.Count != cartItem.Quantity)
                    {
                        errors.Add($"Serial count ({serialList.Count}) does not match quantity ({cartItem.Quantity}) for product {cartItem.ProductName}.");
                    }

                    if (serialList.Any(s => string.IsNullOrWhiteSpace(s)))
                    {
                        errors.Add($"Serial numbers cannot be empty for product {cartItem.ProductName}.");
                    }

                    if (serialList.Distinct().Count() != serialList.Count)
                    {
                        errors.Add($"Duplicate serial numbers provided for product {cartItem.ProductName}.");
                    }

                    cleaned[productId] = serialList;
                }

            if (errors.Any())
            {
                TempData["ErrorMessage"] = string.Join(" ", errors);
                // Do not overwrite existing valid serial data
                return RedirectToAction(nameof(Search), new { query });
            }

            cart.SerialNumbers = cleaned;
            HttpContext.Session.SetObject("Cart", cart);
            return RedirectToAction(nameof(Confirm));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(PaymentViewModel model, string? query = null)
        {
            var accessRedirect = RedirectIfNotAuthenticated();
            if (accessRedirect != null) return accessRedirect;

            if (!ModelState.IsValid) return View("Confirm");

            var cart = HttpContext.Session.GetObject<CartViewModel>("Cart") ?? new CartViewModel();
            var staffId = GetCurrentStaffId();

            // Validate serial numbers for serialized items before processing payment
            var serialErrors = new List<string>();
            foreach (var item in cart.Items)
            {
                // Load product to determine serialization
                var product = await _productService.GetByIdAsync(item.ProductId);
                bool isSerialized = product?.IsSerialized ?? false;
                // Sync cart flag
                item.IsSerialized = isSerialized;

                if (!isSerialized)
                {
                    // No serial numbers required for this product
                    continue;
                }

                if (!cart.SerialNumbers.TryGetValue(item.ProductId, out var serials))
                {
                    serialErrors.Add($"Serial numbers are required for {item.ProductName}.");
                    continue;
                }
                if (serials.Count != item.Quantity)
                    serialErrors.Add($"Serial count ({serials.Count}) does not match quantity ({item.Quantity}) for {item.ProductName}.");
                if (serials.Any(s => string.IsNullOrWhiteSpace(s)))
                    serialErrors.Add($"Serial numbers cannot be empty for {item.ProductName}.");
                if (serials.Distinct().Count() != serials.Count)
                    serialErrors.Add($"Duplicate serial numbers for {item.ProductName}.");
            }
            if (serialErrors.Any())
            {
                TempData["ErrorMessage"] = string.Join(" ", serialErrors);
                return RedirectToAction(nameof(Search), new { query });
            }

            try
            {
                var transaction = await _saleService.ProcessSaleAsync(cart, model.AmountPaid, model.CheckoutKey, staffId);
                HttpContext.Session.Remove("Cart");
                return RedirectToAction(nameof(Details), new { id = transaction.TransactionId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing sale.");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Search), new { query });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveItem(int productId, string? query = null)
        {
            var accessRedirect = RedirectIfNotAuthenticated();
            if (accessRedirect != null) return accessRedirect;

            var cart = HttpContext.Session.GetObject<CartViewModel>("Cart") ?? new CartViewModel();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                cart.Items.Remove(item);
                HttpContext.Session.SetObject("Cart", cart);
            }
            return RedirectToAction(nameof(Search), new { query });
        }


        // GET: /Sales/Details/{id}
        public async Task<IActionResult> Details(int id)
        {
            var accessRedirect = RedirectIfNotAuthenticated();
            if (accessRedirect != null) return accessRedirect;

            var transaction = await _context.SalesTransactions
                .Include(t => t.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(t => t.TransactionId == id);

            if (transaction == null) return NotFound();

            // Optional service-job reference connected to this sale (single FK on ServiceJob).
            var serviceJob = await _context.ServiceJobs
                .Include(j => j.Service)
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.SalesTransactionId == id);

            // Load serial numbers for this transaction, grouped by product
            var serials = await _context.SerialUnits
                .Where(s => s.SalesTransactionId == id)
                .GroupBy(s => s.ProductId)
                .ToDictionaryAsync(g => g.Key, g => g.Select(s => s.SerialNumber).ToList());

            var viewModel = new SaleDetailsViewModel {
                Transaction = transaction,
                Items = transaction.Items,
                SerialNumbersByProduct = serials,
                ServiceJob = serviceJob
            };
            return View(viewModel);
        }

        // GET: /Sales/ReceiptPdf/{id}
        public async Task<IActionResult> ReceiptPdf(int id)
        {
            var accessRedirect = RedirectIfNotAuthenticated();
            if (accessRedirect != null) return accessRedirect;

            var transaction = await _context.SalesTransactions
                .Include(t => t.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(t => t.TransactionId == id);
            if (transaction == null) return NotFound();

            try
            {
                var pdfBytes = GeneratePdfBytes(transaction);
                return File(pdfBytes, "application/pdf", $"Receipt-{transaction.InvoiceNumber}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Receipt generation failed: {ExceptionDetails}", ex.ToString());
                TempData["ErrorMessage"] = "Receipt could not be generated. You can view the details page and retry.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // GET: /Sales/PrintPreviewHtml/{id}
        // Returns HTML fragment for receipt preview (used by modal)
        public async Task<IActionResult> PrintPreviewHtml(int id)
        {
            var accessRedirect = RedirectIfNotAuthenticated();
            if (accessRedirect != null) return accessRedirect;

            var transaction = await _context.SalesTransactions
                .Include(t => t.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(t => t.TransactionId == id);
            if (transaction == null) return NotFound();

            var serials = await _context.SerialUnits
    .Where(s => s.SalesTransactionId == id)
    .GroupBy(s => s.ProductId)
    .ToDictionaryAsync(g => g.Key, g => g.Select(s => s.SerialNumber).ToList());
var viewModel = new SaleDetailsViewModel { Transaction = transaction, Items = transaction.Items, SerialNumbersByProduct = serials };
            return PartialView("_ReceiptPreview", viewModel);
        }

        // Helper to generate PDF – compact 80mm thermal-style sales receipt
        private byte[] GeneratePdfBytes(SalesTransaction transaction)
        {
            var ph = System.Globalization.CultureInfo.GetCultureInfo("en-PH");
            decimal subtotal = transaction.Items != null && transaction.Items.Any()
                ? transaction.Items.Sum(i => i.Subtotal)
                : transaction.TotalAmount;

            var serials = _context.SerialUnits
                .Where(s => s.SalesTransactionId == transaction.TransactionId)
                .GroupBy(s => s.ProductId)
                .ToDictionary(g => g.Key, g => g.Select(s => s.SerialNumber).ToList());

            var cust = string.IsNullOrWhiteSpace(transaction.CustomerName) ? "Walk‑in" : transaction.CustomerName;

            var doc = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    // Narrow receipt paper (80mm) with dynamic height, like a thermal/POS receipt
                    page.ContinuousSize(227f);   // ≈ 80mm in points
                    page.Margin(11f);            // ≈ 4mm compact receipt margins
                    page.DefaultTextStyle(x => x.FontSize(8).FontColor("#111111"));

                    page.Content().Element(c =>
                    {
                        c.Column(col =>
                        {
                            col.Spacing(2);

                            // ── Header: centered business identity ──
                            col.Item().AlignCenter().Text("KAIJENSON MOTOR SHOP").FontSize(12.5f).Bold();
                            col.Item().AlignCenter().Text("Sales Receipt").FontSize(9);
                            col.Item().PaddingVertical(3).LineHorizontal(1).LineColor("#111111");

                            // ── Transaction details ──
                            col.Item().Column(meta =>
                            {
                                meta.Spacing(1);

                                void MetaRow(string label, string value)
                                {
                                    meta.Item().Row(r =>
                                    {
                                        r.ConstantItem(52).Text(label);
                                        r.RelativeItem().AlignRight().Text(value);
                                    });
                                }

                                MetaRow("Receipt No.", transaction.InvoiceNumber);
                                MetaRow("Date", transaction.TransactionDate.ToString("MMM dd, yyyy"));
                                MetaRow("Staff", transaction.Staff?.StaffName ?? "");
                                MetaRow("Customer", cust);
                            });

                            col.Item().PaddingVertical(3).LineHorizontal(1).LineColor("#999999");

                            // ── Items ──
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(5);   // product
                                    columns.ConstantColumn(20);  // qty
                                    columns.ConstantColumn(46);  // unit price
                                    columns.ConstantColumn(50);  // amount
                                });

                                table.Header(header =>
                                {
                                    header.Cell().BorderBottom(1).BorderColor("#111111").Text("Product").FontSize(7).Bold();
                                    header.Cell().BorderBottom(1).BorderColor("#111111").AlignCenter().Text("Qty").FontSize(7).Bold();
                                    header.Cell().BorderBottom(1).BorderColor("#111111").AlignRight().Text("Price").FontSize(7).Bold();
                                    header.Cell().BorderBottom(1).BorderColor("#111111").AlignRight().Text("Amount").FontSize(7).Bold();
                                });

                                if (transaction.Items != null && transaction.Items.Any())
                                {
                                    foreach (var item in transaction.Items)
                                    {
                                        table.Cell().PaddingVertical(1.5f).Text(item.Product?.ProductName ?? "");
                                        table.Cell().PaddingVertical(1.5f).AlignCenter().Text(item.Quantity.ToString());
                                        table.Cell().PaddingVertical(1.5f).AlignRight().Text(item.UnitPrice.ToString("C", ph));
                                        table.Cell().PaddingVertical(1.5f).AlignRight().Text(item.Subtotal.ToString("C", ph));

                                        // Serial numbers directly beneath their product line
                                        if (serials.TryGetValue(item.ProductId, out var sList) && sList.Any())
                                        {
                                            table.Cell().ColumnSpan(4).PaddingVertical(1).Text($"Serial: {string.Join(", ", sList)}").FontSize(6.5f).FontColor("#444444");
                                        }
                                    }
                                }
                                else
                                {
                                    table.Cell().ColumnSpan(4).PaddingVertical(4).AlignCenter().Text("No items to display").FontSize(8);
                                }
                            });

                            col.Item().PaddingVertical(2).LineHorizontal(1).LineColor("#111111");

                            // ── Totals ──
                            col.Item().Column(tot =>
                            {
                                tot.Spacing(1);

                                void TotalRow(string label, string value)
                                {
                                    tot.Item().Row(r =>
                                    {
                                        r.RelativeItem().Text(label).FontSize(8);
                                        r.ConstantItem(60).AlignRight().Text(value).FontSize(8);
                                    });
                                }

                                TotalRow("Subtotal", subtotal.ToString("C", ph));
                                tot.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("TOTAL").FontSize(10).Bold();
                                    r.ConstantItem(60).AlignRight().Text(transaction.TotalAmount.ToString("C", ph)).FontSize(10).Bold().FontColor("#E8650A");
                                });
                                TotalRow("Cash / Paid", transaction.AmountPaid.ToString("C", ph));
                                TotalRow("Change", transaction.Change.ToString("C", ph));
                            });

                            col.Item().PaddingVertical(4).LineHorizontal(1).LineColor("#111111");

                            // ── Closing message ──
                            col.Item().PaddingTop(6).AlignCenter().Text("Thank you for your purchase.").FontSize(8);
                        });
                    });
                });
            });

            // Generate PDF bytes
            using var ms = new System.IO.MemoryStream();
            doc.GeneratePdf(ms);
            return ms.ToArray();
        }
    }
}
