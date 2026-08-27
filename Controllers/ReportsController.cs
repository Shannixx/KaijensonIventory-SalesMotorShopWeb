using System;
using System.Threading.Tasks;
using KaijensonIventory_SalesMotorShopWeb.Services;
using KaijensonIventory_SalesMotorShopWeb.Data;
using Microsoft.EntityFrameworkCore;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class ReportsController : BaseController
    {
        private readonly IReportService _reportService;
        private readonly ApplicationDbContext _context;
        public ReportsController(IReportService reportService, ApplicationDbContext context)
        {
            _reportService = reportService;
            _context = context;
        }

        // GET: /Reports
        public async Task<IActionResult> Index(ReportFilterViewModel filter, string? report)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;
            if (!IsOwnerOrManager())
            {
                TempData["ErrorMessage"] = "Access denied. Admin or Manager privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }
            // Preserve the active report across Generate ("Sales" / "Inventory"; null = landing page)
            ViewBag.ActiveReport = report == "Sales" || report == "Inventory" ? report : null;
            // Ensure defaults
            if (filter.StartDate == default) filter.StartDate = DateTime.Today.AddMonths(-1);
            if (filter.EndDate == default) filter.EndDate = DateTime.Today;
            // Validate dates
            if (filter.StartDate > filter.EndDate)
            {
                ModelState.AddModelError(string.Empty, "Start Date must be before End Date.");
                ViewBag.Products = await _context.Products.OrderBy(p => p.ProductName).ToListAsync();
                ViewBag.Categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();
                var errorVm = new ReportsPageViewModel { Filter = filter };
                return View(errorVm);
            }
            // Populate filter selections
            ViewBag.Products = await _context.Products.OrderBy(p => p.ProductName).ToListAsync();
            ViewBag.Categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();
            var viewModel = await BuildReportViewModelAsync(filter);
            return View(viewModel);
        }

        // GET: /Reports/ExportCsv
        [HttpGet]
        public async Task<IActionResult> ExportCsv([FromQuery] ReportFilterViewModel filter)
        {
            // Reuse same filter handling as Index
            var viewModel = await BuildReportViewModelAsync(filter);
            // Build CSV content
            var sb = new System.Text.StringBuilder();
            // Helper to escape CSV fields
            string Escape(string s)
            {
                if (s == null) return "";
                bool mustQuote = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
                if (mustQuote)
                {
                    s = s.Replace("\"", "\"\"");
                    return $"\"{s}\"";
                }
                return s;
            }
            // Summary KPIs
            sb.AppendLine("Summary KPIs");
            sb.AppendLine(string.Join(",", new[] { "Total Revenue", "Total Qty Sold", "Transactions", "Inventory Value", "Low Stock Items" }));
            sb.AppendLine(string.Join(",", new[] {
                viewModel.RevenueReport.TotalRevenue.ToString("F2"),
                viewModel.SalesPerformanceReport.TotalQuantitySold.ToString(),
                viewModel.SalesPerformanceReport.TransactionCount.ToString(),
                viewModel.TotalInventoryValue.ToString("F2"),
                viewModel.LowStockItemCount.ToString()
            }));
            sb.AppendLine();
            // Inventory
            sb.AppendLine("Inventory");
            sb.AppendLine(string.Join(",", new[] { "Product", "Category", "Qty On Hand", "Status" }));
            foreach (var i in viewModel.InventoryReport.Items)
            {
                sb.AppendLine(string.Join(",", new[] { Escape(i.ProductName), Escape(i.CategoryName), i.QuantityOnHand.ToString(), Escape(i.StockStatus) }));
            }
            sb.AppendLine();
            // Most Sold Products
            sb.AppendLine("Most Sold Products");
            sb.AppendLine(string.Join(",", new[] { "Product", "Qty Sold", "Unit Price", "Revenue" }));
            foreach (var p in viewModel.MostSoldProducts)
            {
                sb.AppendLine(string.Join(",", new[] { Escape(p.ProductName), p.QuantitySold.ToString(), p.UnitPrice.ToString("F2"), p.Revenue.ToString("F2") }));
            }
            sb.AppendLine();
            // Revenue Trend (only dates and revenue)
            sb.AppendLine("Revenue Trend");
            sb.AppendLine(string.Join(",", new[] { "Period", "Revenue" }));
            foreach (var r in viewModel.RevenueTrend)
            {
                sb.AppendLine(string.Join(",", new[] { r.Period.ToString("yyyy-MM-dd"), r.Revenue.ToString("F2") }));
            }
            sb.AppendLine();
            // Sales By Category
            sb.AppendLine("Sales By Category");
            sb.AppendLine(string.Join(",", new[] { "Category", "Revenue", "Units Sold" }));
            foreach (var c in viewModel.SalesByCategory)
            {
                sb.AppendLine(string.Join(",", new[] { Escape(c.CategoryName), c.Revenue.ToString("F2"), c.UnitsSold.ToString() }));
            }
            sb.AppendLine();
            // Low Stock Alerts
                        sb.AppendLine("Low Stock Alerts");
                        sb.AppendLine(string.Join(",", new[] { "Product", "Qty On Hand", "Reorder Level", "Status" }));
                        foreach (var a in viewModel.LowStockAlerts)
                        {
                            sb.AppendLine(string.Join(",", new[] { Escape(a.ProductName), a.QuantityOnHand.ToString(), a.ReorderLevel.ToString(), Escape(a.StockStatus) }));
                        }
                        sb.AppendLine();

                        // Stock Movements
            sb.AppendLine("Stock Movements");
            sb.AppendLine(string.Join(",", new[] { "Date", "Product", "Type", "Qty", "Reference" }));
            foreach (var m in viewModel.StockMovements)
            {
                sb.AppendLine(string.Join(",", new[] { m.Date.ToString("yyyy-MM-dd"), Escape(m.ProductName), Escape(m.MovementType), m.Quantity.ToString(), Escape(m.Reference) }));
            }
            sb.AppendLine();
            // Serial Numbers
            sb.AppendLine("Serial Numbers");
            sb.AppendLine(string.Join(",", new[] { "Serial", "Product", "Status", "Sale Id", "Sale Date" }));
            foreach (var s in viewModel.SerialNumberReport)
            {
                sb.AppendLine(string.Join(",", new[] { Escape(s.SerialNumber), Escape(s.ProductName), Escape(s.Status), s.SaleId?.ToString() ?? "-", s.SaleDate?.ToString("yyyy-MM-dd") ?? "-" }));
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv", fileName);
        }

        // GET: /Reports/ExportSalesPdf
        [HttpGet]
        public async Task<IActionResult> ExportSalesPdf([FromQuery] ReportFilterViewModel filter)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;
            if (!IsOwnerOrManager())
            {
                TempData["ErrorMessage"] = "Access denied. Admin or Manager privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }
            try
            {
                // Same filtering rules as the browser Sales Report
                var viewModel = await BuildReportViewModelAsync(filter);
                var ph = System.Globalization.CultureInfo.GetCultureInfo("en-PH");
                var currentUser = GetCurrentStaffName();
                var generatedOn = DateTime.Now.ToString("MMMM dd, yyyy h:mm tt");

                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(40);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        // Header
                        page.Header().Column(col =>
                        {
                            col.Item().AlignCenter().Text("KAIJENSON MOTOR SHOP").Bold().FontSize(16);
                            col.Item().AlignCenter().Text("SALES REPORT").Bold().FontSize(12);
                            col.Item().PaddingTop(4).AlignCenter().Text($"Date Range: {viewModel.Filter.StartDate:yyyy-MM-dd} to {viewModel.Filter.EndDate:yyyy-MM-dd}");
                            col.Item().AlignCenter().Text($"Generated: {generatedOn}");
                            col.Item().AlignCenter().Text($"Current User: {currentUser}");
                        });

                        // Content: SALES REPORT sections only
                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            // Summary KPIs
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                                table.Cell().Element(Container => Container.Padding(2)).Text("Total Revenue");
                                table.Cell().Element(Container => Container.Padding(2)).Text(viewModel.RevenueReport.TotalRevenue.ToString("C", ph));
                                table.Cell().Element(Container => Container.Padding(2)).Text("Total Qty Sold");
                                table.Cell().Element(Container => Container.Padding(2)).Text(viewModel.SalesPerformanceReport.TotalQuantitySold.ToString());
                                table.Cell().Element(Container => Container.Padding(2)).Text("Transactions");
                                table.Cell().Element(Container => Container.Padding(2)).Text(viewModel.SalesPerformanceReport.TransactionCount.ToString());
                                table.Cell().Element(Container => Container.Padding(2)).Text("Inventory Value");
                                table.Cell().Element(Container => Container.Padding(2)).Text(viewModel.TotalInventoryValue.ToString("C", ph));
                                table.Cell().Element(Container => Container.Padding(2)).Text("Low Stock Items");
                                table.Cell().Element(Container => Container.Padding(2)).Text(viewModel.LowStockItemCount.ToString());
                            });

                            // Sales Performance
                            col.Item().PaddingTop(10).Text("Sales Performance").Bold();
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                                table.Cell().Element(Container => Container.Padding(2)).Text("Transactions").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Total Qty Sold").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Total Revenue").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text(viewModel.SalesPerformanceReport.TransactionCount.ToString());
                                table.Cell().Element(Container => Container.Padding(2)).Text(viewModel.SalesPerformanceReport.TotalQuantitySold.ToString());
                                table.Cell().Element(Container => Container.Padding(2)).Text(viewModel.SalesPerformanceReport.TotalRevenue.ToString("C", ph));
                            });

                            // Most Sold Products
                            col.Item().PaddingTop(10).Text("Most Sold Products").Bold();
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                                table.Cell().Element(Container => Container.Padding(2)).Text("Product").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Qty Sold").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Unit Price").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Revenue").Bold();
                                foreach (var p in viewModel.MostSoldProducts)
                                {
                                    table.Cell().Element(Container => Container.Padding(2)).Text(p.ProductName);
                                    table.Cell().Element(Container => Container.Padding(2)).Text(p.QuantitySold.ToString());
                                    table.Cell().Element(Container => Container.Padding(2)).Text(p.UnitPrice.ToString("C", ph));
                                    table.Cell().Element(Container => Container.Padding(2)).Text(p.Revenue.ToString("C", ph));
                                }
                            });

                            // Revenue Trend
                            col.Item().PaddingTop(10).Text("Revenue Trend").Bold();
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                                table.Cell().Element(Container => Container.Padding(2)).Text("Period").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Revenue").Bold();
                                foreach (var r in viewModel.RevenueTrend)
                                {
                                    table.Cell().Element(Container => Container.Padding(2)).Text(r.Period.ToString("yyyy-MM-dd"));
                                    table.Cell().Element(Container => Container.Padding(2)).Text(r.Revenue.ToString("C", ph));
                                }
                            });

                            // Sales By Category
                            col.Item().PaddingTop(10).Text("Sales By Category").Bold();
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                                table.Cell().Element(Container => Container.Padding(2)).Text("Category").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Revenue").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Units Sold").Bold();
                                foreach (var cat in viewModel.SalesByCategory)
                                {
                                    table.Cell().Element(Container => Container.Padding(2)).Text(cat.CategoryName);
                                    table.Cell().Element(Container => Container.Padding(2)).Text(cat.Revenue.ToString("C", ph));
                                    table.Cell().Element(Container => Container.Padding(2)).Text(cat.UnitsSold.ToString());
                                }
                            });
                        });
                    });
                });

                var pdfBytes = doc.GeneratePdf();
                var fileName = $"SalesReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Unable to generate the PDF report. Please try again.";
                return RedirectToAction(nameof(Index), new { report = "Sales", filter.StartDate, filter.EndDate, filter.ProductId, filter.CategoryId, filter.SerialNumber });
            }
        }

        // GET: /Reports/ExportInventoryPdf
        [HttpGet]
        public async Task<IActionResult> ExportInventoryPdf([FromQuery] ReportFilterViewModel filter)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;
            if (!IsOwnerOrManager())
            {
                TempData["ErrorMessage"] = "Access denied. Admin or Manager privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }
            try
            {
                // Same filtering rules as the browser Inventory Report
                var viewModel = await BuildReportViewModelAsync(filter);
                var currentUser = GetCurrentStaffName();
                var generatedOn = DateTime.Now.ToString("MMMM dd, yyyy h:mm tt");

                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(40);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        // Header
                        page.Header().Column(col =>
                        {
                            col.Item().AlignCenter().Text("KAIJENSON MOTOR SHOP").Bold().FontSize(16);
                            col.Item().AlignCenter().Text("INVENTORY REPORT").Bold().FontSize(12);
                            col.Item().PaddingTop(4).AlignCenter().Text($"As of Date: {viewModel.Filter.EndDate:yyyy-MM-dd}");
                            col.Item().AlignCenter().Text($"Generated: {generatedOn}");
                            col.Item().AlignCenter().Text($"Current User: {currentUser}");
                        });

                        // Content: INVENTORY REPORT sections only
                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            // Current Stock
                            col.Item().Text("Current Stock").Bold();
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                                table.Cell().Element(Container => Container.Padding(2)).Text("Product").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Category").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Qty On Hand").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Status").Bold();
                                foreach (var i in viewModel.InventoryReport.Items)
                                {
                                    table.Cell().Element(Container => Container.Padding(2)).Text(i.ProductName);
                                    table.Cell().Element(Container => Container.Padding(2)).Text(i.CategoryName);
                                    table.Cell().Element(Container => Container.Padding(2)).Text(i.QuantityOnHand.ToString());
                                    table.Cell().Element(Container => Container.Padding(2)).Text(i.StockStatus);
                                }
                            });

                            // Low Stock Alerts
                            col.Item().PaddingTop(10).Text("Low Stock Alerts").Bold();
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                                table.Cell().Element(Container => Container.Padding(2)).Text("Product").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Qty On Hand").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Reorder Level").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Status").Bold();
                                foreach (var a in viewModel.LowStockAlerts)
                                {
                                    table.Cell().Element(Container => Container.Padding(2)).Text(a.ProductName);
                                    table.Cell().Element(Container => Container.Padding(2)).Text(a.QuantityOnHand.ToString());
                                    table.Cell().Element(Container => Container.Padding(2)).Text(a.ReorderLevel.ToString());
                                    table.Cell().Element(Container => Container.Padding(2)).Text(a.StockStatus);
                                }
                            });

                            // Stock Movements
                            col.Item().PaddingTop(10).Text("Stock Movements").Bold();
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                                table.Cell().Element(Container => Container.Padding(2)).Text("Date").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Product").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Type").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Qty").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Reference").Bold();
                                foreach (var m in viewModel.StockMovements)
                                {
                                    table.Cell().Element(Container => Container.Padding(2)).Text(m.Date.ToString("yyyy-MM-dd"));
                                    table.Cell().Element(Container => Container.Padding(2)).Text(m.ProductName);
                                    table.Cell().Element(Container => Container.Padding(2)).Text(m.MovementType);
                                    table.Cell().Element(Container => Container.Padding(2)).Text(m.Quantity.ToString());
                                    table.Cell().Element(Container => Container.Padding(2)).Text(m.Reference);
                                }
                            });

                            // Serial Numbers
                            col.Item().PaddingTop(10).Text("Serial Numbers").Bold();
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                                table.Cell().Element(Container => Container.Padding(2)).Text("Serial").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Product").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Status").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Sale Id").Bold();
                                table.Cell().Element(Container => Container.Padding(2)).Text("Sale Date").Bold();
                                foreach (var s in viewModel.SerialNumberReport)
                                {
                                    table.Cell().Element(Container => Container.Padding(2)).Text(s.SerialNumber);
                                    table.Cell().Element(Container => Container.Padding(2)).Text(s.ProductName);
                                    table.Cell().Element(Container => Container.Padding(2)).Text(s.Status);
                                    table.Cell().Element(Container => Container.Padding(2)).Text(s.SaleId?.ToString() ?? "-");
                                    table.Cell().Element(Container => Container.Padding(2)).Text(s.SaleDate?.ToString("yyyy-MM-dd") ?? "-");
                                }
                            });
                        });
                    });
                });

                var pdfBytes = doc.GeneratePdf();
                var fileName = $"InventoryReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Unable to generate the PDF report. Please try again.";
                return RedirectToAction(nameof(Index), new { report = "Inventory", filter.StartDate, filter.EndDate, filter.ProductId, filter.CategoryId, filter.SerialNumber });
            }
        }

        // Private helper to construct the same view model used by Index
        private async Task<ReportsPageViewModel> BuildReportViewModelAsync(ReportFilterViewModel filter)
        {
            // Replicate the logic from Index (excluding view rendering)
            // Ensure defaults
            if (filter.StartDate == default) filter.StartDate = DateTime.Today.AddMonths(-1);
            if (filter.EndDate == default) filter.EndDate = DateTime.Today;
            var start = filter.StartDate.Date;
            var end = filter.EndDate.Date;
            var inventory = await _reportService.GetInventoryReportAsync(start, end);
            var mostSold = await _reportService.GetMostSoldProductsAsync(start, end);
            var stockMovements = await _reportService.GetStockMovementsAsync(start, end);
            var serials = await _reportService.GetSerialNumberReportAsync(start, end);
            SalesPerformanceReportViewModel salesPerf;
            RevenueReportViewModel revenue;
            var startInclusive = start;
            var endExclusive = end.AddDays(1);
            var salesQuery = _context.SalesTransactions
                .Where(t => t.TransactionDate >= startInclusive && t.TransactionDate < endExclusive);
            if (filter.ProductId.HasValue || filter.CategoryId.HasValue)
            {
                var productIdsQuery = _context.Products.AsQueryable();
                if (filter.ProductId.HasValue)
                    productIdsQuery = productIdsQuery.Where(p => p.ProductId == filter.ProductId.Value);
                if (filter.CategoryId.HasValue)
                    productIdsQuery = productIdsQuery.Where(p => p.CategoryId == filter.CategoryId.Value);
                var productIds = await productIdsQuery.Select(p => p.ProductId).ToListAsync();
                var salesItems = _context.SalesItems
                    .Where(si => productIds.Contains(si.ProductId) && si.Transaction.TransactionDate >= startInclusive && si.Transaction.TransactionDate < endExclusive);
                var transactionIds = await salesItems.Select(si => si.TransactionId).Distinct().ToListAsync();
                var transactionCount = await _context.SalesTransactions
                    .Where(t => transactionIds.Contains(t.TransactionId)).CountAsync();
                var totalQuantity = await salesItems.SumAsync(si => si.Quantity);
                var totalRevenue = await salesItems.SumAsync(si => si.Quantity * si.UnitPrice);
                salesPerf = new SalesPerformanceReportViewModel { TransactionCount = transactionCount, TotalQuantitySold = totalQuantity, TotalRevenue = totalRevenue };
                revenue = new RevenueReportViewModel { TotalRevenue = totalRevenue };
            }
            else
            {
                salesPerf = await _reportService.GetSalesPerformanceReportAsync(start, end);
                revenue = await _reportService.GetRevenueReportAsync(start, end);
            }
            if (filter.ProductId.HasValue)
            {
                var product = await _context.Products.FindAsync(filter.ProductId.Value);
                if (product != null)
                {
                    var productName = product.ProductName;
                    inventory.Items = inventory.Items.Where(i => i.ProductName == productName).ToList();
                    mostSold = mostSold.Where(p => p.ProductName == productName).ToList();
                    stockMovements = stockMovements.Where(m => m.ProductName == productName).ToList();
                    serials = serials.Where(s => s.ProductName == productName).ToList();
                }
            }
            if (filter.CategoryId.HasValue)
            {
                var category = await _context.Categories.FindAsync(filter.CategoryId.Value);
                if (category != null)
                {
                    var categoryName = category.CategoryName;
                    inventory.Items = inventory.Items.Where(i => i.CategoryName == categoryName).ToList();
                    var categoryProductNames = await _context.Products
                        .Where(p => p.CategoryId == filter.CategoryId.Value)
                        .Select(p => p.ProductName)
                        .ToListAsync();
                    mostSold = mostSold.Where(p => categoryProductNames.Contains(p.ProductName)).ToList();
                    stockMovements = stockMovements.Where(m => categoryProductNames.Contains(m.ProductName)).ToList();
                    serials = serials.Where(s => categoryProductNames.Contains(s.ProductName)).ToList();
                }
            }
            if (!string.IsNullOrWhiteSpace(filter.SerialNumber))
            {
                var trimmed = filter.SerialNumber.Trim();
                serials = serials.Where(s => string.Equals(s.SerialNumber, trimmed, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            var totalInventoryValue = await _reportService.GetTotalInventoryValueAsync(start, end, filter.ProductId, filter.CategoryId);
            var lowStockItemCount = await _reportService.GetLowStockItemCountAsync(start, end, filter.ProductId, filter.CategoryId);
            var lowStockAlerts = await _reportService.GetLowStockAlertsAsync(start, end, filter.ProductId, filter.CategoryId);
            var revenueTrend = await _reportService.GetRevenueTrendAsync(start, end, filter.ProductId, filter.CategoryId);
            var salesByCategory = await _reportService.GetSalesByCategoryAsync(start, end, filter.ProductId, filter.CategoryId);

                        return new ReportsPageViewModel
                        {
                            Filter = filter,
                            InventoryReport = inventory,
                            SalesPerformanceReport = salesPerf,
                            RevenueReport = revenue,
                            MostSoldProducts = mostSold,
                            StockMovements = stockMovements,
                            SerialNumberReport = serials,
                            TotalInventoryValue = totalInventoryValue,
                            LowStockItemCount = lowStockItemCount,
                            LowStockAlerts = lowStockAlerts,
                            RevenueTrend = revenueTrend,
                            SalesByCategory = salesByCategory,

                        };
                    }

    }
}
