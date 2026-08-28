using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ClosedXML.Excel;
using System.IO;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class ActivityLogController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ActivityLogController> _logger;

        public ActivityLogController(ApplicationDbContext context, ILogger<ActivityLogController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? searchString, string? module, DateTime? dateFrom, DateTime? dateTo, int page = 1)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;

            try
            {
                int pageSize = 20;
                IQueryable<ActivityLog> query = _context.ActivityLogs
                    .Include(l => l.Staff)
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                    query = query.Where(l => (l.Description != null && l.Description.Contains(searchString)) || l.Action.Contains(searchString));

                if (!string.IsNullOrWhiteSpace(module))
                    query = query.Where(l => l.Module == module);

                if (dateFrom.HasValue)
                    query = query.Where(l => l.Timestamp >= dateFrom.Value);
                if (dateTo.HasValue)
                    query = query.Where(l => l.Timestamp <= dateTo.Value.Date.AddDays(1));

                int total = await query.CountAsync();
                List<ActivityLog> logs = await query
                    .OrderByDescending(l => l.Timestamp)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["CurrentFilter"] = searchString;
                ViewData["Module"] = module;
                ViewData["DateFrom"] = dateFrom?.ToString("yyyy-MM-dd");
                ViewData["DateTo"] = dateTo?.ToString("yyyy-MM-dd");
                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);

                ViewBag.Modules = new SelectList(
                    await _context.ActivityLogs.Select(l => l.Module).Distinct().OrderBy(m => m).ToListAsync(),
                    module);

                return View(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving activity logs.");
                TempData["ErrorMessage"] = "An error occurred while loading activity logs. Please try again.";
                return View(new List<ActivityLog>());
            }
        }

        // Export Activity Logs to PDF
        [HttpGet]
        public async Task<IActionResult> ExportActivityLogPdf(string? searchString, string? module, DateTime? dateFrom, DateTime? dateTo)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;
            // Apply same filtering as Index
            IQueryable<ActivityLog> query = _context.ActivityLogs.Include(l => l.Staff).AsNoTracking();
            if (!string.IsNullOrWhiteSpace(searchString))
                query = query.Where(l => (l.Description != null && l.Description.Contains(searchString)) || l.Action.Contains(searchString));
            if (!string.IsNullOrWhiteSpace(module))
                query = query.Where(l => l.Module == module);
            if (dateFrom.HasValue)
                query = query.Where(l => l.Timestamp >= dateFrom.Value);
            if (dateTo.HasValue)
                query = query.Where(l => l.Timestamp <= dateTo.Value.Date.AddDays(1));

            var logs = await query.OrderByDescending(l => l.Timestamp).ToListAsync();
            var culture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            var generatedOn = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10));
                    page.Header().AlignCenter().Text($"Activity Log Export – Generated: {generatedOn}").Bold().FontSize(14);
                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(); // Timestamp
                                c.RelativeColumn(); // Module
                                c.RelativeColumn(); // Action
                                c.RelativeColumn(); // Staff
                                c.RelativeColumn(); // Description
                            });
                            // Header
                            table.Cell().Element(Container => Container.Padding(2)).Text("Timestamp").Bold();
                            table.Cell().Element(Container => Container.Padding(2)).Text("Module").Bold();
                            table.Cell().Element(Container => Container.Padding(2)).Text("Action").Bold();
                            table.Cell().Element(Container => Container.Padding(2)).Text("Staff").Bold();
                            table.Cell().Element(Container => Container.Padding(2)).Text("Description").Bold();
                            foreach (var log in logs)
                            {
                                table.Cell().Element(Container => Container.Padding(2)).Text(log.Timestamp.ToString("yyyy-MM-dd HH:mm"));
                                table.Cell().Element(Container => Container.Padding(2)).Text(log.Module);
                                table.Cell().Element(Container => Container.Padding(2)).Text(log.Action);
                                table.Cell().Element(Container => Container.Padding(2)).Text(log.Staff?.UserName ?? "System");
                                table.Cell().Element(Container => Container.Padding(2)).Text(log.Description ?? "");
                            }
                        });
                    });
                });
            });
            var pdfBytes = doc.GeneratePdf();
            var fileName = $"ActivityLog_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        // Export Activity Logs to Excel
        [HttpGet]
        public async Task<IActionResult> ExportActivityLogExcel(string? searchString, string? module, DateTime? dateFrom, DateTime? dateTo)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;
            IQueryable<ActivityLog> query = _context.ActivityLogs.Include(l => l.Staff).AsNoTracking();
            if (!string.IsNullOrWhiteSpace(searchString))
                query = query.Where(l => (l.Description != null && l.Description.Contains(searchString)) || l.Action.Contains(searchString));
            if (!string.IsNullOrWhiteSpace(module))
                query = query.Where(l => l.Module == module);
            if (dateFrom.HasValue)
                query = query.Where(l => l.Timestamp >= dateFrom.Value);
            if (dateTo.HasValue)
                query = query.Where(l => l.Timestamp <= dateTo.Value.Date.AddDays(1));

            var logs = await query.OrderByDescending(l => l.Timestamp).ToListAsync();
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("ActivityLog");
            // Header row
            ws.Cell(1, 1).Value = "Timestamp";
            ws.Cell(1, 2).Value = "Module";
            ws.Cell(1, 3).Value = "Action";
            ws.Cell(1, 4).Value = "Staff";
            ws.Cell(1, 5).Value = "Description";
            int row = 2;
            foreach (var log in logs)
            {
                ws.Cell(row, 1).Value = log.Timestamp;
                ws.Cell(row, 2).Value = log.Module;
                ws.Cell(row, 3).Value = log.Action;
                ws.Cell(row, 4).Value = log.Staff?.UserName ?? "System";
                ws.Cell(row, 5).Value = log.Description ?? "";
                row++;
            }
            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            var bytes = ms.ToArray();
            var fileName = $"ActivityLog_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}
