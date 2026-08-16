using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class SerialUnit
    {
        [Key]
        public int SerialUnitId { get; set; }

        [Required]
        [StringLength(100)]
        public string SerialNumber { get; set; } = string.Empty;

        // The product this serial belongs to
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        // Status: Available, Sold, Returned etc.
        [Required, StringLength(20)]
        public string Status { get; set; } = "Available";

        // The sale this serial was assigned to (nullable until sold)
        public int? SalesTransactionId { get; set; }
        public SalesTransaction? SalesTransaction { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? SoldDate { get; set; }
    }
}
