using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class BackupController : BaseController
    {
        private readonly IBackupService _backupService;
        private readonly IActivityLogService _activityLog;
        private readonly IWebHostEnvironment _env;

        public BackupController(IBackupService backupService, IActivityLogService activityLog, IWebHostEnvironment env)
        {
            _backupService = backupService;
            _activityLog = activityLog;
            _env = env;
        }

        // List backup history (Admin view) - Owner can view
        public async Task<IActionResult> Index()
        {
            var redirect = RedirectIfNotOwnerOrManager();
            if (redirect != null) return redirect;

            var history = await _backupService.GetBackupHistoryAsync();

            // Summary calculations
            var lastSuccess = history.FirstOrDefault(b => b.Status == "Successful");
            var availableCount = history.Count(b => b.Status == "Successful");
            var latestBackup = history.FirstOrDefault();
            var dbStatus = await _backupService.GetDatabaseStatusAsync(); // real connectivity check

            ViewBag.LastSuccess = lastSuccess;
            ViewBag.AvailableCount = availableCount;
            ViewBag.LatestBackup = latestBackup;
            ViewBag.DbStatus = dbStatus;

            return View(history);
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

            var backupRoot = Path.Combine(_env.ContentRootPath, "App_Data", "Backups");
            var fullPath = Path.GetFullPath(backup.FilePath);
            if (!fullPath.StartsWith(Path.GetFullPath(backupRoot)))
                return BadRequest("Invalid file path.");

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
