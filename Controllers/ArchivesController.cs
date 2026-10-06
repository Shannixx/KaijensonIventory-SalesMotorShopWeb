using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public sealed class ArchivesController : BaseController
    {
        private readonly ApplicationDbContext _context;

        public ArchivesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            var groups = new List<ArchiveGroupViewModel>();
            var categories = await _context.Categories.AsNoTracking()
                .Where(c => c.IsDeleted)
                .Select(c => new ArchivedRecordViewModel
                {
                    Id = c.CategoryId, Name = c.CategoryName, SecondaryText = c.Description,
                    ArchivedAt = c.DeletedAt, ControllerName = "Categories"
                }).ToListAsync();
            groups.Add(new ArchiveGroupViewModel("Categories", "Categories", categories));

            var brands = await _context.Brands.AsNoTracking()
                .Where(b => b.IsDeleted)
                .Select(b => new ArchivedRecordViewModel
                {
                    Id = b.BrandId, Name = b.BrandName, SecondaryText = b.Description,
                    ArchivedAt = b.DeletedAt, ControllerName = "Brands"
                }).ToListAsync();
            groups.Add(new ArchiveGroupViewModel("Brands", "Brands", brands));

            var mechanics = await _context.Mechanics.AsNoTracking()
                .Where(m => m.IsDeleted)
                .Select(m => new ArchivedRecordViewModel
                {
                    Id = m.MechanicId, Name = m.MechanicName, SecondaryText = m.Specialization,
                    ArchivedAt = m.DeletedAt, ControllerName = "Mechanics"
                }).ToListAsync();
            groups.Add(new ArchiveGroupViewModel("Mechanics", "Mechanics", mechanics));

            var suppliers = await _context.Suppliers.AsNoTracking()
                .Where(s => s.IsDeleted)
                .Select(s => new ArchivedRecordViewModel
                {
                    Id = s.SupplierId, Name = s.CompanyName, SecondaryText = s.ContactPerson,
                    ArchivedAt = s.DeletedAt, ControllerName = "Suppliers"
                }).ToListAsync();
            groups.Add(new ArchiveGroupViewModel("Suppliers", "Suppliers", suppliers));

            var products = await _context.Products.AsNoTracking()
                .Where(p => p.IsDeleted)
                .Select(p => new ArchivedRecordViewModel
                {
                    Id = p.ProductId, Name = p.ProductName, SecondaryText = p.Brand,
                    ArchivedAt = p.DeletedAt, ControllerName = "Products"
                }).ToListAsync();
            groups.Add(new ArchiveGroupViewModel("Products", "Products", products));

            var orders = await _context.PurchaseOrders.AsNoTracking()
                .Where(p => p.IsDeleted)
                .Select(p => new ArchivedRecordViewModel
                {
                    Id = p.PurchaseOrderId, Name = p.PurchaseOrderNumber,
                    SecondaryText = p.Supplier != null ? p.Supplier.CompanyName : null,
                    ArchivedAt = p.DeletedAt, ControllerName = "PurchaseOrders"
                }).ToListAsync();
            groups.Add(new ArchiveGroupViewModel("PurchaseOrders", "Purchase Orders", orders));

            var deliveries = await _context.Deliveries.AsNoTracking()
                .Where(d => d.IsDeleted)
                .Select(d => new ArchivedRecordViewModel
                {
                    Id = d.DeliveryId,
                    Name = d.PurchaseOrder != null ? d.PurchaseOrder.PurchaseOrderNumber : "Delivery",
                    SecondaryText = d.Status, ArchivedAt = d.DeletedAt, ControllerName = "Delivery"
                }).ToListAsync();
            groups.Add(new ArchiveGroupViewModel("Deliveries", "Deliveries", deliveries));

            var services = await _context.Services.AsNoTracking()
                .Where(s => s.IsDeleted)
                .Select(s => new ArchivedRecordViewModel
                {
                    Id = s.ServiceId, Name = s.ServiceName, SecondaryText = s.Description,
                    ArchivedAt = s.DeletedAt, ControllerName = "Services"
                }).ToListAsync();
            groups.Add(new ArchiveGroupViewModel("Services", "Services", services));

            // Records come from fixed, server-owned archive sources and controller routes.
            var orderedGroups = groups.Select(group => group with
            {
                Records = group.Records
                    .OrderByDescending(record => record.ArchivedAt)
                    .ThenBy(record => record.Name)
                    .ToList()
            }).ToList();

            return View(new ArchivesIndexViewModel
            {
                Groups = orderedGroups
            });
        }
    }
}
