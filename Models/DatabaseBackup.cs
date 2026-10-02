using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class DatabaseBackup
    {
        public const string SuccessfulStatus = "Successful";
        public const string FailedStatus = "Failed";
        public const string InvalidStatus = "Invalid";
        public const string MissingStatus = "Missing";
        public const string DeletedStatus = "Deleted";
        public const string FullBackupType = "Full Database Backup";

        [Key]
        public int BackupId { get; set; }

        [Required, StringLength(260)]
        public string FileName { get; set; } = string.Empty;

        [Required, StringLength(260)]
        public string FilePath { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? CreatedBy { get; set; }
        public Staff? CreatedByStaff { get; set; }

        [Required, StringLength(50)]
        public string BackupType { get; set; } = FullBackupType;

        [StringLength(20)]
        public string Status { get; set; } = FailedStatus;

        [StringLength(500)]
        public string? Description { get; set; }
    }
}
