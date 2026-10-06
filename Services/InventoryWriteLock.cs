using KaijensonIventory_SalesMotorShopWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Services;

// Stock writers reserve products; PO edits and receipts also reserve the order.
internal static class InventoryWriteLock
{
    public static Task AcquireProductAsync(ApplicationDbContext context, int productId) =>
        AcquireAsync(context, $"Inventory:Product:{productId}");

    public static Task AcquireOrderAsync(ApplicationDbContext context, int orderId) =>
        AcquireAsync(context, $"Inventory:Order:{orderId}");

    public static Task AcquireCheckoutAsync(ApplicationDbContext context, string checkoutKey) =>
        AcquireAsync(context, $"Sale:Checkout:{checkoutKey}");

    public static Task AcquireSalesMonthAsync(ApplicationDbContext context, DateTime monthStart) =>
        AcquireAsync(context, $"Sale:Month:{monthStart.ToString("yyyyMM", System.Globalization.CultureInfo.InvariantCulture)}");

    private static Task AcquireAsync(ApplicationDbContext context, string resource)
    {
        return context.Database.ExecuteSqlInterpolatedAsync($@"
            DECLARE @lockResult int;
            EXEC @lockResult = sp_getapplock @Resource = {resource},
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000;
            IF @lockResult < 0 THROW 51002, 'The inventory item is busy. Please retry.', 1;");
    }
}
