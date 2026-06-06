namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public static class StockStatusService
    {
        public static string CalculateStockStatus(int quantityOnHand, int reorderLevel)
        {
            if (quantityOnHand <= 0)
                return "Out of Stock";
            if (quantityOnHand <= reorderLevel)
                return "Low Stock";
            return "Available";
        }

        public static void UpdateStockStatus(Models.Product product)
        {
            product.StockStatus = CalculateStockStatus(product.QuantityOnHand, product.ReorderLevel);
        }
    }
}
