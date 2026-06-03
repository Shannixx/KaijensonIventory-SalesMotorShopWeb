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

        [StringLength(150)]
        [Display(Name = "Contact Person")]
        public string? ContactPerson { get; set; }

        [StringLength(30)]
        [Display(Name = "Contact Number")]
        public string? ContactNumber { get; set; }

        [StringLength(250)]
        public string? Address { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
        public ICollection<StockIn> StockIns { get; set; } = new List<StockIn>();
    }
}
