using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class BackupController : BaseController
    {
        private readonly IBackupService _backupService;
        private readonly ILogger<BackupController> _logger;

        public BackupController(IBackupService backupService, ILogger<BackupController> logger)
        {
            _backupService = backupService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var accessCheck = RedirectIfNotAdmin();
            if (accessCheck != null) return accessCheck;

            var backups = new List<DatabaseBackup>();
            ViewBag.BackupHistoryAvailable = true;

            try
            {
                backups = await _backupService.GetBackupHistoryAsync();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not load database backup history.");
                ViewBag.BackupHistoryAvailable = false;
                TempData["ErrorMessage"] = "Backup history could not be loaded. Database connectivity is checked separately; review the application log and migration state.";
            }

            try
            {
                ViewBag.DatabaseStatus = await _backupService.GetDatabaseStatusAsync();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not load database connectivity status for the backup page.");
                ViewBag.DatabaseStatus = "Unavailable";
            }

            ViewBag.LatestBackup = backups.FirstOrDefault();
            return View(backups);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BackupNow()
        {
            var accessCheck = RedirectIfNotAdmin();
            if (accessCheck != null) return accessCheck;

            try
            {
                var backup = await _backupService.CreateBackupAsync(GetCurrentStaffId());
                if (backup.Status == DatabaseBackup.SuccessfulStatus)
                    TempData["SuccessMessage"] = "Database backup created successfully.";
                else
                    TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(backup.Description)
                        ? "Database backup failed. Review the application log for details."
                        : $"Database backup failed: {backup.Description}";
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Database backup request failed.");
                TempData["ErrorMessage"] = $"Database backup could not be completed: {exception.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int backupId)
        {
            var accessCheck = RedirectIfNotAdmin();
            if (accessCheck != null) return accessCheck;

            try
            {
                if (await _backupService.RestoreBackupAsync(backupId, GetCurrentStaffId()))
                    TempData["SuccessMessage"] = "Database restored and verified successfully.";
                else
                    TempData["ErrorMessage"] = "Database restore failed. The selected backup was not restored successfully.";
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Database restore request failed for backup ID {BackupId}.", backupId);
                TempData["ErrorMessage"] = "Database restore failed. The selected backup was not restored successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int backupId)
        {
            var accessCheck = RedirectIfNotAdmin();
            if (accessCheck != null) return accessCheck;

            try
            {
                if (await _backupService.DeleteBackupAsync(backupId, GetCurrentStaffId()))
                    TempData["SuccessMessage"] = "Backup file deleted; its history record was retained.";
                else
                    TempData["ErrorMessage"] = "The backup file could not be deleted.";
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Backup deletion request failed for backup ID {BackupId}.", backupId);
                TempData["ErrorMessage"] = "The backup file could not be deleted.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
