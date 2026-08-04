using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class Delivery
    {
        [Key]
        public int DeliveryId { get; set; }

        [Required]
        public int PurchaseOrderId { get; set; }

        [Required, StringLength(20)]
        public string Status { get; set; } = "Pending";

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? DeliveredDate { get; set; }

        // Navigation property (optional)
        public PurchaseOrder? PurchaseOrder { get; set; }
    }
}
