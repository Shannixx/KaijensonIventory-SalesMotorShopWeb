namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class InventoryHealthViewModel
    {
        public List<ProductHealthInfo> Products { get; set; } = new();
        public int TotalChecked { get; set; }
        public int DiscrepancyCount { get; set; }
        public bool RebuildSuccess { get; set; }
        public string? RebuildMessage { get; set; }
        public int TotalTransactions { get; set; }
        public int OpeningBalanceCount { get; set; }
        public int StockInCount { get; set; }
        public int SaleCount { get; set; }
        public int SaleReversalCount { get; set; }
        public int ServiceUseCount { get; set; }
        public int POReceivingCount { get; set; }
        public int AdjustmentCount { get; set; }
    }

    public class ProductHealthInfo
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int CalculatedStock { get; set; }
        public int Difference => CurrentStock - CalculatedStock;
        public bool HasDiscrepancy => CurrentStock != CalculatedStock;
        public string Status => HasDiscrepancy ? "Mismatch" : "OK";
        public int TransactionCount { get; set; }
        public decimal AverageCost { get; set; }
        public int ReorderLevel { get; set; }
    }
}
