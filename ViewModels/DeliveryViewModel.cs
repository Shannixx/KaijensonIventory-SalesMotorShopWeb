using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class DeliveryViewModel
    {
        public int DeliveryId { get; set; }
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

        // New delivery history entries (one per receiving event)
        public List<DeliveryHistoryViewModel> History { get; set; } = new();
    }

    public class DeliveryItemViewModel
    {
        [Display(Name = "Product")]
        public string? ProductName { get; set; }

        [Display(Name = "Brand")]
        public string? Brand { get; set; }

        [Display(Name = "Category")]
        public string? Category { get; set; }

        [Display(Name = "Quantity")]
        public int Quantity { get; set; }

        // Cumulative received quantity for this PO item
        public int ReceivedQuantity { get; set; }

        // Remaining quantity to receive
        public int Remaining => Quantity - ReceivedQuantity;

        // Identifier for PO item (needed for receiving)
        public int PurchaseOrderItemId { get; set; }
    }

    public class DeliveryHistoryViewModel
    {
        public string? PurchaseOrderNumber { get; set; }
        public DateTime OrderDate { get; set; }
        public string? ProductName { get; set; }
        public DateTime DateReceived { get; set; }
        public int QuantityReceived { get; set; }
        public string? StatusAfter { get; set; }
    }
}
