using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Brand> Brands => Set<Brand>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<Mechanic> Mechanics => Set<Mechanic>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Service> Services => Set<Service>();
        public DbSet<Staff> Staff => Set<Staff>();
        public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
        public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
        public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
        public DbSet<Delivery> Deliveries => Set<Delivery>();
        public DbSet<SerialUnit> SerialUnits => Set<SerialUnit>();
        public DbSet<DeliveryItem> DeliveryItems => Set<DeliveryItem>();

        // Sales entities (already present in DbContext)
        public DbSet<SalesTransaction> SalesTransactions => Set<SalesTransaction>();
        public DbSet<SalesItem> SalesItems => Set<SalesItem>();
        public DbSet<Notification> Notifications => Set<Notification>();

        // Service job / work order entities (Service remains the catalog definition)
        public DbSet<ServiceJob> ServiceJobs => Set<ServiceJob>();
        public DbSet<ServiceHistory> ServiceHistories => Set<ServiceHistory>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Brand>()
                .HasIndex(b => b.BrandName).IsUnique();

            // Brand -> Supplier (optional)
            modelBuilder.Entity<Brand>()
                .HasOne(b => b.Supplier)
                .WithMany()
                .HasForeignKey(b => b.SupplierId)
                .OnDelete(DeleteBehavior.SetNull);

            // Brand -> CreatedByStaff (audit)
            modelBuilder.Entity<Brand>()
                .HasOne(b => b.CreatedByStaff)
                .WithMany()
                .HasForeignKey(b => b.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull);

            // Supplier -> CreatedByStaff (audit)
            modelBuilder.Entity<Supplier>()
                .HasOne(s => s.CreatedByStaff)
                .WithMany()
                .HasForeignKey(s => s.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Brand>()
                .HasOne(b => b.CreatedByStaff)
                .WithMany()
                .HasForeignKey(b => b.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull);

            // Mechanic -> HiredByStaff (audit)
            modelBuilder.Entity<Mechanic>()
                .HasOne(m => m.HiredByStaff)
                .WithMany()
                .HasForeignKey(m => m.HiredBy)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Category>()
                .HasIndex(c => c.CategoryName).IsUnique();

            modelBuilder.Entity<Staff>()
                .HasIndex(s => s.UserName).IsUnique();

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Supplier).WithMany(s => s.Products).HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.PurchaseOrder).WithMany().HasForeignKey(p => p.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);

            // Audit relationship for CreatedByStaff (Product)
            modelBuilder.Entity<Product>()
                .HasOne(p => p.CreatedByStaff)
                .WithMany()
                .HasForeignKey(p => p.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull);

            // Category is optional for services: they are created with only
            // ServiceName and ServicePrice; products still require a category.
            modelBuilder.Entity<Service>()
                .HasOne(s => s.Category).WithMany().HasForeignKey(s => s.CategoryId).OnDelete(DeleteBehavior.Restrict);

            // Audit relationship for CreatedByStaff (Service)
            modelBuilder.Entity<Service>()
                .HasOne(s => s.CreatedByStaff)
                .WithMany()
                .HasForeignKey(s => s.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull);

            // ServiceJob relationships
            modelBuilder.Entity<ServiceJob>()
                .HasIndex(j => j.ServiceJobNumber).IsUnique();

            modelBuilder.Entity<ServiceJob>()
                .HasOne(j => j.Service).WithMany()
                .HasForeignKey(j => j.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ServiceJob>()
                .HasOne(j => j.Mechanic).WithMany()
                .HasForeignKey(j => j.MechanicId)
                .OnDelete(DeleteBehavior.Restrict);

            // Optional link to an existing sale (single FK direction: ServiceJob -> SalesTransaction).
            // Never cascade into sales data; unlink the job if a sale is ever removed.
            modelBuilder.Entity<ServiceJob>()
                .HasOne(j => j.SalesTransaction).WithMany()
                .HasForeignKey(j => j.SalesTransactionId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ServiceHistory>()
                .HasOne(h => h.ServiceJob).WithMany(j => j.Histories)
                .HasForeignKey(h => h.ServiceJobId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ActivityLog>()
                .HasOne(l => l.Staff).WithMany().HasForeignKey(l => l.StaffId).OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Category>()
                .HasOne(c => c.CreatedByStaff)
                .WithMany()
                .HasForeignKey(c => c.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PurchaseOrder>()
                .HasOne(p => p.Supplier).WithMany(s => s.PurchaseOrders).HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseOrder>()
                .HasOne(p => p.Staff).WithMany().HasForeignKey(p => p.CreatedBy).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseOrder>()
                .HasIndex(p => p.PurchaseOrderNumber).IsUnique();

            modelBuilder.Entity<PurchaseOrderItem>()
                .HasOne(i => i.PurchaseOrder).WithMany(po => po.Items).HasForeignKey(i => i.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PurchaseOrderItem>()
                .HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);

            // Serial unit unique constraint
            modelBuilder.Entity<SerialUnit>()
                .HasIndex(s => s.SerialNumber).IsUnique();

            // SerialUnit relationships
            modelBuilder.Entity<SerialUnit>()
                .HasOne(s => s.Product)
                .WithMany()
                .HasForeignKey(s => s.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ServiceJob>()
                .HasOne(j => j.SalesTransaction)
                .WithMany()
                .HasForeignKey(j => j.SalesTransactionId)
                .OnDelete(DeleteBehavior.SetNull);

            // The staff member who created/processed the service job.
            // Use Restrict to avoid cascade delete cycles with ActivityLog's cascade.
            modelBuilder.Entity<ServiceJob>()
                .HasOne(j => j.ProcessedByStaff)
                .WithMany()
                .HasForeignKey(j => j.ProcessedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            // DeliveryItem relationships
            modelBuilder.Entity<DeliveryItem>()
                .HasOne(di => di.Delivery)
                .WithMany(d => d.Items)
                .HasForeignKey(di => di.DeliveryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DeliveryItem>()
                .HasOne(di => di.PurchaseOrderItem)
                .WithMany()
                .HasForeignKey(di => di.PurchaseOrderItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Category>().HasData(
                new Category
                {
                    CategoryId = 1,
                    CategoryName = "Lubricant",
                    Description = "Products used to lubricate and maintain motorcycle engines, transmissions, and other moving components.",
                    CreatedBy = null,
                    CreatedAt = new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc)
                },
                new Category
                {
                    CategoryId = 2,
                    CategoryName = "Accessories",
                    Description = "Motorcycle accessories and add-on items intended to improve functionality, convenience, protection, or appearance.",
                    CreatedBy = null,
                    CreatedAt = new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc)
                },
                new Category
                {
                    CategoryId = 3,
                    CategoryName = "Spare Parts",
                    Description = "Replacement components used to repair, maintain, or restore motorcycles and their mechanical systems.",
                    CreatedBy = null,
                    CreatedAt = new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc)
                }
            );

            modelBuilder.Entity<Brand>().HasData(
                new Brand { BrandId = 1, BrandName = "Honda", Description = "Japanese motorcycle and automotive brand known for motorcycles, engines, and related mobility products.", CountryOrigin = "Japan", CreatedAt = new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc) },
                new Brand { BrandId = 2, BrandName = "Yamaha", Description = "Japanese manufacturer known for motorcycles, engines, and a wide range of mobility products.", CountryOrigin = "Japan", CreatedAt = new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc) },
                new Brand { BrandId = 3, BrandName = "Suzuki", Description = "Japanese manufacturer producing motorcycles, engines, and other mobility-related products.", CountryOrigin = "Japan", CreatedAt = new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc) },
                new Brand { BrandId = 4, BrandName = "Kawasaki", Description = "Japanese manufacturer known for motorcycles, engines, and other transportation and industrial products.", CountryOrigin = "Japan", CreatedAt = new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc) },
                new Brand { BrandId = 5, BrandName = "Kymco", Description = "Taiwanese manufacturer specializing in scooters, motorcycles, and related mobility products.", CountryOrigin = "Taiwan", CreatedAt = new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc) }
            );
        }
    }
}
