using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class InventoryTransaction
    {
        [Key]
        public int InventoryTransactionId { get; set; }

        public int ProductId { get; set; }

        [Required, StringLength(30)]
        [Display(Name = "Transaction Type")]
        public string TransactionType { get; set; } = string.Empty;

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Unit Cost")]
        public decimal UnitCost { get; set; }

        public int? ReferenceId { get; set; }

        [StringLength(50)]
        public string? ReferenceType { get; set; }

        public int? StaffId { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Remarks { get; set; }

        public Product? Product { get; set; }
        public Staff? Staff { get; set; }
    }
}
