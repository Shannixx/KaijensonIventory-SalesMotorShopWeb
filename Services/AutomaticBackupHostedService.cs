using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;


namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public class AutomaticBackupHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AutomaticBackupHostedService> _logger;
        private readonly IConfiguration _config;

        public AutomaticBackupHostedService(IServiceScopeFactory scopeFactory, ILogger<AutomaticBackupHostedService> logger, IConfiguration config)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var settings = _config.GetSection("BackupSettings").Get<BackupSettings>();
                if (settings == null)
                {
                    _logger.LogError("BackupSettings not configured.");
                    // Wait before next retry to avoid busy loop
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    }
                    catch (OperationCanceledException) { throw; }
                    continue;
                }

                // Compute next run time based on configured hour/minute
                var now = DateTime.Now;
                var next = new DateTime(now.Year, now.Month, now.Day, settings.Hour, settings.Minute, 0);
                if (now >= next) next = next.AddDays(1);
                var delay = next - now;
                if (delay > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(delay, stoppingToken);
                    }
                    catch (OperationCanceledException) { throw; }
                }

                if (!settings.Enabled)
                {
                    _logger.LogInformation("Automatic backup disabled; skipping run.");
                    continue; // loop again to re‑read settings
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
                    var activityLog = scope.ServiceProvider.GetRequiredService<IActivityLogService>();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var backup = await backupService.CreateAutomaticBackupAsync();

                    // Log according to backup status (single source of truth)
                    if (backup.Status == "Successful")
                    {
                        await activityLog.LogAsync(
                            action: "Automatic Database Backup",
                            module: "System",
                            description: "System attempted automatic database backup. Status: Successful",
                            staffId: null);

                        _logger.LogInformation($"Automatic backup created: {backup.FileName}");

                        // Enforce retention only after a successful backup
                        await EnforceRetentionAsync(dbContext, settings.RetentionCount);
                    }
                    else
                    {
                        // Backup failed but CreateAutomaticBackupAsync did not throw – log failure
                        await activityLog.LogAsync(
                            action: "Automatic Database Backup",
                            module: "System",
                            description: $"System attempted automatic database backup. Status: Failed. Reason: {backup.Description ?? "unknown"}",
                            staffId: null);

                        _logger.LogWarning($"Automatic backup failed: {backup.FileName}");
                        // No retention enforcement on failure
                    }
                }
                catch (OperationCanceledException)
                {
                    // Respect cancellation – allow service to stop gracefully
                    throw;
                }
                catch (Exception ex)
                {
                    // Log failure to ActivityLog (safe summary, no stack trace)
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var activityLog = scope.ServiceProvider.GetRequiredService<IActivityLogService>();
                        await activityLog.LogAsync(
                            action: "Automatic Database Backup",
                            module: "System",
                            description: $"System attempted automatic database backup. Status: Failed. Error: {ex.Message}",
                            staffId: null);
                    }
                    catch { /* ignore secondary failures */ }

                    _logger.LogError(ex, "Automatic backup failed.");
                    // Continue loop for next scheduled run
                }
            }
        }

        private async Task EnforceRetentionAsync(ApplicationDbContext context, int retainCount)
        {
            var autoBackups = await context.DatabaseBackups
                .Where(b => b.BackupType == "Automatic" && b.Status == "Successful")
                .OrderBy(b => b.CreatedAt)
                .ToListAsync();
            if (autoBackups.Count <= retainCount) return;
            var toDelete = autoBackups.Take(autoBackups.Count - retainCount);
            foreach (var backup in toDelete)
            {
                try
                {
                    var fullPath = Path.GetFullPath(backup.FilePath);
                    if (File.Exists(fullPath))
                        File.Delete(fullPath);
                    context.DatabaseBackups.Remove(backup);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to delete old backup {backup.BackupId}");
                }
            }
            await context.SaveChangesAsync();
        }
    }

    public class BackupSettings
    {
        public bool Enabled { get; set; } = true;
        public int Hour { get; set; } = 21;
        public int Minute { get; set; } = 0;
        public int RetentionCount { get; set; } = 7;
        // Optional custom backup directory for SQL backup files.
        // If null or empty, the service will determine a suitable location.
        public string? BackupDirectory { get; set; }
    }
}
