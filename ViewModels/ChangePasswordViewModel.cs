using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class ChangePasswordViewModel
    {
        [Required, DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}
