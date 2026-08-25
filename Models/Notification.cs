using System;
using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }

        public int? ProductId { get; set; }
        public Product? Product { get; set; }

        [Required, MaxLength(30)]
        public string AlertType { get; set; } = string.Empty; // "LowStock", "OutOfStock", or "Reorder"

        [Required, MaxLength(500)]
        public string Message { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
