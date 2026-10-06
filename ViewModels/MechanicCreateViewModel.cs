using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    // Only the fields shown on the Create Mechanic form are validated as input.
    public class MechanicCreateViewModel
    {
        [Required, StringLength(150)]
        [Display(Name = "Mechanic Name")]
        public string MechanicName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Specialization { get; set; } = string.Empty;

        [Required, StringLength(30)]
        [Display(Name = "Contact Number")]
        public string ContactNumber { get; set; } = string.Empty;
    }
}
