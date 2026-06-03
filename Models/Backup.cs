using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class Backup
    {
        [Key]
        public int BackupId { get; set; }

        [Required, StringLength(30)]
        [Display(Name = "Backup Type")]
        public string BackupType { get; set; } = string.Empty;

        [Required, StringLength(500)]
        [Display(Name = "Backup File")]
        public string BackupFile { get; set; } = string.Empty;

        public DateTime BackupDate { get; set; } = DateTime.Now;

        [Required, StringLength(20)]
        public string Status { get; set; } = string.Empty;
    }
}
