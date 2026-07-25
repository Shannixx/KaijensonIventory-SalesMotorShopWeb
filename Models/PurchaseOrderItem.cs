using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class PurchaseOrderItem
    {
        [Key]
        public int PurchaseOrderItemId { get; set; }

        public int PurchaseOrderId { get; set; }

        [Display(Name = "Product")]
        public int ProductId { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Range(0, 9999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        public PurchaseOrder? PurchaseOrder { get; set; }
        public Product? Product { get; set; }
    }
}
