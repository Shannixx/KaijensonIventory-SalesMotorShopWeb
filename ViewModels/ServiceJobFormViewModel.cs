using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    // Only selection IDs and customer/payment input are accepted from a job form.
    public class ServiceJobFormViewModel : IValidatableObject
    {
        public int ServiceJobId { get; set; }

        [Range(1, int.MaxValue), Display(Name = "Base Service")]
        public int ServiceId { get; set; }

        [Range(1, int.MaxValue), Display(Name = "Mechanic")]
        public int MechanicId { get; set; }

        [Required, StringLength(150), Display(Name = "Customer Name")]
        public string CustomerName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Range(typeof(decimal), "0", "999999.99"), Display(Name = "Amount Received")]
        public decimal AmountReceived { get; set; }

        [StringLength(64)]
        public string? SubmissionToken { get; set; }

        public List<int> SelectedAddOnIds { get; set; } = new();

        [BindNever, ValidateNever]
        public string ServiceJobNumber { get; set; } = string.Empty;
        [BindNever, ValidateNever]
        public string Status { get; set; } = string.Empty;
        [BindNever, ValidateNever]
        public DateTime? CompletedDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (decimal.Round(AmountReceived, 2) != AmountReceived)
                yield return new ValidationResult("Amount received must have at most two decimal places.", new[] { nameof(AmountReceived) });
        }
    }
}
