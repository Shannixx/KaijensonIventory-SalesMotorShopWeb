using KaijensonIventory_SalesMotorShopWeb.Models;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public interface IBackupService
    {
        Task<DatabaseBackup> CreateBackupAsync(int staffId);
        Task<List<DatabaseBackup>> GetBackupHistoryAsync();
        Task<DatabaseBackup?> GetBackupAsync(int backupId);
        Task<bool> ValidateBackupAsync(int backupId);
        Task<bool> RestoreBackupAsync(int backupId, int staffId);
        Task<bool> DeleteBackupAsync(int backupId, int staffId);
        Task<string> GetDatabaseStatusAsync();
    }
}
