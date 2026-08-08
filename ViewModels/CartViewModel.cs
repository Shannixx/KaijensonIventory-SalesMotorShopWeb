using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class CartViewModel
    {
        public List<SaleItemViewModel> Items { get; set; } = new();
        public string? CustomerName { get; set; }
        // No client‑side total – server will compute it
    }
}
