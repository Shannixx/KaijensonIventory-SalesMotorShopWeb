using System;
using KaijensonIventory_SalesMotorShopWeb.Services;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class CustomerPurchaseOrderViewModel
    {
        public ProductListResult Products { get; set; } = null!;
        public CartViewModel Cart { get; set; } = new();
        public string? SearchQuery { get; set; }
    }
}
