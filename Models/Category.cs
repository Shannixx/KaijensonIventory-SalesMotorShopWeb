using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required, StringLength(100)]
        [Display(Name = "Category Name")]
        public string CategoryName { get; set; } = string.Empty;

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
