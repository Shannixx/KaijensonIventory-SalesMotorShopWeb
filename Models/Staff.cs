using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class Staff
    {
        [Key]
        public int StaffId { get; set; }

        [Required, StringLength(100)]
        [Display(Name = "Staff Name")]
        public string StaffName { get; set; } = string.Empty;

        [Required, StringLength(50)]
        [Display(Name = "Username")]
        public string UserName { get; set; } = string.Empty;

        [Required, StringLength(256)]
        public string PasswordHash { get; set; } = string.Empty;

        [StringLength(30)]
        [Display(Name = "Contact Number")]
        public string? ContactNumber { get; set; }

        [StringLength(250)]
        public string? Address { get; set; }

        [Required, StringLength(20)]
        public string Role { get; set; } = "Manager";
    }
}
