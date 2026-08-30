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
        public string Specialization { get; set; } = string.Empty;

        [Required, StringLength(30)]
        [Display(Name = "Contact Number")]
        public string ContactNumber { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(150)]
        [Display(Name = "Email Address")]
        public string EmailAddress { get; set; } = string.Empty;

        [Required, StringLength(250)]
        public string Address { get; set; } = string.Empty;

        [Range(0, 60)]
        public int? YearsOfExperience { get; set; }

        [Required, StringLength(20)]
        public string Status { get; set; } = "Active";

        [Required, StringLength(20)]
        [Display(Name = "Work Status")]
        public string WorkStatus { get; set; } = "Available";

        public DateTime? DateHired { get; set; }

        public int? HiredBy { get; set; }
        public Staff? HiredByStaff { get; set; }
    }
}
