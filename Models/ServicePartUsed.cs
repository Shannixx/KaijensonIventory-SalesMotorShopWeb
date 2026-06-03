using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class ServicePartUsed
    {
        [Key]
        public int PartUsedId { get; set; }

        public int ServiceTxnId { get; set; }

        public int ProductId { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        public ServiceTransaction? ServiceTransaction { get; set; }
        public Product? Product { get; set; }
    }
}
