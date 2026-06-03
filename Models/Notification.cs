using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }

        public int? ProductId { get; set; }

        [Required, StringLength(30)]
        [Display(Name = "Alert Type")]
        public string AlertType { get; set; } = string.Empty;

        [Required, StringLength(500)]
        public string Message { get; set; } = string.Empty;

        [Display(Name = "Is Read")]
        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Product? Product { get; set; }
    }
}
