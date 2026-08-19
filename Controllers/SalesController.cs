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
        public async Task<IActionResult> Search(string? query)
        {
            var accessRedirect = RedirectIfNotAuthenticated();
            if (accessRedirect != null) return accessRedirect;

            var results = await _productService.GetPagedAsync(query, null, 1);
            return View(results);
        }

        // POST: /Sales/AddToCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int productId, int quantity)
        {
            var accessRedirect = RedirectIfNotAuthenticated();
            if (accessRedirect != null) return accessRedirect;

            // Basic quantity validation
            if (quantity < 1)
            {
                TempData["ErrorMessage"] = "Quantity must be greater than zero.";
                return RedirectToAction(nameof(Search));
            }

            // Load product to validate existence and stock
            var product = await _productService.GetByIdAsync(productId);
            if (product == null)
            {
                TempData["ErrorMessage"] = "Product not found.";
                return RedirectToAction(nameof(Search));
            }

            if (quantity > product.QuantityOnHand)
            {
                TempData["ErrorMessage"] = "Requested quantity exceeds available stock.";
                return RedirectToAction(nameof(Search));
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
            return RedirectToAction(nameof(Cart));
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
        public IActionResult DecreaseQuantity(int productId)
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
            return RedirectToAction(nameof(Cart));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IncreaseQuantity(int productId)
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
            return RedirectToAction(nameof(Cart));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSerialNumbers([FromForm] Dictionary<int, List<string>> SerialNumbers)
        {
            var accessRedirect = RedirectIfNotAuthenticated();
            if (accessRedirect != null) return accessRedirect;

            var cart = HttpContext.Session.GetObject<CartViewModel>("Cart") ?? new CartViewModel();

            if (SerialNumbers == null)
            {
                TempData["ErrorMessage"] = "Serial numbers were not provided.";
                return RedirectToAction(nameof(Cart));
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
                return RedirectToAction(nameof(Cart));
            }

            cart.SerialNumbers = cleaned;
            HttpContext.Session.SetObject("Cart", cart);
            return RedirectToAction(nameof(Confirm));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(PaymentViewModel model)
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
                return RedirectToAction(nameof(Cart));
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
                return RedirectToAction(nameof(Cart));
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveItem(int productId)
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
            return RedirectToAction(nameof(Cart));
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

            // Load serial numbers for this transaction, grouped by product
            var serials = await _context.SerialUnits
                .Where(s => s.SalesTransactionId == id)
                .GroupBy(s => s.ProductId)
                .ToDictionaryAsync(g => g.Key, g => g.Select(s => s.SerialNumber).ToList());

            var viewModel = new SaleDetailsViewModel {
                Transaction = transaction,
                Items = transaction.Items,
                SerialNumbersByProduct = serials
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

        // Helper to generate PDF (simplified – uses QuestPDF like other controllers)
        private byte[] GeneratePdfBytes(SalesTransaction transaction)
        {
            // Generate a professional printable receipt similar to Purchase Order PDF
            var doc = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(QuestPDF.Helpers.PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    // Header – shop name and receipt title with transaction info on right
                    page.Header().Element(c =>
                    {
                        c.Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                // Left side – shop name and document title
                                row.RelativeItem().Column(left =>
                                {
                                    left.Item().Text("KAIJENSON MOTOR SHOP").FontSize(18).Bold();
                                    left.Item().Text("Sales Receipt").FontSize(14);
                                });
                                // Right side – transaction details
                                row.ConstantItem(250).Column(right =>
                                {
                                    right.Item().AlignRight().Text($"Receipt #: {transaction.InvoiceNumber}").Bold();
                                    right.Item().AlignRight().Text($"Date: {transaction.TransactionDate:MMM dd, yyyy}");
                                    right.Item().AlignRight().Text($"Staff: {transaction.Staff?.StaffName ?? ""}");
                                    var cust = string.IsNullOrWhiteSpace(transaction.CustomerName) ? "Walk‑in" : transaction.CustomerName;
                                    right.Item().AlignRight().Text($"Customer: {cust}");
                                });
                            });
                            col.Item().PaddingVertical(10).LineHorizontal(1);
                        });
                    });

                    // Content – product table and financial summary
                    page.Content().Element(c =>
                    {
                        c.Column(col =>
                        {
                            // Product table
col.Item().Table(table => { var serials = _context.SerialUnits.Where(s => s.SalesTransactionId == transaction.TransactionId).GroupBy(s => s.ProductId).ToDictionary(g => g.Key, g => g.Select(s => s.SerialNumber).ToList());
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(25); // index
                                    columns.RelativeColumn(4); // product
                                    columns.RelativeColumn(2); // qty
                                    columns.RelativeColumn(2); // unit price
                                    columns.RelativeColumn(2); // subtotal
                                });

                                // Header row styling similar to PO
                                table.Header(header =>
                                {
                                    header.Cell().Background("#FF7F11").Padding(5).Text("#").FontColor("#fff").FontSize(10).Bold();
                                    header.Cell().Background("#FF7F11").Padding(5).Text("Product").FontColor("#fff").FontSize(10).Bold();
                                    header.Cell().Background("#FF7F11").Padding(5).AlignRight().Text("Qty").FontColor("#fff").FontSize(10).Bold();
                                    header.Cell().Background("#FF7F11").Padding(5).AlignRight().Text("Unit Price").FontColor("#fff").FontSize(10).Bold();
                                    header.Cell().Background("#FF7F11").Padding(5).AlignRight().Text("Subtotal").FontColor("#fff").FontSize(10).Bold();
                                });

                                int idx = 1;
if (transaction.Items != null && transaction.Items.Any())
                                          {
foreach (var item in transaction.Items)
                                                {
                                                    table.Cell().Padding(5).Text(idx.ToString()).FontSize(10);
                                                    table.Cell().Padding(5).Text(item.Product?.ProductName ?? "").FontSize(10);
                                                    table.Cell().Padding(5).AlignRight().Text(item.Quantity.ToString()).FontSize(10);
                                                    table.Cell().Padding(5).AlignRight().Text(item.UnitPrice.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-PH"))).FontSize(10);
                                                    table.Cell().Padding(5).AlignRight().Text(item.Subtotal.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-PH"))).FontSize(10);
                                                    idx++;
                                                    // Serial numbers row if applicable
                                                    if (serials != null && serials.TryGetValue(item.ProductId, out var sList) && sList.Any())
                                                    {
                                                        table.Cell().ColumnSpan(5).Padding(5).Text($"Serial: {string.Join(", ", sList)}").FontSize(8);
                                                    }
                                                }
                                          }
                                          else
                                          {
                                              // No items – add a placeholder row
                                              table.Cell().ColumnSpan(5).AlignCenter().Text("No items to display").FontSize(10);
                                          }

                                // Footer totals
                                table.Cell().ColumnSpan(4).AlignRight().Text("Total:").Bold().FontSize(12);
                                table.Cell().AlignRight().Text(transaction.TotalAmount.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-PH"))).Bold().FontSize(12);
                            });

                            // Amount paid and change
                            col.Item().PaddingTop(10).AlignRight().Text($"Amount Paid: {transaction.AmountPaid.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-PH"))}").Bold().FontSize(12);
                            col.Item().AlignRight().Text($"Change: {transaction.Change.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-PH"))}").Bold().FontSize(12);

                            // Footer thank‑you message
                            col.Item().PaddingTop(30).AlignCenter().Text("Thank you for your purchase.").FontSize(10);
                        });
                    });

                    // Footer – page number
                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
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
