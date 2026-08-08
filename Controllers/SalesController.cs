using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.Services;
using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using KaijensonIventory_SalesMotorShopWeb.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using System.Globalization;
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
        public IActionResult AddToCart(int productId, int quantity)
        {
            var accessRedirect = RedirectIfNotAuthenticated();
            if (accessRedirect != null) return accessRedirect;

            if (quantity <= 0)
            {
                TempData["ErrorMessage"] = "Quantity must be greater than zero.";
                return RedirectToAction(nameof(Search));
            }

            // Retrieve existing cart from session or create new
            var cart = HttpContext.Session.GetObject<CartViewModel>("Cart") ?? new CartViewModel();

            // Add or update item
            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                cart.Items.Add(new SaleItemViewModel { ProductId = productId, Quantity = quantity });
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
                }
                else
                {
                    item.ProductName = "[Removed]";
                    item.UnitPrice = 0m;
                    item.Subtotal = 0m;
                }
            }

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
                HttpContext.Session.SetObject("Cart", cart);
            }
            return RedirectToAction(nameof(Cart));
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

        // POST: /Sales/ProcessPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(PaymentViewModel model)
        {
            var accessRedirect = RedirectIfNotAuthenticated();
            if (accessRedirect != null) return accessRedirect;

            if (!ModelState.IsValid) return View("Confirm");

            var cart = HttpContext.Session.GetObject<CartViewModel>("Cart") ?? new CartViewModel();
            var staffId = GetCurrentStaffId();

            try
            {
                var transaction = await _saleService.ProcessSaleAsync(cart, model.AmountPaid, model.CheckoutKey, staffId);
                // Clear cart after successful sale
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

            var viewModel = new SaleDetailsViewModel { Transaction = transaction, Items = transaction.Items };
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

            var viewModel = new SaleDetailsViewModel { Transaction = transaction, Items = transaction.Items };
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
                                    left.Item().Text("Customer Purchase Order / Sales Receipt").FontSize(14);
                                });
                                // Right side – transaction details
                                row.ConstantItem(250).Column(right =>
                                {
                                    right.Item().AlignRight().Text($"Invoice #: {transaction.InvoiceNumber}").Bold();
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
                            col.Item().Table(table =>
                            {
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
