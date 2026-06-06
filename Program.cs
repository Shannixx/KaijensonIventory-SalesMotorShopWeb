using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.Services;
using KaijensonIventory_SalesMotorShopWeb.Hubs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<HashingService>();
builder.Services.AddSingleton<PdfExportService>();

builder.Services.AddSignalR();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    if (!db.Staff.Any())
    {
        var hasher = scope.ServiceProvider.GetRequiredService<HashingService>();
        db.Staff.Add(new Staff
        {
            StaffName = "System Admin",
            UserName = "admin",
            PasswordHash = hasher.HashPassword("admin123"),
            Role = "Admin"
        });
    }

    // Seed motorcycle categories
    if (!db.Categories.Any())
    {
        db.Categories.AddRange(
            new Category { CategoryName = "Engine Parts" },
            new Category { CategoryName = "Brake Parts" },
            new Category { CategoryName = "Electrical Parts" },
            new Category { CategoryName = "Oil & Lubricants" },
            new Category { CategoryName = "Filters" },
            new Category { CategoryName = "Tires & Wheels" },
            new Category { CategoryName = "Bearings & Seals" },
            new Category { CategoryName = "Chain & Sprocket" },
            new Category { CategoryName = "Suspension" },
            new Category { CategoryName = "Body & Accessories" },
            new Category { CategoryName = "Gaskets & Seals" },
            new Category { CategoryName = "Lighting & Signals" }
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

    // Seed products (only if none exist)
    if (!db.Products.Any())
    {
        var catEngine = db.Categories.FirstOrDefault(c => c.CategoryName == "Engine Parts");
        var catBrake = db.Categories.FirstOrDefault(c => c.CategoryName == "Brake Parts");
        var catOil = db.Categories.FirstOrDefault(c => c.CategoryName == "Oil & Lubricants");
        var catFilter = db.Categories.FirstOrDefault(c => c.CategoryName == "Filters");
        var catTire = db.Categories.FirstOrDefault(c => c.CategoryName == "Tires & Wheels");
        var catChain = db.Categories.FirstOrDefault(c => c.CategoryName == "Chain & Sprocket");
        var catElectrical = db.Categories.FirstOrDefault(c => c.CategoryName == "Electrical Parts");
        var catBody = db.Categories.FirstOrDefault(c => c.CategoryName == "Body & Accessories");
        var catLighting = db.Categories.FirstOrDefault(c => c.CategoryName == "Lighting & Signals");
        var catSuspension = db.Categories.FirstOrDefault(c => c.CategoryName == "Suspension");

        var supHonda = db.Suppliers.FirstOrDefault(s => s.CompanyName == "Honda Parts Trading");
        var supYamaha = db.Suppliers.FirstOrDefault(s => s.CompanyName == "Yamaha Genuine Parts");
        var supSuzuki = db.Suppliers.FirstOrDefault(s => s.CompanyName == "Suzuki Auto Supply");
        var supKawasaki = db.Suppliers.FirstOrDefault(s => s.CompanyName == "Kawasaki Motors Parts");
        var supTire = db.Suppliers.FirstOrDefault(s => s.CompanyName == "Tire City Supply");
        var supOil = db.Suppliers.FirstOrDefault(s => s.CompanyName == "Oil Depot Philippines");
        var supBearing = db.Suppliers.FirstOrDefault(s => s.CompanyName == "Bearing House Inc.");
        var supMotoDepot = db.Suppliers.FirstOrDefault(s => s.CompanyName == "Motorcycle Parts Depot");

        if (catEngine != null && catBrake != null && catOil != null && catFilter != null && catTire != null && catChain != null && catElectrical != null && catBody != null && catLighting != null && catSuspension != null && supHonda != null && supYamaha != null && supSuzuki != null && supKawasaki != null && supTire != null && supOil != null && supBearing != null && supMotoDepot != null)
        {
            db.Products.AddRange(
                new Product { ProductName = "Brake Pads - Honda Click 125", Brand = "Honda", PartNumber = "06430-K35-931", PartType = "Brake Parts", ModelCompatibility = "Click 125", CategoryId = catBrake.CategoryId, SupplierId = supHonda.SupplierId, Price = 350.00m, AverageCost = 220.00m, QuantityOnHand = 25, ReorderLevel = 10, StockStatus = "Available", Description = "Genuine Honda brake pads for Click 125" },
                new Product { ProductName = "Engine Oil 10W-40 (1L)", Brand = "Honda", PartNumber = "08230-P99K4-LK", PartType = "Oil", ModelCompatibility = "Universal", CategoryId = catOil.CategoryId, SupplierId = supOil.SupplierId, Price = 180.00m, AverageCost = 110.00m, QuantityOnHand = 3, ReorderLevel = 10, StockStatus = "Low Stock", Description = "Honda 4-stroke engine oil" },
                new Product { ProductName = "Chain Set - Yamaha Mio", Brand = "Yamaha", PartNumber = "2S3-22111-00", PartType = "Chain Set", ModelCompatibility = "Mio i 125, Mio Soul", CategoryId = catChain.CategoryId, SupplierId = supYamaha.SupplierId, Price = 650.00m, AverageCost = 400.00m, QuantityOnHand = 15, ReorderLevel = 5, StockStatus = "Available", Description = "Drive chain and sprocket set for Yamaha Mio" },
                new Product { ProductName = "Oil Filter - Suzuki Raider", Brand = "Suzuki", PartNumber = "16510-26H00", PartType = "Filters", ModelCompatibility = "Raider 150, Raider J", CategoryId = catFilter.CategoryId, SupplierId = supSuzuki.SupplierId, Price = 120.00m, AverageCost = 65.00m, QuantityOnHand = 30, ReorderLevel = 10, StockStatus = "Available", Description = "Genuine Suzuki oil filter" },
                new Product { ProductName = "Air Filter - Kawasaki Barako", Brand = "Kawasaki", PartNumber = "11013-1078", PartType = "Filters", ModelCompatibility = "Barako 175", CategoryId = catFilter.CategoryId, SupplierId = supKawasaki.SupplierId, Price = 150.00m, AverageCost = 85.00m, QuantityOnHand = 8, ReorderLevel = 5, StockStatus = "Available", Description = "Air filter element for Kawasaki Barako" },
                new Product { ProductName = "Rear Tire 90/80-17", Brand = "Various", PartNumber = "TIR-R90-17", PartType = "Tires", ModelCompatibility = "Universal 17-inch", CategoryId = catTire.CategoryId, SupplierId = supTire.SupplierId, Price = 1200.00m, AverageCost = 800.00m, QuantityOnHand = 12, ReorderLevel = 5, StockStatus = "Available", Description = "Rear motorcycle tire 90/80-17 tubeless" },
                new Product { ProductName = "Front Tire 80/90-17", Brand = "Various", PartNumber = "TIR-F80-17", PartType = "Tires", ModelCompatibility = "Universal 17-inch", CategoryId = catTire.CategoryId, SupplierId = supTire.SupplierId, Price = 1100.00m, AverageCost = 750.00m, QuantityOnHand = 0, ReorderLevel = 5, StockStatus = "Out of Stock", Description = "Front motorcycle tire 80/90-17 tubeless" },
                new Product { ProductName = "Spark Plug NGK CR7HSA", Brand = "NGK", PartNumber = "CR7HSA", PartType = "Engine Parts", ModelCompatibility = "Universal", CategoryId = catEngine.CategoryId, SupplierId = supMotoDepot.SupplierId, Price = 85.00m, AverageCost = 40.00m, QuantityOnHand = 50, ReorderLevel = 20, StockStatus = "Available", Description = "Standard NGK spark plug for most motorcycles" },
                new Product { ProductName = "Rectifier - Honda XRM", Brand = "Honda", PartNumber = "31600-KW9-003", PartType = "Electrical Parts", ModelCompatibility = "XRM 125, Wave 125", CategoryId = catElectrical.CategoryId, SupplierId = supHonda.SupplierId, Price = 450.00m, AverageCost = 280.00m, QuantityOnHand = 3, ReorderLevel = 5, StockStatus = "Low Stock", Description = "Voltage rectifier for Honda XRM and Wave" },
                new Product { ProductName = "Headlight Bulb H4 LED", Brand = "Various", PartNumber = "LED-H4", PartType = "Lighting & Signals", ModelCompatibility = "Universal", CategoryId = catLighting.CategoryId, SupplierId = supMotoDepot.SupplierId, Price = 250.00m, AverageCost = 130.00m, QuantityOnHand = 20, ReorderLevel = 10, StockStatus = "Available", Description = "H4 LED headlight bulb, white 6000K" },
                new Product { ProductName = "Shock Absorber - Suzuki Raider", Brand = "Suzuki", PartNumber = "54100-26H00", PartType = "Suspension", ModelCompatibility = "Raider 150", CategoryId = catSuspension.CategoryId, SupplierId = supSuzuki.SupplierId, Price = 1800.00m, AverageCost = 1200.00m, QuantityOnHand = 5, ReorderLevel = 3, StockStatus = "Available", Description = "Rear shock absorber for Suzuki Raider 150" },
                new Product { ProductName = "Side Mirror Set - Yamaha Mio", Brand = "Yamaha", PartNumber = "5SB-26290-00", PartType = "Body Parts", ModelCompatibility = "Mio i 125", CategoryId = catBody.CategoryId, SupplierId = supYamaha.SupplierId, Price = 320.00m, AverageCost = 180.00m, QuantityOnHand = 10, ReorderLevel = 5, StockStatus = "Available", Description = "Pair of side mirrors for Yamaha Mio i 125" },
                new Product { ProductName = "Brake Cable - Honda Wave", Brand = "Honda", PartNumber = "45450-K25-831", PartType = "Brake Parts", ModelCompatibility = "Wave 100, Wave 125", CategoryId = catBrake.CategoryId, SupplierId = supHonda.SupplierId, Price = 150.00m, AverageCost = 75.00m, QuantityOnHand = 18, ReorderLevel = 10, StockStatus = "Available", Description = "Front brake cable for Honda Wave series" },
                new Product { ProductName = "Crankcase Gasket Set", Brand = "Various", PartNumber = "GST-UNIV-001", PartType = "Gaskets", ModelCompatibility = "Universal 150cc", CategoryId = catFilter.CategoryId, SupplierId = supBearing.SupplierId, Price = 280.00m, AverageCost = 150.00m, QuantityOnHand = 7, ReorderLevel = 5, StockStatus = "Available", Description = "Complete crankcase gasket set for 150cc engines" }
            );

            db.SaveChanges();
        }
    }

    // Seed services (only if none exist)
    if (!db.Services.Any())
    {
        var svcCatEngine = db.Categories.FirstOrDefault(c => c.CategoryName == "Engine Parts");
        var svcCatBrake = db.Categories.FirstOrDefault(c => c.CategoryName == "Brake Parts");
        var svcCatElectrical = db.Categories.FirstOrDefault(c => c.CategoryName == "Electrical Parts");
        var svcCatOil = db.Categories.FirstOrDefault(c => c.CategoryName == "Oil & Lubricants");
        var svcCatChain = db.Categories.FirstOrDefault(c => c.CategoryName == "Chain & Sprocket");
        var svcCatTire = db.Categories.FirstOrDefault(c => c.CategoryName == "Tires & Wheels");
        var svcCatBody = db.Categories.FirstOrDefault(c => c.CategoryName == "Body & Accessories");

        var mechEngine = db.Mechanics.FirstOrDefault(m => m.MechanicName == "Andres Bonifacio");
        var mechElec = db.Mechanics.FirstOrDefault(m => m.MechanicName == "Jose Rizal");
        var mechBrake = db.Mechanics.FirstOrDefault(m => m.MechanicName == "Emilio Aguinaldo");
        var mechGeneral = db.Mechanics.FirstOrDefault(m => m.MechanicName == "Gabriela Silang");
        var mechTrans = db.Mechanics.FirstOrDefault(m => m.MechanicName == "Lapu-Lapu");

        if (svcCatEngine != null && svcCatBrake != null && svcCatElectrical != null && svcCatOil != null && svcCatChain != null && svcCatTire != null && svcCatBody != null && mechEngine != null && mechElec != null && mechBrake != null && mechGeneral != null && mechTrans != null)
        {
            db.Services.AddRange(
                new Service { ServiceName = "Change Oil (Labor Only)", CategoryId = svcCatOil.CategoryId, MechanicId = mechGeneral.MechanicId, ServicePrice = 150.00m },
                new Service { ServiceName = "Brake Pad Replacement", CategoryId = svcCatBrake.CategoryId, MechanicId = mechBrake.MechanicId, ServicePrice = 250.00m },
                new Service { ServiceName = "Chain & Sprocket Replacement", CategoryId = svcCatChain.CategoryId, MechanicId = mechTrans.MechanicId, ServicePrice = 350.00m },
                new Service { ServiceName = "Engine Tune-Up", CategoryId = svcCatEngine.CategoryId, MechanicId = mechEngine.MechanicId, ServicePrice = 500.00m },
                new Service { ServiceName = "Electrical System Check", CategoryId = svcCatElectrical.CategoryId, MechanicId = mechElec.MechanicId, ServicePrice = 200.00m },
                new Service { ServiceName = "Tire Replacement", CategoryId = svcCatTire.CategoryId, MechanicId = mechGeneral.MechanicId, ServicePrice = 150.00m },
                new Service { ServiceName = "Full Service (Change Oil + Tune-Up)", CategoryId = svcCatEngine.CategoryId, MechanicId = mechEngine.MechanicId, ServicePrice = 800.00m },
                new Service { ServiceName = "Lighting Repair / Replacement", CategoryId = svcCatBody.CategoryId, MechanicId = mechElec.MechanicId, ServicePrice = 180.00m }
            );

            db.SaveChanges();
        }
    }
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapHub<NotificationHub>("/notificationHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
