using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    /// <summary>
    /// A chronological work/history entry for a service job.
    /// Existing rows are never overwritten; new work is appended as new rows.
    /// </summary>
    public class ServiceHistory
    {
        [Key]
        public int ServiceHistoryId { get; set; }

        public int ServiceJobId { get; set; }
        public ServiceJob? ServiceJob { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Work Date")]
        public DateTime WorkDate { get; set; } = DateTime.Now;

        [Required, StringLength(300)]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 999999.99)]
        [Display(Name = "Amount Received")]
        public decimal AmountReceived { get; set; }

        [StringLength(30)]
        [Display(Name = "Payment Status")]
        public string PaymentStatus { get; set; } = ServiceJob.PaymentUnpaid;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
