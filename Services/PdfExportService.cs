using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using KaijensonIventory_SalesMotorShopWeb.Models;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public class PdfExportService
    {
        public PdfExportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateSalesReceipt(SalesTransaction transaction)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5);
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontFamily("Inter").FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().AlignCenter().Text("KAIJENSON MOTOR SHOP").Bold().FontSize(16).FontColor("#FF7F11");
                        col.Item().AlignCenter().Text("Sales & Inventory System").FontSize(9).FontColor(Colors.Grey.Medium);
                        col.Item().PaddingVertical(5).LineHorizontal(1).LineColor("#FF7F11");
                        col.Item().PaddingVertical(3).Text($"Invoice: {transaction.InvoiceNumber}").Bold();
                        col.Item().Text($"Date: {transaction.TransactionDate:MMM dd, yyyy HH:mm}");
                        col.Item().Text($"Customer: {transaction.CustomerName}");
                        col.Item().Text($"Cashier: {transaction.Staff?.StaffName ?? "N/A"}");
                        col.Item().PaddingVertical(3).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().PaddingVertical(5).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(1);
                            c.RelativeColumn(2);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Text("Item").Bold().FontSize(9);
                            h.Cell().AlignRight().Text("Qty").Bold().FontSize(9);
                            h.Cell().AlignRight().Text("Subtotal").Bold().FontSize(9);
                        });

                        foreach (var item in transaction.SalesItems)
                        {
                            table.Cell().Text(item.Product?.ProductName ?? "Unknown").FontSize(9);
                            table.Cell().AlignRight().Text(item.Quantity.ToString()).FontSize(9);
                            table.Cell().AlignRight().Text(item.Subtotal.ToString("N2")).FontSize(9);
                        }
                    });

                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        col.Item().AlignRight().Text($"Total: {transaction.TotalAmount:N2}").Bold().FontSize(12);
                        col.Item().AlignRight().Text($"Amount Paid: {transaction.AmountPaid:N2}").FontSize(10);
                        col.Item().AlignRight().Text($"Change: {transaction.Change:N2}").FontSize(10);
                        col.Item().PaddingTop(10).AlignCenter().Text("Thank you for your business!").FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            }).GeneratePdf();
        }

        public byte[] GenerateSalesReport(List<SalesTransaction> transactions, DateTime? dateFrom, DateTime? dateTo)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontFamily("Inter").FontSize(9));

                    page.Header().Column(col =>
                    {
                        col.Item().AlignCenter().Text("KAIJENSON MOTOR SHOP").Bold().FontSize(16).FontColor("#FF7F11");
                        col.Item().AlignCenter().Text("Sales Summary Report").FontSize(12);
                        if (dateFrom.HasValue || dateTo.HasValue)
                        {
                            string range = $"Period: {dateFrom?.ToString("MMM dd, yyyy") ?? "Start"} - {dateTo?.ToString("MMM dd, yyyy") ?? "End"}";
                            col.Item().AlignCenter().Text(range).FontSize(9).FontColor(Colors.Grey.Medium);
                        }
                        col.Item().PaddingVertical(3).LineHorizontal(1).LineColor("#FF7F11");
                    });

                    page.Content().PaddingVertical(5).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Text("Invoice #").Bold().FontSize(9);
                            h.Cell().Text("Customer").Bold().FontSize(9);
                            h.Cell().Text("Date").Bold().FontSize(9);
                            h.Cell().AlignRight().Text("Amount").Bold().FontSize(9);
                            h.Cell().Text("Cashier").Bold().FontSize(9);
                        });

                        foreach (var t in transactions)
                        {
                            table.Cell().Text(t.InvoiceNumber).FontSize(8);
                            table.Cell().Text(t.CustomerName).FontSize(8);
                            table.Cell().Text(t.TransactionDate.ToString("MMM dd, yyyy")).FontSize(8);
                            table.Cell().AlignRight().Text(t.TotalAmount.ToString("N2")).FontSize(8);
                            table.Cell().Text(t.Staff?.StaffName ?? "").FontSize(8);
                        }
                    });

                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        col.Item().AlignRight().Text($"Total Records: {transactions.Count} | Grand Total: {transactions.Sum(t => t.TotalAmount):N2}").Bold().FontSize(10);
                        col.Item().AlignRight().Text($"Generated: {DateTime.Now:MMM dd, yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            }).GeneratePdf();
        }

        public byte[] GenerateInventoryReport(List<Product> products)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontFamily("Inter").FontSize(9));

                    page.Header().Column(col =>
                    {
                        col.Item().AlignCenter().Text("KAIJENSON MOTOR SHOP").Bold().FontSize(16).FontColor("#FF7F11");
                        col.Item().AlignCenter().Text("Inventory Summary Report").FontSize(12);
                        col.Item().PaddingVertical(3).LineHorizontal(1).LineColor("#FF7F11");
                    });

                    page.Content().PaddingVertical(5).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Text("Product").Bold().FontSize(9);
                            h.Cell().Text("Brand").Bold().FontSize(9);
                            h.Cell().Text("Category").Bold().FontSize(9);
                            h.Cell().AlignRight().Text("Price").Bold().FontSize(9);
                            h.Cell().AlignRight().Text("Stock").Bold().FontSize(9);
                            h.Cell().Text("Status").Bold().FontSize(9);
                        });

                        foreach (var p in products)
                        {
                            table.Cell().Text(p.ProductName).FontSize(8);
                            table.Cell().Text(p.Brand ?? "-").FontSize(8);
                            table.Cell().Text(p.Category?.CategoryName ?? "-").FontSize(8);
                            table.Cell().AlignRight().Text(p.Price.ToString("N2")).FontSize(8);
                            table.Cell().AlignRight().Text(p.QuantityOnHand.ToString()).FontSize(8);
                            table.Cell().Text(p.StockStatus).FontSize(8);
                        }
                    });

                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        col.Item().AlignRight().Text($"Total Products: {products.Count} | Total Value: {products.Sum(p => p.Price * p.QuantityOnHand):N2}").FontSize(10);
                        col.Item().AlignRight().Text($"Generated: {DateTime.Now:MMM dd, yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            }).GeneratePdf();
        }
    }
}
