using KaijensonIventory_SalesMotorShopWeb.Data;
using Microsoft.Extensions.Logging;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.Services;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF Community license: configured once at startup, before any PDF generation.
QuestPDF.Settings.License = LicenseType.Community;

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<HashingService>();

builder.Services.AddScoped<IActivityLogService, ActivityLogService>();

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ISaleService, SaleService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();

builder.Services.AddScoped<IDeliveryService, DeliveryService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddScoped<IBackupConfigurationService, BackupConfigurationService>();
builder.Services.AddHostedService<AutomaticBackupHostedService>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Apply pending migrations on startup in Development when enabled
if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("ApplyMigrationsOnStartup"))
{
    using var migrationScope = app.Services.CreateScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = migrationScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        migrationDb.Database.Migrate();
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error applying database migrations on startup.");
        throw;
    }
}



using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    if (!db.Staff.Any())
    {
        var hasher = scope.ServiceProvider.GetRequiredService<HashingService>();
        db.Staff.Add(new Staff
        {
            StaffName = "System Admin",
            UserName = "admin",
            PasswordHash = hasher.HashPassword("admin123"),
            Role = "Admin",
            Status = "Approved"
        });
    }

    // Seed categories (only if HasData hasn't populated them)
    if (!db.Categories.Any())
    {
        db.Categories.AddRange(
            new Category { CategoryName = "Lubricant" },
            new Category { CategoryName = "Accessories" },
            new Category { CategoryName = "SpareParts" }
        );
    }

    // Seed suppliers
    if (!db.Suppliers.Any())
    {
        db.Suppliers.AddRange(
            new Supplier { CompanyName = "Honda Parts Trading", ContactPerson = "Juan Dela Cruz", ContactNumber = "09171234567", Address = "Manila" },
            new Supplier { CompanyName = "Yamaha Genuine Parts", ContactPerson = "Maria Santos", ContactNumber = "09181234568", Address = "Quezon City" },
            new Supplier { CompanyName = "Suzuki Auto Supply", ContactPerson = "Pedro Reyes", ContactNumber = "09191234569", Address = "Cebu" },
            new Supplier { CompanyName = "Kawasaki Motors Parts", ContactPerson = "Ana Lopez", ContactNumber = "09201234570", Address = "Davao" },
            new Supplier { CompanyName = "Motorcycle Parts Depot", ContactPerson = "Jose Garcia", ContactNumber = "09211234571", Address = "Bulacan" },
            new Supplier { CompanyName = "Bearing House Inc.", ContactPerson = "Carlos Tan", ContactNumber = "09221234572", Address = "Makati" },
            new Supplier { CompanyName = "Tire City Supply", ContactPerson = "Luis Mendoza", ContactNumber = "09231234573", Address = "Pasig" },
            new Supplier { CompanyName = "Oil Depot Philippines", ContactPerson = "Ramon Villanueva", ContactNumber = "09241234574", Address = "Laguna" }
        );
    }

    // Seed mechanics
    if (!db.Mechanics.Any())
    {
        db.Mechanics.AddRange(
            new Mechanic { MechanicName = "Andres Bonifacio", Specialization = "Engine Overhaul", ContactNumber = "09151234501", Address = "Manila" },
            new Mechanic { MechanicName = "Jose Rizal", Specialization = "Electrical Systems", ContactNumber = "09161234502", Address = "Calamba" },
            new Mechanic { MechanicName = "Emilio Aguinaldo", Specialization = "Brake & Suspension", ContactNumber = "09171234503", Address = "Kawit" },
            new Mechanic { MechanicName = "Gabriela Silang", Specialization = "General Service", ContactNumber = "09181234504", Address = "Ilocos" },
            new Mechanic { MechanicName = "Lapu-Lapu", Specialization = "Transmission & Chain", ContactNumber = "09191234505", Address = "Cebu" }
        );
    }

    db.SaveChanges();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();