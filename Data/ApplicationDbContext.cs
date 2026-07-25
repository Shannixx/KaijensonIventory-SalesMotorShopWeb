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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Brand>()
                .HasIndex(b => b.BrandName).IsUnique();

            modelBuilder.Entity<Category>()
                .HasIndex(c => c.CategoryName).IsUnique();

            modelBuilder.Entity<Staff>()
                .HasIndex(s => s.UserName).IsUnique();

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Supplier).WithMany(s => s.Products).HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Service>()
                .HasOne(s => s.Category).WithMany().HasForeignKey(s => s.CategoryId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Service>()
                .HasOne(s => s.Mechanic).WithMany().HasForeignKey(s => s.MechanicId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ActivityLog>()
                .HasOne(l => l.Staff).WithMany().HasForeignKey(l => l.StaffId).OnDelete(DeleteBehavior.SetNull);

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

            modelBuilder.Entity<Category>().HasData(
                new Category { CategoryId = 1, CategoryName = "Lubricant" },
                new Category { CategoryId = 2, CategoryName = "Accessories" },
                new Category { CategoryId = 3, CategoryName = "SpareParts" }
            );

            modelBuilder.Entity<Brand>().HasData(
                new Brand { BrandId = 1, BrandName = "Honda", CountryOrigin = "Japan", Status = "Active" },
                new Brand { BrandId = 2, BrandName = "Yamaha", CountryOrigin = "Japan", Status = "Active" },
                new Brand { BrandId = 3, BrandName = "Suzuki", CountryOrigin = "Japan", Status = "Active" },
                new Brand { BrandId = 4, BrandName = "Kawasaki", CountryOrigin = "Japan", Status = "Active" },
                new Brand { BrandId = 5, BrandName = "Kymco", CountryOrigin = "Taiwan", Status = "Active" }
            );
        }
    }
}
