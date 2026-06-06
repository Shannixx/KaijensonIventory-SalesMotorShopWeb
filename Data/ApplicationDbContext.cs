using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<Mechanic> Mechanics => Set<Mechanic>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Service> Services => Set<Service>();
        public DbSet<Staff> Staff => Set<Staff>();
        public DbSet<SalesTransaction> SalesTransactions => Set<SalesTransaction>();
        public DbSet<SalesItem> SalesItems => Set<SalesItem>();
        public DbSet<ServiceTransaction> ServiceTransactions => Set<ServiceTransaction>();
        public DbSet<ServicePartUsed> ServicePartsUsed => Set<ServicePartUsed>();
        public DbSet<StockIn> StockIns => Set<StockIn>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<Backup> Backups => Set<Backup>();
        public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
        public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
        public DbSet<Customer> Customers => Set<Customer>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Category>()
                .HasIndex(c => c.CategoryName).IsUnique();

            modelBuilder.Entity<Staff>()
                .HasIndex(s => s.UserName).IsUnique();

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category).WithMany(c => c.Products).HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Supplier).WithMany(s => s.Products).HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Service>()
                .HasOne(s => s.Category).WithMany().HasForeignKey(s => s.CategoryId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Service>()
                .HasOne(s => s.Mechanic).WithMany().HasForeignKey(s => s.MechanicId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SalesTransaction>()
                .HasOne(t => t.Staff).WithMany().HasForeignKey(t => t.StaffId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SalesItem>()
                .HasOne(i => i.Transaction).WithMany(t => t.SalesItems).HasForeignKey(i => i.TransactionId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SalesItem>()
                .HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ServiceTransaction>()
                .HasOne(t => t.Mechanic).WithMany().HasForeignKey(t => t.MechanicId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ServiceTransaction>()
                .HasOne(t => t.Staff).WithMany().HasForeignKey(t => t.StaffId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ServicePartUsed>()
                .HasOne(p => p.ServiceTransaction).WithMany(t => t.PartsUsed).HasForeignKey(p => p.ServiceTxnId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ServicePartUsed>()
                .HasOne(p => p.Product).WithMany().HasForeignKey(p => p.ProductId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockIn>()
                .HasOne(s => s.Product).WithMany().HasForeignKey(s => s.ProductId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockIn>()
                .HasOne(s => s.Supplier).WithMany(sup => sup.StockIns).HasForeignKey(s => s.SupplierId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockIn>()
                .HasOne(s => s.Staff).WithMany().HasForeignKey(s => s.StaffId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Product).WithMany().HasForeignKey(n => n.ProductId).OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ActivityLog>()
                .HasOne(l => l.Staff).WithMany().HasForeignKey(l => l.StaffId).OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<InventoryTransaction>()
                .HasOne(t => t.Product).WithMany().HasForeignKey(t => t.ProductId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryTransaction>()
                .HasOne(t => t.Staff).WithMany().HasForeignKey(t => t.StaffId).OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<InventoryTransaction>()
                .HasIndex(t => t.TransactionDate);

            modelBuilder.Entity<InventoryTransaction>()
                .HasIndex(t => new { t.ProductId, t.TransactionDate });

            modelBuilder.Entity<Customer>()
                .HasIndex(c => c.CustomerName);

            modelBuilder.Entity<SalesTransaction>()
                .HasOne(t => t.Customer).WithMany(c => c.SalesTransactions).HasForeignKey(t => t.CustomerId).OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ServiceTransaction>()
                .HasOne(t => t.Customer).WithMany(c => c.ServiceTransactions).HasForeignKey(t => t.CustomerId).OnDelete(DeleteBehavior.SetNull);
        }
    }
}
