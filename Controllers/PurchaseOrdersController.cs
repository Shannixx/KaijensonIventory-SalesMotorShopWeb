using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.Services;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class PurchaseOrdersController : BaseController
    {
        private readonly IPurchaseOrderService _purchaseOrderService;
        private readonly ILogger<PurchaseOrdersController> _logger;

        public PurchaseOrdersController(IPurchaseOrderService purchaseOrderService, ILogger<PurchaseOrdersController> logger)
        {
            _purchaseOrderService = purchaseOrderService;
            _logger = logger;
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

            var result = await _purchaseOrderService.GetPagedAsync(searchString, statusFilter, page);

            ViewData["CurrentFilter"] = searchString;
            ViewData["StatusFilter"] = statusFilter;
            ViewData["Page"] = page;
            ViewData["TotalPages"] = result.TotalPages;
            ViewBag.CanDelete = IsAdmin();

            return View(result.Items);
        }

        public async Task<IActionResult> Create()
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            return View(await _purchaseOrderService.PrepareCreateViewModelAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PurchaseOrderViewModel viewModel)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            if (!ModelState.IsValid)
                return View(await _purchaseOrderService.PrepareCreateViewModelAsync(viewModel));

            var result = await _purchaseOrderService.CreateAsync(viewModel, GetCurrentStaffId());

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(error.Key ?? "", error.Message);
                return View(await _purchaseOrderService.PrepareCreateViewModelAsync(viewModel));
            }

            TempData["SuccessMessage"] = "Purchase order created successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            var viewModel = await _purchaseOrderService.GetDetailsViewModelAsync(id);
            if (viewModel == null) return NotFound();

            return View(viewModel);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            var viewModel = await _purchaseOrderService.PrepareEditViewModelAsync(id);
            if (viewModel == null) return NotFound();

            if (viewModel.Status == "Delivered" || viewModel.Status == "Cancelled")
            {
                TempData["ErrorMessage"] = "Cannot edit a purchase order that has been delivered or cancelled.";
                return RedirectToAction(nameof(Details), new { id });
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PurchaseOrderViewModel viewModel)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            if (id != viewModel.PurchaseOrderId) return NotFound();

            if (!ModelState.IsValid)
                return View(await _purchaseOrderService.PrepareEditViewModelAsync(viewModel));

            var result = await _purchaseOrderService.UpdateAsync(viewModel, GetCurrentStaffId());

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(error.Key ?? "", error.Message);
                return View(await _purchaseOrderService.PrepareEditViewModelAsync(viewModel));
            }

            TempData["SuccessMessage"] = "Purchase order updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            if (!IsAdmin())
                return Forbid();

            var result = await _purchaseOrderService.DeleteAsync(id, GetCurrentStaffId());

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault()?.Message
                    ?? "An error occurred while deleting the purchase order. Please try again.";
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] = "Purchase order deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            var result = await _purchaseOrderService.ApproveAsync(id, GetCurrentStaffId());

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault()?.Message
                    ?? "An error occurred while approving the purchase order. Please try again.";
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] = "Purchase order approved. Proceed to delivery.";
            return RedirectToAction("Details", "Delivery", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            var result = await _purchaseOrderService.CancelAsync(id, GetCurrentStaffId());

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault()?.Message
                    ?? "An error occurred while cancelling the purchase order. Please try again.";
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] = "Purchase order cancelled.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<JsonResult> GetSupplierInfo(int id)
        {
            var info = await _purchaseOrderService.GetSupplierInfoAsync(id);
            return Json(info ?? new SupplierInfoDto());
        }

        [HttpGet]
        public async Task<JsonResult> GetProductsBySupplier(int id)
        {
            var products = await _purchaseOrderService.GetProductsBySupplierAsync(id);
            return Json(products);
        }

        [HttpGet]
        public async Task<IActionResult> PrintPreviewHtml(int id)
        {
            var viewModel = await _purchaseOrderService.GetDetailsViewModelAsync(id);
            if (viewModel == null) return NotFound();

            return PartialView("_PrintPreview", viewModel);
        }

        public async Task<IActionResult> Print(int id)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            var order = await _purchaseOrderService.GetByIdAsync(id);
            if (order == null) return NotFound();

            try
            {
                byte[] pdfBytes = GeneratePurchaseOrderPdfBytes(order);
                await _purchaseOrderService.LogPrintAsync(id, GetCurrentStaffId());

                return File(pdfBytes, "application/pdf", $"PO-{order.PurchaseOrderNumber}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing purchase order {Id}", id);
                TempData["ErrorMessage"] = "An error occurred while generating the PDF. Please try again.";
                return RedirectToAction(nameof(Index));
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

                            col.Item().PaddingTop(10).EnsureSpace(30).AlignRight().Text($"Total Amount: {order.TotalAmount:N2}")
                                .Bold().FontSize(14);

                            if (!string.IsNullOrWhiteSpace(order.Remarks))
                            {
                                col.Item().PaddingTop(15).EnsureSpace(30).Text("Remarks:").Bold().FontSize(10);
                                col.Item().Text(order.Remarks).FontSize(10);
                            }

                            col.Item().PaddingTop(30).EnsureSpace(80).Row(row =>
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
    }
}
