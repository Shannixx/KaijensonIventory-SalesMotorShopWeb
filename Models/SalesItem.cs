using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class SalesItem
    {
        [Key]
        public int SalesItemId { get; set; }

        public int TransactionId { get; set; }

        public int ProductId { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Unit Price")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        public SalesTransaction? Transaction { get; set; }
        public Product? Product { get; set; }
    }
}
