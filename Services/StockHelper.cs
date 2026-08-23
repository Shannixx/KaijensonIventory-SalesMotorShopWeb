using System;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public static class StockHelper
    {
        public const int LowStockThreshold = 5;

        public static string GetStockStatus(int quantity)
        {
            if (quantity <= 0) return "Out of Stock";
            if (quantity < LowStockThreshold) return "Low Stock";
            return "Available";
        }
    }
}
