using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class BrandFormViewModel
    {
        public int BrandId { get; set; }

        [Required(ErrorMessage = "Brand name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Brand name must be between 2 and 100 characters.")]
        public string BrandName { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Brand description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Country of origin is required.")]
        [StringLength(50, ErrorMessage = "Country of origin cannot exceed 50 characters.")]
        public string CountryOrigin { get; set; } = string.Empty;



        public int? SupplierId { get; set; }

        public string? CreatedByName { get; set; }

        public DateTime CreatedAt { get; set; }

        public IEnumerable<SelectListItem> Suppliers { get; set; } = new List<SelectListItem>();
    }
}
