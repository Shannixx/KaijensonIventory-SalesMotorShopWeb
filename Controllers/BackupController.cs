using Microsoft.Extensions.Configuration;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Linq;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class BackupController : BaseController
    {
        private readonly IBackupService _backupService;
        private readonly IActivityLogService _activityLog;
        private readonly IWebHostEnvironment _env;
        private readonly IBackupConfigurationService _configService;

        public BackupController(IBackupService backupService, IActivityLogService activityLog, IWebHostEnvironment env, IBackupConfigurationService configService)
        {
            _backupService = backupService;
            _activityLog = activityLog;
            _env = env;
            _configService = configService;
        }

        // List backup history (Admin view)
        // Save automatic backup settings (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSettings(BackupConfiguration model)
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;
            await _configService.SaveAsync(model);
            // Log the change
            await _activityLog.LogAsync("Backup Settings Updated", "System", "Automatic backup settings changed", staffId: GetCurrentStaffId());
            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> Index()
        {
            var redirect = RedirectIfNotOwnerOrManager();
            if (redirect != null) return redirect;

var history = await _backupService.GetBackupHistoryAsync();
            var dbStatus = await _backupService.GetDatabaseStatusAsync();
            var settings = await _configService.GetAsync();

            // Compute summary properties for the view model
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
                // Reuse the same scheduling logic as the hosted service
                var now = DateTime.Now;
                switch (settings.Frequency?.ToLowerInvariant())
                {
                    case "weekly":
                        var targetDow = settings.DayOfWeek ?? 0;
                        var daysUntil = ((int)targetDow - (int)now.DayOfWeek + 7) % 7;
                        var candidate = new DateTime(now.Year, now.Month, now.Day, settings.Hour, settings.Minute, 0).AddDays(daysUntil);
                        if (candidate <= now) candidate = candidate.AddDays(7);
                        nextScheduled = candidate;
                        break;
                    case "monthly":
                        var day = settings.DayOfMonth ?? 1;
                        int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
                        if (day > daysInMonth) day = daysInMonth;
                        var candidateMonth = new DateTime(now.Year, now.Month, day, settings.Hour, settings.Minute, 0);
                        if (candidateMonth <= now)
                        {
                            var nextMonth = now.AddMonths(1);
                            daysInMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
                            var nextDay = settings.DayOfMonth ?? 1;
                            if (nextDay > daysInMonth) nextDay = daysInMonth;
                            candidateMonth = new DateTime(nextMonth.Year, nextMonth.Month, nextDay, settings.Hour, settings.Minute, 0);
                        }
                        nextScheduled = candidateMonth;
                        break;
                    case "daily":
                    default:
                        var candidateDaily = new DateTime(now.Year, now.Month, now.Day, settings.Hour, settings.Minute, 0);
                        if (candidateDaily <= now) candidateDaily = candidateDaily.AddDays(1);
                        nextScheduled = candidateDaily;
                        break;
                }
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



        // Show confirmation page for creating backup (Admin)
        public IActionResult Create()
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;
            return View();
        }

        // POST: actually create backup
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateConfirmed()
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            var staffId = GetCurrentStaffId();
            await _backupService.CreateBackupAsync(staffId);
            return RedirectToAction(nameof(Index));
        }

        // Download backup file (Admin, secure path)
        public async Task<IActionResult> Download(int id)
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            var backup = await _backupService.GetBackupAsync(id);
            if (backup == null || backup.Status != "Successful")
                return NotFound();

            // Use stored backup file path directly (already validated on creation)
            var fullPath = backup.FilePath;
            if (!System.IO.File.Exists(fullPath))
                return NotFound();

            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            return File(fileBytes, "application/octet-stream", backup.FileName);
        }

        // Step A/B: Validate backup and show result page
        public async Task<IActionResult> ValidateBackup(int id)
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            var backup = await _backupService.GetBackupAsync(id);
            if (backup == null)
                return NotFound();

            var isValid = await _backupService.ValidateBackupAsync(id);
            // Pass info via ViewBag for status display
            ViewBag.IsValid = isValid;
            // Pass the backup as the model for the view
            return View("Validate", backup);
        }


        // Step C: Show restore preview (requires prior validation success)
        public async Task<IActionResult> PreviewRestore(int id)
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            var backup = await _backupService.GetBackupAsync(id);
            if (backup == null)
                return NotFound();

            var isValid = await _backupService.ValidateBackupAsync(id);
            if (!isValid)
                return BadRequest("Backup validation failed. Cannot preview restore.");

            // Pass backup as model instead of ViewBag
            return View("Preview", backup);
        }

        // Step D: Final confirmation page (GET) – shows same info with strong confirmation
        public async Task<IActionResult> ConfirmRestore(int id)
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            var backup = await _backupService.GetBackupAsync(id);
            if (backup == null)
                return NotFound();

            var isValid = await _backupService.ValidateBackupAsync(id);
            if (!isValid)
                return BadRequest("Backup validation failed. Cannot confirm restore.");

            // Pass backup as model to the view
            return View("Confirm", backup);
        }

        // Step E/F/G/H/I: Execute restore after final confirmation (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecuteRestore(int id)
        {
            var redirect = RedirectIfNotAdmin();
            if (redirect != null) return redirect;

            var staffId = GetCurrentStaffId();
            var success = await _backupService.RestoreBackupAsync(id, staffId);
            // Result UI
            ViewBag.Success = success;
            ViewBag.BackupId = id;
            return View("Result");
        }
    }
}
