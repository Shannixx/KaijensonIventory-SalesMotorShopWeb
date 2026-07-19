# Kaijenson Inventory - Sales Motor Shop Web

## Database connection

The connection string is **not committed** to version control. The base
`appsettings.json` contains an empty placeholder. You must provide a valid
connection string via one of the following methods:

### .NET User Secrets (recommended for local development)

```powershell
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=YOUR_SERVER\SQLEXPRESS;Database=KaijensonInventorySalesDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

Replace `YOUR_SERVER\SQLEXPRESS` with your local SQL Server instance name.

### Environment variable

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=YOUR_SERVER\SQLEXPRESS;Database=KaijensonInventorySalesDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

Or set it at the system or user level through your operating system settings.

---

## Migrations — safety notice

**The application no longer applies migrations automatically on startup.**

This is a deliberate safety measure to prevent accidental schema changes or
data loss.

### How to apply migrations in Development

If you need to apply pending migrations during local development:

1. Set `"ApplyMigrationsOnStartup": true` in `appsettings.Development.json`.
2. Ensure `ASPNETCORE_ENVIRONMENT` is set to `Development`.
3. Start the application.

Example `appsettings.Development.json`:

```json
{
  "ApplyMigrationsOnStartup": true,
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

This flag is **off by default** and is **ignored outside Development**.
Never enable it in Production.

### How to apply migrations via an offline script (recommended for all environments)

Generate an idempotent SQL script and review it before applying:

```powershell
dotnet ef migrations script --idempotent --output migrate.sql
```

Inspect `migrate.sql` for any destructive commands (`DROP TABLE`,
`DROP COLUMN`, `DELETE`, `ALTER COLUMN`). Apply only after review.

```powershell
sqlcmd -S YOUR_SERVER\SQLEXPRESS -d KaijensonInventorySalesDb -i migrate.sql
```

---

## ⚠️ Critical warning — destructive migrations exist

The migration chain currently contains migrations that **drop business tables**
(`20260718083528_RemoveSalesCustomerModules` and
`20260719045420_AddBrandsTable`). These include:

- `Backups`
- `Customers`
- `SalesTransactions`
- `SalesItems`
- `RewardRedemptions`
- `InventoryTransactions`
- `Notifications`
- `PurchaseOrders`
- `PurchaseOrderItems`
- `ServiceTransactions`
- `ServicePartsUsed`
- `StockIns`

**Do not apply these migrations to any database that contains real data.**

An existing database must be backed up before any schema work:

```sql
BACKUP DATABASE [KaijensonInventorySalesDb] TO DISK = 'C:\Backups\KaijensonInventorySalesDb_FULL_YYYYMMDD.bak';
```

---

## Migration commands (informational only — do not execute migration-update)

```powershell
# List pending migrations
dotnet ef migrations list --context ApplicationDbContext

# Generate an offline script for review
dotnet ef migrations script --idempotent --output migrate.sql

# NEVER run this without reviewing the generated script first:
# dotnet ef database update --context ApplicationDbContext
```
