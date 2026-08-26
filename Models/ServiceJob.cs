using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    /// <summary>
    /// An actual service job / work order performed by a mechanic for a customer.
    /// Distinct from <see cref="Service"/>, which is the service type/catalog definition.
    /// </summary>
    public class ServiceJob
    {
        public const string StatusStillWorking = "Still Working";
        public const string StatusFinished = "Finished";
        public const string PaymentUnpaid = "Unpaid";
        public const string PaymentPaid = "Paid";

        public static readonly string[] AllStatuses =
            { StatusStillWorking, StatusFinished };

        public static readonly string[] AllPaymentStatuses =
            { PaymentUnpaid, PaymentPaid };

        [Key]
        public int ServiceJobId { get; set; }

        [Required, StringLength(50)]
        [Display(Name = "Service Job ID")]
        public string ServiceJobNumber { get; set; } = string.Empty; // e.g. SV-001

        [Display(Name = "Service")]
        public int ServiceId { get; set; }
        public Service? Service { get; set; }

        [Display(Name = "Mechanic")]
        public int MechanicId { get; set; }
        public Mechanic? Mechanic { get; set; }

        [Required, StringLength(150)]
        [Display(Name = "Customer Name")]
        public string CustomerName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required, StringLength(30)]
        public string Status { get; set; } = StatusStillWorking;

        [Required, StringLength(30)]
        [Display(Name = "Payment Status")]
        public string PaymentStatus { get; set; } = PaymentUnpaid;

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 999999.99)]
        [Display(Name = "Amount Received")]
        public decimal AmountReceived { get; set; }

        [Display(Name = "Service Date")]
        public DateTime ServiceDate { get; set; } = DateTime.Now;

        [Display(Name = "Completed Date")]
        public DateTime? CompletedDate { get; set; }

        [Display(Name = "Sales Transaction")]
        public int? SalesTransactionId { get; set; }
        public SalesTransaction? SalesTransaction { get; set; }

        [Display(Name = "Created At")]
                public DateTime CreatedAt { get; set; } = DateTime.Now;

                // The staff member (authenticated user) who created this service job.
                // Enables receipts to show "Processed By" and aligns with requirement Part 15.
                public int ProcessedByStaffId { get; set; }
                public Staff? ProcessedByStaff { get; set; }

                // Amount of change to return to the customer. Computed server‑side.
                [Column(TypeName = "decimal(18,2)")]
                [Display(Name = "Change Amount")]
                public decimal ChangeAmount { get; set; }

                // Token to protect against duplicate form submissions.
                [StringLength(64)]
                public string SubmissionToken { get; set; } = string.Empty;

                public ICollection<ServiceHistory> Histories { get; set; } = new List<ServiceHistory>();
            }
        }
