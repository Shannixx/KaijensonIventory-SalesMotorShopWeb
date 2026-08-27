using System.Collections.Generic;
using System.Threading.Tasks;
using KaijensonIventory_SalesMotorShopWeb.Models;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public interface IBackupService
    {
        Task<DatabaseBackup> CreateBackupAsync(int staffId);
        Task<DatabaseBackup> CreatePreRestoreBackupAsync(int staffId);
        Task<DatabaseBackup> GetBackupAsync(int backupId);
        Task<List<DatabaseBackup>> GetBackupHistoryAsync();
        Task<bool> ValidateBackupAsync(int backupId);
        Task<bool> RestoreBackupAsync(int backupId, int staffId);
        Task<bool> VerifyDatabaseAsync();
        Task<string> GetDatabaseStatusAsync();
        Task<DatabaseBackup> CreateAutomaticBackupAsync();
    }
}
