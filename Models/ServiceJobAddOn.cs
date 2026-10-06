using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class ServiceJobAddOn
    {
        public int ServiceJobId { get; set; }
        public ServiceJob? ServiceJob { get; set; }

        public int AddOnServiceId { get; set; }
        public Service? AddOnService { get; set; }

        [Required, StringLength(150)]
        public string AddOnNameSnapshot { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceSnapshot { get; set; }
    }
}
