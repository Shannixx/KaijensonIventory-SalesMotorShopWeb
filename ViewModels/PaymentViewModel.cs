using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class PaymentViewModel
    {
        // Serial numbers for serialized products, keyed by product ID
        public Dictionary<int, List<string>> SerialNumbers { get; set; } = new();
        [Required]
        public string CheckoutKey { get; set; } = string.Empty;

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Amount paid must be non‑negative.")]
        public decimal AmountPaid { get; set; }
    }
}
