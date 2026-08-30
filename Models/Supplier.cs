using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class Supplier
    {
        [Key]
        public int SupplierId { get; set; }

        [Required, StringLength(150)]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = string.Empty;

        [Required, StringLength(150)]
        [Display(Name = "Contact Person")]
        public string? ContactPerson { get; set; }

        [Required, StringLength(30)]
        [Display(Name = "Contact Number")]
        public string? ContactNumber { get; set; }

        [Required, StringLength(250)]
        public string? Address { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
        public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
        [Required, EmailAddress, StringLength(150)]
        [Display(Name = "Email Address")]
        public string? EmailAddress { get; set; }

        [Required, StringLength(20)]
        public string Status { get; set; } = "Active";

        public DateTime? CreatedAt { get; set; }

        public int? CreatedBy { get; set; }
        public Staff? CreatedByStaff { get; set; }
    }
}
