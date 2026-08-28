using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using KaijensonIventory_SalesMotorShopWeb.Data;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class BackupController : BaseController
    {
        private readonly IBackupService _backupService;
        private readonly IActivityLogService _activityLog;
        private readonly IWebHostEnvironment _env;
        private readonly IBackupConfigurationService _configService;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<BackupController> _logger;
        public BackupController(IBackupService backupService, IActivityLogService activityLog, IWebHostEnvironment env, IBackupConfigurationService configService, ApplicationDbContext dbContext, ILogger<BackupController> logger)
        {
            _backupService = backupService;
            _activityLog = activityLog;
            _env = env;
            _configService = configService;
            _dbContext = dbContext;
            _logger = logger;
        }

        // List backup history (Admin view)
        // Save automatic backup settings (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSettings(BackupConfiguration model)
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;
            await _configService.SaveAdminSettingsAsync(model);
            // Log the change
            await _activityLog.LogAsync("Backup Settings Updated", "System", "Automatic backup settings changed", staffId: GetCurrentStaffId());
            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> Index()
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            var history = await _backupService.GetBackupHistoryAsync();
            var dbStatus = await _backupService.GetDatabaseStatusAsync();
            var settings = await _configService.GetAsync();

            var lastSuccess = history
                .Where(b => b.Status == "Successful")
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefault();
            var latest = history.OrderByDescending(b => b.CreatedAt).FirstOrDefault();
            var latestAuto = history
                .Where(b => b.BackupType == "Automatic")
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefault();
            DateTime? nextScheduled = null;
            if (settings.Enabled)
            {
                nextScheduled = settings.NextAutomaticRun;
            }

            var viewModel = new KaijensonIventory_SalesMotorShopWeb.ViewModels.BackupPageViewModel
            {
                History = history,
                Settings = settings,
                LastSuccessfulBackup = lastSuccess,
                AvailableBackupCount = history?.Count ?? 0,
                LatestBackup = latest,
                LatestAutomaticBackup = latestAuto,
                NextScheduledBackup = nextScheduled,
                DatabaseStatus = dbStatus
            };

            return View(viewModel);
        }

        // New Review action for displaying backup details and data coverage
        public async Task<IActionResult> Review(int id)
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            var backup = await _backupService.GetBackupAsync(id);
            if (backup == null || backup.Status != "Successful")
                return NotFound();

            // Get list of entity names from DbContext via reflection
            // Use EF Core metadata to get actual table names
            var entityTypes = _dbContext.Model.GetEntityTypes();
            var entityNames = entityTypes
                .Select(e => (e.GetSchema() != null ? $"{e.GetSchema()}." : "") + e.GetTableName())
                .OrderBy(n => n)
                .ToList();
            ViewBag.EntityNames = entityNames;

            // Get real database name from connection string
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_dbContext.Database.GetDbConnection().ConnectionString);
            ViewBag.DatabaseName = builder.InitialCatalog;

            return View(backup);
        }



        // POST: Manual backup directly from Backup page
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BackupNow()
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            var staffId = GetCurrentStaffId();
            var backup = await _backupService.CreateBackupAsync(staffId);
            if (backup.Status != "Successful")
            {
                // Log already done in service; show error view
                ViewBag.ErrorMessage = backup.Description ?? "Manual backup failed.";
                return View("Error", backup);
            }
            // Log manual backup
            await _activityLog.LogAsync("Manual Database Backup", "System", "Manual backup performed", staffId);
            return RedirectToAction(nameof(Index));
        }

        // Delete backup (Admin)
        public async Task<IActionResult> Delete(int id)
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            var backup = await _backupService.GetBackupAsync(id);
            if (backup == null)
                return NotFound();

            // Only allow deletion of eligible backups (Manual or Automatic Successful, not safety backup)
            if (backup.BackupType == "Pre-Restore Safety Backup" || backup.Status != "Successful")
                return BadRequest("Backup cannot be deleted.");

            return View("Delete", backup);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            var backup = await _backupService.GetBackupAsync(id);
            if (backup == null)
                return NotFound();

            // Delete file and record
            try
            {
                if (System.IO.File.Exists(backup.FilePath))
                {
                    System.IO.File.Delete(backup.FilePath);
                }
                else
                {
                    // Log missing physical file
                    await _activityLog.LogAsync("Backup Delete", "System", $"Physical file missing for backup ID {id} at path {backup.FilePath}", GetCurrentStaffId());
                }
                _dbContext.DatabaseBackups.Remove(backup);
                await _dbContext.SaveChangesAsync();
                await _activityLog.LogAsync("Backup Deleted", "System", $"Deleted backup ID {id}", GetCurrentStaffId());
            }
            catch (Exception ex)
            {
                await _activityLog.LogAsync("Backup Delete Failed", "System", ex.Message, GetCurrentStaffId());
                // Show error view
                ViewBag.ErrorMessage = "Failed to delete backup.";
                return View("Error", backup);
            }
            return RedirectToAction(nameof(Index));
        }

// GET: confirmation page for recovery
        [HttpGet]
        public async Task<IActionResult> ConfirmRecover(int id)
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            var backup = await _backupService.GetBackupAsync(id);
            if (backup == null || backup.Status != "Successful")
                return BadRequest("Invalid backup for recovery.");

            // Get real database name from connection string
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_dbContext.Database.GetDbConnection().ConnectionString);
            ViewBag.DatabaseName = builder.InitialCatalog;

            // Show confirmation view with backup details
            return View("ConfirmRecover", backup);
        }

        // POST: perform the actual restore after confirmation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Recover(int id)
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            var backup = await _backupService.GetBackupAsync(id);
            if (backup == null || backup.Status != "Successful")
                return BadRequest("Invalid backup for recovery.");

            // Validate backup before recovery
            var isValid = await _backupService.ValidateBackupAsync(id);
            if (!isValid)
            {
                await _activityLog.LogAsync("Recovery Validation Failed", "System", $"Backup ID {id} failed validation", GetCurrentStaffId());
                return BadRequest("Backup validation failed. Cannot recover.");
            }

            var staffId = GetCurrentStaffId();
            // Log start of recovery
            _logger?.LogInformation("[Recovery] Starting restore for BackupId={BackupId}", id);
            var success = await _backupService.RestoreBackupAsync(id, staffId);
            // Result UI – reuse Result view
            ViewBag.Success = success;
            ViewBag.BackupId = id;
            return View("Result");
        }

        // Remove outdated confirm and execute actions
    }
}
