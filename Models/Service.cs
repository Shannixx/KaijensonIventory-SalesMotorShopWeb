using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class Service
    {
        [Key]
        public int ServiceId { get; set; }

        [Required, StringLength(150)]
        [Display(Name = "Service Name")]
        public string ServiceName { get; set; } = string.Empty;

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Service Price")]
        public decimal ServicePrice { get; set; }

        // Optional: a service is created with just a name and price.
        // Categories remain required for products only.
        [Display(Name = "Category")]
        public int? CategoryId { get; set; }

        public Category? Category { get; set; }
    }
}
