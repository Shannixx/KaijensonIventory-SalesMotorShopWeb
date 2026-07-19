using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class Brand
    {
        [Key]
        public int BrandId { get; set; }

        [Required(ErrorMessage = "Brand name is required")]
        [StringLength(100)]
        public string BrandName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Country of origin is required")]
        [StringLength(50)]
        public string CountryOrigin { get; set; } = string.Empty;

        [Required]
        public string Status { get; set; } = "Active";
    }
}
