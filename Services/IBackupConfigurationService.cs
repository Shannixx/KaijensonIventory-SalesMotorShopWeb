using System.Threading.Tasks;
using KaijensonIventory_SalesMotorShopWeb.Models;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public interface IBackupConfigurationService
    {
        Task<BackupConfiguration> GetAsync();
        Task SaveAsync(BackupConfiguration config);
        Task SaveAdminSettingsAsync(BackupConfiguration config);
        Task SaveSchedulerStateAsync(DateTime? lastAutomaticRun, DateTime? nextAutomaticRun);
    }
}
