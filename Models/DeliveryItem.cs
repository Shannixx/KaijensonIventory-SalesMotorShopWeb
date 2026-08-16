using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class DeliveryItem
    {
        [Key]
        public int DeliveryItemId { get; set; }

        public int DeliveryId { get; set; }
        public Delivery Delivery { get; set; } = null!;

        public int PurchaseOrderItemId { get; set; }
        public PurchaseOrderItem PurchaseOrderItem { get; set; } = null!;

        // Quantity received in this delivery event
        [Range(1, int.MaxValue)]
        public int ReceivedQuantity { get; set; }

        public DateTime ReceivedDate { get; set; } = DateTime.Now;
    }
}
