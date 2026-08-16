using Microsoft.AspNetCore.Mvc.Rendering;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class ProductEditViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public int CategoryId { get; set; }
        public int SupplierId { get; set; }
        public int QuantityOnHand { get; set; }
        public string? Description { get; set; }
        public string? ModelCompatibility { get; set; }
        public int? PurchaseOrderId { get; set; }
        public decimal? Price { get; set; }
        public List<SelectListItem> Categories { get; set; } = new();
        public List<SelectListItem> Suppliers { get; set; } = new();
        public List<SelectListItem> Brands { get; set; } = new();
        public List<SelectListItem> PurchaseOrders { get; set; } = new();
        public bool IsSerialized { get; set; }
    }
}
