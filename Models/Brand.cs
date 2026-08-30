using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class Brand
    {
        [Key]
        public int BrandId { get; set; }

        [Required(ErrorMessage = "Brand name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Brand name must be between 2 and 100 characters.")]
        public string BrandName { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Brand description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Country of origin is required")]
        [StringLength(50)]
        public string CountryOrigin { get; set; } = string.Empty;



        // Supplier relationship (optional)
        public int? SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        // Audit fields
        public int? CreatedBy { get; set; }
        public Staff? CreatedByStaff { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

}
