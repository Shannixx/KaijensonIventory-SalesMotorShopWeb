using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class ActivityLog
    {
        [Key]
        public int ActivityLogId { get; set; }

        [Required, StringLength(100)]
        public string Action { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string Module { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public int? StaffId { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;

        public Staff? Staff { get; set; }
    }
}
