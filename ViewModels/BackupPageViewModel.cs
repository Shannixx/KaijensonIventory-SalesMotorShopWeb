using System.Collections.Generic;
using KaijensonIventory_SalesMotorShopWeb.Models;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class BackupPageViewModel
    {
        public List<DatabaseBackup> History { get; set; } = new();
        public BackupConfiguration Settings { get; set; } = new();
        public DatabaseBackup? LastSuccessfulBackup { get; set; }
        public int AvailableBackupCount { get; set; }
        public DatabaseBackup? LatestBackup { get; set; }
        public DatabaseBackup? LatestAutomaticBackup { get; set; }
        public DateTime? NextScheduledBackup { get; set; }
        public string? DatabaseStatus { get; set; }
    }
}
