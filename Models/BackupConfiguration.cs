using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class BackupConfiguration
    {
        [Key]
        public int Id { get; set; } // singleton row

        public bool Enabled { get; set; } = false;

        // Daily, Weekly, Monthly
        [MaxLength(20)]
        public string Frequency { get; set; } = "Daily";

        public int Hour { get; set; } = 0;
        public int Minute { get; set; } = 0;

        // For Weekly: 0=Sunday .. 6=Saturday
        public int? DayOfWeek { get; set; }

        // For Monthly: 1-31
        public int? DayOfMonth { get; set; }

        public int RetentionCount { get; set; } = 7;

        public string? BackupDirectory { get; set; }

        // Tracks the last automatic backup occurrence that was processed
        public DateTime? LastAutomaticRun { get; set; }
        public DateTime? NextAutomaticRun { get; set; }
    }
}
