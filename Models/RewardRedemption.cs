using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class RewardRedemption
    {
        [Key]
        public int RewardRedemptionId { get; set; }

        public int CustomerId { get; set; }

        [Required, StringLength(50)]
        public string RewardName { get; set; } = string.Empty;

        public int PointsCost { get; set; }

        [StringLength(200)]
        public string? Description { get; set; }

        public DateTime RedeemedAt { get; set; } = DateTime.Now;

        public int? RedeemedByStaffId { get; set; }

        public int? SalesTransactionId { get; set; }

        [ForeignKey("CustomerId")]
        public Customer? Customer { get; set; }

        [ForeignKey("RedeemedByStaffId")]
        public Staff? RedeemedByStaff { get; set; }

        [ForeignKey("SalesTransactionId")]
        public SalesTransaction? SalesTransaction { get; set; }
    }
}
