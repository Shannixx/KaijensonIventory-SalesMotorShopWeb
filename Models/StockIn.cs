using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class StockIn
    {
        [Key]
        public int StockInId { get; set; }

        public int ProductId { get; set; }

        public int SupplierId { get; set; }

        [Range(1, int.MaxValue)]
        [Display(Name = "Quantity Received")]
        public int QuantityReceived { get; set; }

        [Display(Name = "Delivery Date")]
        [DataType(DataType.Date)]
        public DateTime DeliveryDate { get; set; } = DateTime.Today;

        [StringLength(250)]
        public string? Remarks { get; set; }

        public int StaffId { get; set; }

        public Product? Product { get; set; }
        public Supplier? Supplier { get; set; }
        public Staff? Staff { get; set; }
    }
}
