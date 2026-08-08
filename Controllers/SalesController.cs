using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.Services;
using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using KaijensonIventory_SalesMotorShopWeb.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
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
        public IActionResult Confirm()
        {
            var accessRedirect = RedirectIfNotAuthenticated();
            if (accessRedirect != null) return accessRedirect;

            var cart = HttpContext.Session.GetObject<CartViewModel>("Cart") ?? new CartViewModel();
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
                // Use existing QuestPDF logic (similar to PurchaseOrders) – render from view
                var pdfBytes = GeneratePdfBytes(transaction);
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Receipt generation failed.");
                TempData["ErrorMessage"] = "Receipt could not be generated. You can view the details page and retry.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // Helper to generate PDF (simplified – uses QuestPDF like other controllers)
        private byte[] GeneratePdfBytes(SalesTransaction transaction)
        {
            // Very basic PDF generation – for demo purposes
            var doc = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.Header().Text($"Receipt - {transaction.InvoiceNumber}").Bold().FontSize(20);
                    page.Content().Column(col =>
                    {
                        col.Item().Text($"Date: {transaction.TransactionDate}");
                        col.Item().Text($"Staff: {transaction.Staff?.StaffName}");
                        col.Item().Text($"Customer: {transaction.CustomerName}");
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(4);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                            });
                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("Product");
                                header.Cell().Element(CellStyle).AlignRight().Text("Qty");
                                header.Cell().Element(CellStyle).AlignRight().Text("Unit Price");
                                header.Cell().Element(CellStyle).AlignRight().Text("Subtotal");
                            });
                            foreach (var item in transaction.Items)
                            {
                                table.Cell().Element(CellStyle).Text(item.Product.ProductName);
                                table.Cell().Element(CellStyle).AlignRight().Text(item.Quantity.ToString());
                                table.Cell().Element(CellStyle).AlignRight().Text(item.UnitPrice.ToString("C"));
                                table.Cell().Element(CellStyle).AlignRight().Text(item.Subtotal.ToString("C"));
                            }
                            table.Cell().ColumnSpan(3).AlignRight().Text("Total:");
                            table.Cell().AlignRight().Text(transaction.TotalAmount.ToString("C"));
                        });
                        col.Item().Text($"Amount Paid: {transaction.AmountPaid:C}");
                        col.Item().Text($"Change: {transaction.Change:C}");
                    });
                });
            });

            static IContainer CellStyle(IContainer container) => container.BorderBottom(1).PaddingVertical(5).PaddingHorizontal(2);
            return doc.GeneratePdf();
        }
    }
}
