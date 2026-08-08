using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class SalesTransaction
    {
        [Key]
        public int TransactionId { get; set; }

        [Required, MaxLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string CheckoutKey { get; set; } = string.Empty; // Idempotency token

        [Required, MaxLength(150)]
        public string CustomerName { get; set; } = string.Empty; // empty = walk‑in

        public DateTime TransactionDate { get; set; } = DateTime.Now;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Change { get; set; }

        public int StaffId { get; set; }
        public Staff Staff { get; set; } = null!;

        public ICollection<SalesItem> Items { get; set; } = new List<SalesItem>();
    }
}
