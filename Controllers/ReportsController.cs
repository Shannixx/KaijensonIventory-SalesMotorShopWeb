using ClosedXML.Excel;
using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.Services;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PdfExportService _pdf;

        public ReportsController(ApplicationDbContext context, PdfExportService pdf)
        {
            _context = context;
            _pdf = pdf;
        }

        private bool SessionValid()
        {
            return HttpContext.Session.GetInt32("StaffId").HasValue;
        }

        private IActionResult RedirectToLogin()
        {
            TempData["ErrorMessage"] = "Session expired. Please log in again.";
            return RedirectToAction("Login", "Account");
        }

        public async Task<IActionResult> Index()
        {
            if (!SessionValid()) return RedirectToLogin();

            var stats = new ReportStatsViewModel
            {
                TotalSales = await _context.SalesTransactions.CountAsync(),
                TotalInventory = await _context.Products.CountAsync(),
                TotalServices = await _context.ServiceTransactions.CountAsync(),
                TotalActivities = await _context.ActivityLogs.CountAsync(),
                TotalCustomers = await _context.Customers.CountAsync(),
                TotalSuppliers = await _context.Suppliers.CountAsync(),
                TotalPurchaseOrders = await _context.PurchaseOrders.CountAsync()
            };

            return View(stats);
        }

        // ──────────────────────────────────────────────
        //  SALES SUMMARY
        // ──────────────────────────────────────────────
        public async Task<IActionResult> SalesSummary(DateTime? dateFrom, DateTime? dateTo, string? search, int page = 1)
        {
            if (!SessionValid()) return RedirectToLogin();

            try
            {
                int pageSize = 10;
                IQueryable<SalesTransaction> query = _context.SalesTransactions
                    .Include(t => t.Staff)
                    .AsNoTracking();

                if (dateFrom.HasValue)
                    query = query.Where(t => t.TransactionDate >= dateFrom.Value);
                if (dateTo.HasValue)
                    query = query.Where(t => t.TransactionDate <= dateTo.Value.Date.AddDays(1));
                if (!string.IsNullOrWhiteSpace(search))
                    query = query.Where(t => t.InvoiceNumber.Contains(search) || t.CustomerName.Contains(search));

                var stats = new ReportStatsViewModel
                {
                    TotalSales = await query.CountAsync(),
                    TotalInventory = await _context.Products.CountAsync(),
                    TotalServices = await _context.ServiceTransactions.CountAsync(),
                    TotalActivities = await _context.ActivityLogs.CountAsync()
                };

                int total = await query.CountAsync();
                var items = await query
                    .OrderByDescending(t => t.TransactionDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["DateFrom"] = dateFrom?.ToString("yyyy-MM-dd");
                ViewData["DateTo"] = dateTo?.ToString("yyyy-MM-dd");
                ViewData["Search"] = search;

                ViewBag.Stats = stats;
                return View(items);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while generating the sales summary report.";
                return View(new List<SalesTransaction>());
            }
        }

        // ──────────────────────────────────────────────
        //  INVENTORY SUMMARY
        // ──────────────────────────────────────────────
        public async Task<IActionResult> InventorySummary(int? categoryId, string? search, int page = 1)
        {
            if (!SessionValid()) return RedirectToLogin();

            try
            {
                int pageSize = 10;
                IQueryable<Product> query = _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .AsNoTracking();

                if (categoryId.HasValue && categoryId.Value > 0)
                    query = query.Where(p => p.CategoryId == categoryId.Value);
                if (!string.IsNullOrWhiteSpace(search))
                    query = query.Where(p => p.ProductName.Contains(search));

                var stats = new ReportStatsViewModel
                {
                    TotalSales = await _context.SalesTransactions.CountAsync(),
                    TotalInventory = await query.CountAsync(),
                    TotalServices = await _context.ServiceTransactions.CountAsync(),
                    TotalActivities = await _context.ActivityLogs.CountAsync()
                };

                int total = await query.CountAsync();
                var items = await query
                    .OrderBy(p => p.ProductName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["CategoryId"] = categoryId;
                ViewData["Search"] = search;

                ViewBag.Categories = new SelectList(
                    await _context.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync(),
                    "CategoryId", "CategoryName", categoryId);
                ViewBag.Stats = stats;

                return View(items);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while generating the inventory summary report.";
                return View(new List<Product>());
            }
        }

        // ──────────────────────────────────────────────
        //  SERVICE SUMMARY
        // ──────────────────────────────────────────────
        public async Task<IActionResult> ServiceSummary(DateTime? dateFrom, DateTime? dateTo, string? status, string? search, int page = 1)
        {
            if (!SessionValid()) return RedirectToLogin();

            try
            {
                int pageSize = 10;
                IQueryable<ServiceTransaction> query = _context.ServiceTransactions
                    .Include(s => s.Mechanic)
                    .AsNoTracking();

                if (dateFrom.HasValue)
                    query = query.Where(s => s.Date >= dateFrom.Value);
                if (dateTo.HasValue)
                    query = query.Where(s => s.Date <= dateTo.Value.Date.AddDays(1));
                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(s => s.Status == status);
                if (!string.IsNullOrWhiteSpace(search))
                    query = query.Where(s => s.CustomerName.Contains(search));

                var stats = new ReportStatsViewModel
                {
                    TotalSales = await _context.SalesTransactions.CountAsync(),
                    TotalInventory = await _context.Products.CountAsync(),
                    TotalServices = await query.CountAsync(),
                    TotalActivities = await _context.ActivityLogs.CountAsync()
                };

                int total = await query.CountAsync();
                var items = await query
                    .OrderByDescending(s => s.Date)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["DateFrom"] = dateFrom?.ToString("yyyy-MM-dd");
                ViewData["DateTo"] = dateTo?.ToString("yyyy-MM-dd");
                ViewData["Status"] = status;
                ViewData["Search"] = search;

                ViewBag.Stats = stats;
                return View(items);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while generating the service summary report.";
                return View(new List<ServiceTransaction>());
            }
        }

        // ──────────────────────────────────────────────
        //  ACTIVITY SUMMARY
        // ──────────────────────────────────────────────
        public async Task<IActionResult> ActivitySummary(DateTime? dateFrom, DateTime? dateTo, string? module, string? search, int page = 1)
        {
            if (!SessionValid()) return RedirectToLogin();

            try
            {
                int pageSize = 20;
                IQueryable<ActivityLog> query = _context.ActivityLogs
                    .Include(l => l.Staff)
                    .AsNoTracking();

                if (dateFrom.HasValue)
                    query = query.Where(l => l.Timestamp >= dateFrom.Value);
                if (dateTo.HasValue)
                    query = query.Where(l => l.Timestamp <= dateTo.Value.Date.AddDays(1));
                if (!string.IsNullOrWhiteSpace(module))
                    query = query.Where(l => l.Module == module);
                if (!string.IsNullOrWhiteSpace(search))
                    query = query.Where(l => l.Action.Contains(search) || l.Description!.Contains(search) || l.Module.Contains(search));

                var stats = new ReportStatsViewModel
                {
                    TotalSales = await _context.SalesTransactions.CountAsync(),
                    TotalInventory = await _context.Products.CountAsync(),
                    TotalServices = await _context.ServiceTransactions.CountAsync(),
                    TotalActivities = await query.CountAsync()
                };

                int total = await query.CountAsync();
                var items = await query
                    .OrderByDescending(l => l.Timestamp)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["DateFrom"] = dateFrom?.ToString("yyyy-MM-dd");
                ViewData["DateTo"] = dateTo?.ToString("yyyy-MM-dd");
                ViewData["Module"] = module;
                ViewData["Search"] = search;

                ViewBag.Modules = new SelectList(
                    await _context.ActivityLogs.Select(l => l.Module).Distinct().OrderBy(m => m).ToListAsync(),
                    module);
                ViewBag.Stats = stats;

                return View(items);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while generating the activity summary report.";
                return View(new List<ActivityLog>());
            }
        }

        // ──────────────────────────────────────────────
        //  CUSTOMER REPORT
        // ──────────────────────────────────────────────
        public async Task<IActionResult> CustomerReport(string? search, int page = 1)
        {
            if (!SessionValid()) return RedirectToLogin();

            try
            {
                int pageSize = 10;
                IQueryable<Customer> query = _context.Customers.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(search))
                    query = query.Where(c => c.CustomerName.Contains(search) || (c.ContactNumber != null && c.ContactNumber.Contains(search)));

                int total = await query.CountAsync();
                var items = await query
                    .OrderByDescending(c => c.TotalPurchases)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["Search"] = search;

                return View(items);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred.";
                return View(new List<Customer>());
            }
        }

        // ──────────────────────────────────────────────
        //  SUPPLIER REPORT
        // ──────────────────────────────────────────────
        public async Task<IActionResult> SupplierReport(string? search, int page = 1)
        {
            if (!SessionValid()) return RedirectToLogin();

            try
            {
                int pageSize = 10;
                IQueryable<Supplier> query = _context.Suppliers
                    .Include(s => s.Products)
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(search))
                    query = query.Where(s => s.CompanyName.Contains(search));

                int total = await query.CountAsync();
                var items = await query
                    .OrderBy(s => s.CompanyName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["Search"] = search;

                return View(items);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred.";
                return View(new List<Supplier>());
            }
        }

        // ──────────────────────────────────────────────
        //  LOW STOCK REPORT
        // ──────────────────────────────────────────────
        public async Task<IActionResult> LowStockReport(string? search, int page = 1)
        {
            if (!SessionValid()) return RedirectToLogin();

            try
            {
                int pageSize = 20;
                IQueryable<Product> query = _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .Where(p => p.QuantityOnHand <= p.ReorderLevel)
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(search))
                    query = query.Where(p => p.ProductName.Contains(search));

                int total = await query.CountAsync();
                var items = await query
                    .OrderBy(p => p.QuantityOnHand)
                    .ThenBy(p => p.ProductName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["Search"] = search;

                return View(items);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred.";
                return View(new List<Product>());
            }
        }

        // ──────────────────────────────────────────────
        //  PURCHASE ORDER REPORT
        // ──────────────────────────────────────────────
        public async Task<IActionResult> PurchaseOrderReport(string? status, string? search, int page = 1)
        {
            if (!SessionValid()) return RedirectToLogin();

            try
            {
                int pageSize = 10;
                IQueryable<PurchaseOrder> query = _context.PurchaseOrders
                    .Include(p => p.Supplier)
                    .Include(p => p.Staff)
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(p => p.Status == status);
                if (!string.IsNullOrWhiteSpace(search))
                    query = query.Where(p => p.PONumber.Contains(search) || p.Supplier!.CompanyName.Contains(search));

                int total = await query.CountAsync();
                var items = await query
                    .OrderByDescending(p => p.OrderDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["Status"] = status;
                ViewData["Search"] = search;

                return View(items);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred.";
                return View(new List<PurchaseOrder>());
            }
        }

        // ──────────────────────────────────────────────
        //  EXCEL EXPORT — SALES SUMMARY
        // ──────────────────────────────────────────────
        public async Task<IActionResult> ExportSalesSummary(DateTime? dateFrom, DateTime? dateTo, string? search)
        {
            if (!SessionValid()) return RedirectToLogin();

            IQueryable<SalesTransaction> query = _context.SalesTransactions
                .Include(t => t.Staff)
                .AsNoTracking();

            if (dateFrom.HasValue) query = query.Where(t => t.TransactionDate >= dateFrom.Value);
            if (dateTo.HasValue) query = query.Where(t => t.TransactionDate <= dateTo.Value.Date.AddDays(1));
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(t => t.InvoiceNumber.Contains(search) || t.CustomerName.Contains(search));

            var data = await query.OrderByDescending(t => t.TransactionDate).ToListAsync();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Sales Summary");
            int row = 1;

            ws.Cell(row, 1).Value = "Invoice #";
            ws.Cell(row, 2).Value = "Customer";
            ws.Cell(row, 3).Value = "Date";
            ws.Cell(row, 4).Value = "Total Amount";
            ws.Cell(row, 5).Value = "Cashier";
            var headerRange = ws.Range(row, 1, row, 5);
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FF7F11");
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            foreach (var item in data)
            {
                row++;
                ws.Cell(row, 1).Value = item.InvoiceNumber;
                ws.Cell(row, 2).Value = item.CustomerName;
                ws.Cell(row, 3).Value = item.TransactionDate.ToString("MMM dd, yyyy");
                ws.Cell(row, 4).Value = item.TotalAmount;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 5).Value = item.Staff?.StaffName ?? "";
                var dataRange = ws.Range(row, 1, row, 5);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            ws.Columns(1, 5).AdjustToContents();

            var fileName = $"SalesSummary_{DateTime.Now:yyyyMMdd}.xlsx";
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // ──────────────────────────────────────────────
        //  EXCEL EXPORT — INVENTORY SUMMARY
        // ──────────────────────────────────────────────
        public async Task<IActionResult> ExportInventorySummary(int? categoryId, string? search)
        {
            if (!SessionValid()) return RedirectToLogin();

            IQueryable<Product> query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .AsNoTracking();

            if (categoryId.HasValue && categoryId.Value > 0)
                query = query.Where(p => p.CategoryId == categoryId.Value);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.ProductName.Contains(search));

            var data = await query.OrderBy(p => p.ProductName).ToListAsync();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Inventory Summary");
            int row = 1;

            ws.Cell(row, 1).Value = "Product Name";
            ws.Cell(row, 2).Value = "Category";
            ws.Cell(row, 3).Value = "Supplier";
            ws.Cell(row, 4).Value = "Price";
            ws.Cell(row, 5).Value = "Quantity";
            ws.Cell(row, 6).Value = "Status";
            var headerRange = ws.Range(row, 1, row, 6);
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FF7F11");
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            foreach (var item in data)
            {
                row++;
                ws.Cell(row, 1).Value = item.ProductName;
                ws.Cell(row, 2).Value = item.Category?.CategoryName ?? "";
                ws.Cell(row, 3).Value = item.Supplier?.CompanyName ?? "";
                ws.Cell(row, 4).Value = item.Price;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 5).Value = item.QuantityOnHand;
                ws.Cell(row, 6).Value = item.StockStatus;
                var dataRange = ws.Range(row, 1, row, 6);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            ws.Columns(1, 6).AdjustToContents();

            var fileName = $"InventorySummary_{DateTime.Now:yyyyMMdd}.xlsx";
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // ──────────────────────────────────────────────
        //  EXCEL EXPORT — SERVICE SUMMARY
        // ──────────────────────────────────────────────
        public async Task<IActionResult> ExportServiceSummary(DateTime? dateFrom, DateTime? dateTo, string? status, string? search)
        {
            if (!SessionValid()) return RedirectToLogin();

            IQueryable<ServiceTransaction> query = _context.ServiceTransactions
                .Include(s => s.Mechanic)
                .AsNoTracking();

            if (dateFrom.HasValue) query = query.Where(s => s.Date >= dateFrom.Value);
            if (dateTo.HasValue) query = query.Where(s => s.Date <= dateTo.Value.Date.AddDays(1));
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(s => s.Status == status);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(s => s.CustomerName.Contains(search));

            var data = await query.OrderByDescending(s => s.Date).ToListAsync();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Service Summary");
            int row = 1;

            ws.Cell(row, 1).Value = "ID";
            ws.Cell(row, 2).Value = "Customer";
            ws.Cell(row, 3).Value = "Vehicle";
            ws.Cell(row, 4).Value = "Fee";
            ws.Cell(row, 5).Value = "Status";
            ws.Cell(row, 6).Value = "Mechanic";
            ws.Cell(row, 7).Value = "Date";
            var headerRange = ws.Range(row, 1, row, 7);
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FF7F11");
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            foreach (var item in data)
            {
                row++;
                ws.Cell(row, 1).Value = item.ServiceTxnId;
                ws.Cell(row, 2).Value = item.CustomerName;
                ws.Cell(row, 3).Value = $"{item.Make} {item.Model}";
                ws.Cell(row, 4).Value = item.ServiceFee;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 5).Value = item.Status;
                ws.Cell(row, 6).Value = item.Mechanic?.MechanicName ?? "";
                ws.Cell(row, 7).Value = item.Date.ToString("MMM dd, yyyy");
                var dataRange = ws.Range(row, 1, row, 7);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            ws.Columns(1, 7).AdjustToContents();

            var fileName = $"ServiceSummary_{DateTime.Now:yyyyMMdd}.xlsx";
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // ──────────────────────────────────────────────
        //  EXCEL EXPORT — ACTIVITY SUMMARY
        // ──────────────────────────────────────────────
        public async Task<IActionResult> ExportActivitySummary(DateTime? dateFrom, DateTime? dateTo, string? module, string? search)
        {
            if (!SessionValid()) return RedirectToLogin();

            IQueryable<ActivityLog> query = _context.ActivityLogs
                .Include(l => l.Staff)
                .AsNoTracking();

            if (dateFrom.HasValue) query = query.Where(l => l.Timestamp >= dateFrom.Value);
            if (dateTo.HasValue) query = query.Where(l => l.Timestamp <= dateTo.Value.Date.AddDays(1));
            if (!string.IsNullOrWhiteSpace(module)) query = query.Where(l => l.Module == module);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(l => l.Action.Contains(search) || l.Description!.Contains(search) || l.Module.Contains(search));

            var data = await query.OrderByDescending(l => l.Timestamp).ToListAsync();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Activity Summary");
            int row = 1;

            ws.Cell(row, 1).Value = "Timestamp";
            ws.Cell(row, 2).Value = "Module";
            ws.Cell(row, 3).Value = "Action";
            ws.Cell(row, 4).Value = "Staff";
            ws.Cell(row, 5).Value = "Description";
            var headerRange = ws.Range(row, 1, row, 5);
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FF7F11");
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            foreach (var item in data)
            {
                row++;
                ws.Cell(row, 1).Value = item.Timestamp.ToString("MMM dd, yyyy HH:mm");
                ws.Cell(row, 2).Value = item.Module;
                ws.Cell(row, 3).Value = item.Action;
                ws.Cell(row, 4).Value = item.Staff?.StaffName ?? "";
                ws.Cell(row, 5).Value = item.Description ?? "";
                var dataRange = ws.Range(row, 1, row, 5);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            ws.Columns(1, 5).AdjustToContents();

            var fileName = $"ActivitySummary_{DateTime.Now:yyyyMMdd}.xlsx";
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // ──────────────────────────────────────────────
        //  PDF EXPORT — SALES SUMMARY
        // ──────────────────────────────────────────────
        public async Task<IActionResult> PdfSalesSummary(DateTime? dateFrom, DateTime? dateTo, string? search)
        {
            if (!SessionValid()) return RedirectToLogin();

            IQueryable<SalesTransaction> query = _context.SalesTransactions
                .Include(t => t.Staff)
                .AsNoTracking();

            if (dateFrom.HasValue) query = query.Where(t => t.TransactionDate >= dateFrom.Value);
            if (dateTo.HasValue) query = query.Where(t => t.TransactionDate <= dateTo.Value.Date.AddDays(1));
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(t => t.InvoiceNumber.Contains(search) || t.CustomerName.Contains(search));

            var data = await query.OrderByDescending(t => t.TransactionDate).ToListAsync();
            byte[] pdf = _pdf.GenerateSalesReport(data, dateFrom, dateTo);

            var fileName = $"SalesSummary_{DateTime.Now:yyyyMMdd}.pdf";
            return File(pdf, "application/pdf", fileName);
        }

        // ──────────────────────────────────────────────
        //  PDF EXPORT — INVENTORY SUMMARY
        // ──────────────────────────────────────────────
        public async Task<IActionResult> PdfInventorySummary(int? categoryId, string? search)
        {
            if (!SessionValid()) return RedirectToLogin();

            IQueryable<Product> query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .AsNoTracking();

            if (categoryId.HasValue && categoryId.Value > 0)
                query = query.Where(p => p.CategoryId == categoryId.Value);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.ProductName.Contains(search));

            var data = await query.OrderBy(p => p.ProductName).ToListAsync();
            byte[] pdf = _pdf.GenerateInventoryReport(data);

            var fileName = $"InventorySummary_{DateTime.Now:yyyyMMdd}.pdf";
            return File(pdf, "application/pdf", fileName);
        }
    }
}
