using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class Customer
    {
        [Key]
        public int CustomerId { get; set; }

        [Required, StringLength(150)]
        [Display(Name = "Customer Name")]
        public string CustomerName { get; set; } = string.Empty;

        [StringLength(30)]
        [Display(Name = "Contact Number")]
        public string? ContactNumber { get; set; }

        [StringLength(100)]
        [DataType(DataType.EmailAddress)]
        public string? Email { get; set; }

        [StringLength(250)]
        public string? Address { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [Display(Name = "Total Purchases")]
        [DataType(DataType.Currency)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18,2)")]
        public decimal TotalPurchases { get; set; }

        [Display(Name = "Last Purchase Date")]
        [DataType(DataType.Date)]
        public DateTime? LastPurchaseDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<SalesTransaction> SalesTransactions { get; set; } = new List<SalesTransaction>();
        public ICollection<ServiceTransaction> ServiceTransactions { get; set; } = new List<ServiceTransaction>();
    }
}
