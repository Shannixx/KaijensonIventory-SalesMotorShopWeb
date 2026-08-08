using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class PaymentViewModel
    {
        [Required]
        public string CheckoutKey { get; set; } = string.Empty;

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Amount paid must be non‑negative.")]
        public decimal AmountPaid { get; set; }
    }
}
