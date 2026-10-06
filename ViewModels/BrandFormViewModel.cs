using System;
using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class BrandFormViewModel
    {
        public int BrandId { get; set; }

        [Required(ErrorMessage = "Brand name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Brand name must be between 2 and 100 characters.")]
        public string BrandName { get; set; } = string.Empty;

        public string? CreatedByName { get; set; }

        public DateTime CreatedAt { get; set; }

    }
}
