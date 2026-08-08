using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class Mechanic
    {
        [Key]
        public int MechanicId { get; set; }

        [Required, StringLength(150)]
        [Display(Name = "Mechanic Name")]
        public string MechanicName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string? Specialization { get; set; }

        [Required, StringLength(30)]
        [Display(Name = "Contact Number")]
        public string? ContactNumber { get; set; }

        [Required, StringLength(250)]
        public string? Address { get; set; }
    }
}
