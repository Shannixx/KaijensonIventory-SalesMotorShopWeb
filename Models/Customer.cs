using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class Customer
    {
        [Key]
        public int CustomerId { get; set; }

        [Required, StringLength(150)]
        [Display(Name = "Customer Name")]
        public string CustomerName { get; set; } = string.Empty;

        [StringLength(30)]
        [Display(Name = "Contact Number")]
        public string? ContactNumber { get; set; }

        [StringLength(100)]
        [DataType(DataType.EmailAddress)]
        public string? Email { get; set; }

        [StringLength(250)]
        public string? Address { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [Display(Name = "Walk-in Customer")]
        public bool IsWalkInCustomer { get; set; }

        [Display(Name = "Lifetime Spend")]
        [DataType(DataType.Currency)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18,2)")]
        public decimal TotalPurchases { get; set; }

        [Display(Name = "Last Purchase Date")]
        [DataType(DataType.Date)]
        public DateTime? LastPurchaseDate { get; set; }

        [Display(Name = "Reward Points")]
        public int RewardPoints { get; set; }

        public string AvailableReward
        {
            get
            {
                if (RewardPoints >= 1000) return "Free Basic Service";
                if (RewardPoints >= 500) return "Free Engine Oil";
                if (RewardPoints >= 300) return "5% Discount";
                if (RewardPoints >= 100) return "2% Discount";
                return "No reward available yet";
            }
        }

        public int PointsForNextReward
        {
            get
            {
                if (RewardPoints < 100) return 100 - RewardPoints;
                if (RewardPoints < 300) return 300 - RewardPoints;
                if (RewardPoints < 500) return 500 - RewardPoints;
                if (RewardPoints < 1000) return 1000 - RewardPoints;
                return 0;
            }
        }

        public string NextRewardName
        {
            get
            {
                if (RewardPoints < 100) return "2% Discount";
                if (RewardPoints < 300) return "5% Discount";
                if (RewardPoints < 500) return "Free Engine Oil";
                if (RewardPoints < 1000) return "Free Basic Service";
                return "Max Tier Reached";
            }
        }

        [Display(Name = "Loyalty Tier")]
        public string LoyaltyTier
        {
            get
            {
                if (TotalPurchases >= 50000) return "Platinum";
                if (TotalPurchases >= 20000) return "Gold";
                if (TotalPurchases >= 10000) return "Silver";
                if (TotalPurchases >= 3000) return "Bronze";
                return "Regular";
            }
        }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<SalesTransaction> SalesTransactions { get; set; } = new List<SalesTransaction>();
        public ICollection<ServiceTransaction> ServiceTransactions { get; set; } = new List<ServiceTransaction>();
        public ICollection<RewardRedemption> RewardRedemptions { get; set; } = new List<RewardRedemption>();
    }
}
