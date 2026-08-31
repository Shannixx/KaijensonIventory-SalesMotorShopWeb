using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class ResetPasswordViewModel
    {
        [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm New Password")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}