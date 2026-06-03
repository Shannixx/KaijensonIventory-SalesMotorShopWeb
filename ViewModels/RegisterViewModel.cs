using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class RegisterViewModel
    {
        [Required, StringLength(100)]
        [Display(Name = "Staff Name")]
        public string StaffName { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare("Password", ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
