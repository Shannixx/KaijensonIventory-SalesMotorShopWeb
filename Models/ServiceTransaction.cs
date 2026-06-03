using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class ServiceTransaction
    {
        [Key]
        public int ServiceTxnId { get; set; }

        [Required, StringLength(150)]
        [Display(Name = "Customer Name")]
        public string CustomerName { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Make { get; set; }

        [StringLength(50)]
        public string? Model { get; set; }

        public int? Year { get; set; }

        [StringLength(20)]
        [Display(Name = "Plate Number")]
        public string? PlateNumber { get; set; }

        [StringLength(500)]
        [Display(Name = "Service Description")]
        public string? ServiceDescription { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Service Fee")]
        public decimal ServiceFee { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        [Required, StringLength(20)]
        public string Status { get; set; } = "Pending";

        public int MechanicId { get; set; }

        public int StaffId { get; set; }

        public Mechanic? Mechanic { get; set; }
        public Staff? Staff { get; set; }
        public ICollection<ServicePartUsed> PartsUsed { get; set; } = new List<ServicePartUsed>();
    }
}
