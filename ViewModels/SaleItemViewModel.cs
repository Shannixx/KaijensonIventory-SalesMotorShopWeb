using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class SaleItemViewModel
    {
        public int ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty; // display only
        public decimal UnitPrice { get; set; } // display only
        public int Quantity { get; set; }
        public bool IsSerialized { get; set; }
        public decimal Subtotal { get; set; } // display only
    }
}
