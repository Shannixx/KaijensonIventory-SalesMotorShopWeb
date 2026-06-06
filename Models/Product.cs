using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class Product
    {
        [Key]
        public int ProductId { get; set; }

        [Required, StringLength(150)]
        [Display(Name = "Product Name")]
        public string ProductName { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Brand")]
        public string? Brand { get; set; }

        [StringLength(100)]
        [Display(Name = "Model Compatibility")]
        public string? ModelCompatibility { get; set; }

        [StringLength(50)]
        [Display(Name = "Part Number")]
        public string? PartNumber { get; set; }

        [StringLength(50)]
        [Display(Name = "Part Type")]
        public string? PartType { get; set; }

        [StringLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Range(0, int.MaxValue)]
        [Display(Name = "Quantity On Hand")]
        public int QuantityOnHand { get; set; }

        [Range(0, int.MaxValue)]
        [Display(Name = "Reorder Level")]
        public int ReorderLevel { get; set; }

        [Range(1, 365)]
        [Display(Name = "Lead Time (Days)")]
        public int LeadTimeDays { get; set; } = 30;

        [Display(Name = "Auto-calculate Reorder Level")]
        public bool UseAutoReorder { get; set; }

        [Display(Name = "Last Recalculated")]
        public DateTime? LastRecalcDate { get; set; }

        [StringLength(30)]
        [Display(Name = "Stock Status")]
        public string StockStatus { get; set; } = "Available";

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Average Cost")]
        public decimal AverageCost { get; set; }

        [StringLength(500)]
        [Display(Name = "Image")]
        public string? ImagePath { get; set; }

        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        [Display(Name = "Supplier")]
        public int SupplierId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Category? Category { get; set; }
        public Supplier? Supplier { get; set; }
    }
}
