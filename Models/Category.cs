using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Category name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Category name must be between 2 and 100 characters.")]
        public string CategoryName { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        [Display(Name = "Created By")]
        public int? CreatedBy { get; set; }

        [ForeignKey(nameof(CreatedBy))]
        public Staff? CreatedByStaff { get; set; }

        [Display(Name = "Date Created")]
        public DateTime CreatedAt { get; set; }
    }
}
