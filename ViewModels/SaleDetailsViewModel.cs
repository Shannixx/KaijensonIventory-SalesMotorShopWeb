using System.Collections.Generic;
using KaijensonIventory_SalesMotorShopWeb.Models;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class SaleDetailsViewModel
    {
        public SalesTransaction Transaction { get; set; } = null!;
        public IEnumerable<SalesItem> Items { get; set; } = System.Array.Empty<SalesItem>();
        public Dictionary<int, List<string>> SerialNumbersByProduct { get; set; } = new();
    }
}