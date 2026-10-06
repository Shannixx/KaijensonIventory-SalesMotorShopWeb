using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class ServiceFormViewModel : IValidatableObject
    {
        public int ServiceId { get; set; }

        [Required(ErrorMessage = "Service name is required.")]
        [StringLength(150, ErrorMessage = "Service name cannot exceed 150 characters.")]
        [Display(Name = "Service Name")]
        public string ServiceName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Price is required.")]
        [Range(typeof(decimal), "0", "999999.99", ErrorMessage = "Price must be between 0 and 999999.99.")]
        [Display(Name = "Service Price")]
        public decimal? ServicePrice { get; set; }

        [Required(ErrorMessage = "Select a service type.")]
        [Display(Name = "Service Type")]
        public bool? IsAddOn { get; set; }

        [Required(ErrorMessage = "Status is required.")]
        [RegularExpression("^(Active|Inactive)$", ErrorMessage = "Status must be Active or Inactive.")]
        public string Status { get; set; } = "Active";

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ServicePrice is decimal price)
            {
                if (decimal.Round(price, 2) != price)
                {
                    yield return new ValidationResult("Price must have no more than two decimal places.", new[] { nameof(ServicePrice) });
                }

                if (IsAddOn == true && price <= 0)
                {
                    yield return new ValidationResult("An add-on price must be greater than zero.", new[] { nameof(ServicePrice) });
                }
            }
        }
    }
}
