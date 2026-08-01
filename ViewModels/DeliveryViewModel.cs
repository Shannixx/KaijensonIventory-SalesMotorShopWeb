using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class DeliveryViewModel
    {
        public int PurchaseOrderId { get; set; }

        [Display(Name = "PO Number")]
        public string? PurchaseOrderNumber { get; set; }

        [Display(Name = "Status")]
        public string? Status { get; set; }

        [Display(Name = "Supplier")]
        public string? SupplierName { get; set; }

        [Display(Name = "Order Date")]
        public DateTime OrderDate { get; set; }

        [Display(Name = "Delivered Date")]
        public DateTime? DeliveredDate { get; set; }

        [Display(Name = "Created By")]
        public string? CreatedByName { get; set; }

        public List<DeliveryItemViewModel> Items { get; set; } = new();
    }

    public class DeliveryItemViewModel
    {
        [Display(Name = "Product")]
        public string? ProductName { get; set; }

        [Display(Name = "Brand")]
        public string? Brand { get; set; }

        [Display(Name = "Part Type")]
        public string? PartType { get; set; }

        [Display(Name = "Quantity")]
        public int Quantity { get; set; }
    }
}
