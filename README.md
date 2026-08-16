# Kaijenson Inventory Sales Motor Shop

## Setup for Team Members

This guide walks a new team member through cloning the repository and getting the application running locally.

---

### Required Environment

- **.NET 8 SDK**
- **SQL Server** (default instance or Express)
- **Visual Studio 2022** (recommended)
- **Entity Framework Core Tools** (Package Manager Console or CLI)

The project targets SQL Server. The default connection string is:

```
Server=.;
Database=KaijensonInventorySalesDb;
Trusted_Connection=True;
TrustServerCertificate=True;
MultipleActiveResultSets=true
```

`Server=.` refers to the local default SQL Server instance.

---

### SQL Server Instance Alternative

If you installed **SQL Server Express** using the named instance `SQLEXPRESS`, change the connection string to:

```
Server=.\SQLEXPRESS;
Database=KaijensonInventorySalesDb;
Trusted_Connection=True;
TrustServerCertificate=True;
MultipleActiveResultSets=true
```

No changes are needed when the default instance is available.

---

### Clone and Setup Workflow

1. **Clone the repository**
   ```
   git clone <repository-url>
   ```
2. **Open the solution** in Visual Studio.
3. **Restore NuGet packages** (Visual Studio does this automatically on load).
4. **Make sure SQL Server is running**.
5. **Verify the connection string** in `appsettings.json` if needed (see above).
6. Open **Package Manager Console** (`Tools → NuGet Package Manager → Package Manager Console`).
7. Set the project as **Default Project** in the console dropdown.
8. Run the command:
   ```
   Update-Database
   ```
9. Wait for the `InitialCreate` migration to finish.
10. **Run the application** (F5 or Ctrl+F5).

---

### Package Manager Console Command

- **Command:** `Update-Database`
- **Equivalent CLI:** `dotnet ef database update`

---

### Database Name

The database created by EF Core is named **KaijensonInventorySalesDb**. Do **not** create tables manually; EF Core will generate the complete schema from the `20260816163134_InitialCreate` migration.

---

### New Baseline Migration

The project now uses a single **baseline migration** (`20260816163134_InitialCreate`) that represents the current full schema, including tables for:
- Products, Categories, Brands, Suppliers
- Mechanics, Staff
- Purchase Orders, Deliveries, Delivery Items
- Sales, Serial Tracking
- Activity Logs, Notifications
- and all other current application tables.

---

### Important – Old Local Database

If you previously worked with an older version of the project that had a different migration history, your local database may not match the new baseline migration. For a local/test database with no important data, the recommended solution is to **delete and recreate** the database and then run `Update-Database` again. **Do not delete a database that contains important project data.**

---

### Connection String Troubleshooting

If SQL Server cannot be found:
1. Open **Windows Services**.
2. Locate **SQL Server (MSSQLSERVER)** or **SQL Server (SQLEXPRESS)**.
3. Ensure the service is **Running**.
4. Match the `Server=` part of the connection string to the installed instance.

- **Default instance:** `Server=.`
- **SQL Server Express:** `Server=.\SQLEXPRESS`

---

### Run the Application

Typical workflow after setup:
1. `Update-Database`
2. **Build** the solution.
3. **Run** the application (press **F5** or **Ctrl+F5** in Visual Studio).

---

### Do Not Change the Migrations

Do **not** manually delete or edit the baseline migration. If the database model changes in the future, create a new migration using the standard EF Core workflow.
