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

        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        [Display(Name = "Mechanic")]
        public int MechanicId { get; set; }

        public Category? Category { get; set; }
        public Mechanic? Mechanic { get; set; }
    }
}
