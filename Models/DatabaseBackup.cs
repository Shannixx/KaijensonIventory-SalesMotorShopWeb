using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class DatabaseBackup
    {
        [Key]
        public int BackupId { get; set; }

        [Required, MaxLength(260)]
        public string FileName { get; set; } = string.Empty;

        [Required, MaxLength(260)]
        public string FilePath { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Reference to Staff who created
        public int? CreatedBy { get; set; }
        public Staff? CreatedByStaff { get; set; }

        [Required, MaxLength(50)]
        public string BackupType { get; set; } = string.Empty; // Full Database Backup / Pre-Restore Safety Backup

        [MaxLength(20)]
        public string Status { get; set; } = string.Empty; // Successful, Failed, Invalid, Missing

        [MaxLength(500)]
        public string? Description { get; set; }
    }
}
