using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
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

        public AutomaticBackupHostedService(IServiceScopeFactory scopeFactory, ILogger<AutomaticBackupHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Loop checks configuration roughly every minute.
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Resolve scoped services for this iteration.
                    using var scope = _scopeFactory.CreateScope();
                    var configService = scope.ServiceProvider.GetRequiredService<IBackupConfigurationService>();
                    var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
                    var activityLog = scope.ServiceProvider.GetRequiredService<IActivityLogService>();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var settings = await configService.GetAsync();

                    if (!settings.Enabled)
                    {
                        _logger.LogInformation("Automatic backup disabled; waiting for next check.");
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                        continue;
                    }

                    var now = DateTime.Now;
                    var scheduledOccurrence = settings.NextAutomaticRun;

                    // If the scheduled time has passed, attempt a backup.
                    if (scheduledOccurrence != null && now >= scheduledOccurrence.Value)
                    {
                        var backup = await backupService.CreateAutomaticBackupAsync();

                        if (backup.Status == "Successful")
                        {
                            // Record that this occurrence has been processed
                            settings.LastAutomaticRun = scheduledOccurrence.Value;
                            // Calculate next occurrence and persist
                            settings.NextAutomaticRun = ComputeNextOccurrence(DateTime.Now, settings);
                            await configService.SaveSchedulerStateAsync(settings.LastAutomaticRun, settings.NextAutomaticRun);

                            await activityLog.LogAsync(
                                action: "Automatic Database Backup",
                                module: "System",
                                description: "System attempted automatic database backup. Status: Successful",
                                staffId: null);

                            _logger.LogInformation($"Automatic backup created: {backup.FileName}");
                            await EnforceRetentionAsync(dbContext, settings.RetentionCount);
                        }
                        else
                        {
                            await activityLog.LogAsync(
                                action: "Automatic Database Backup",
                                module: "System",
                                description: $"System attempted automatic database backup. Status: Failed. Reason: {backup.Description ?? "unknown"}",
                                staffId: null);

                            _logger.LogWarning($"Automatic backup failed: {backup.FileName}");
                        }
                    }

                    // Wait a short period before re‑checking configuration.
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Graceful shutdown; exit loop.
                    break;
                }
                catch (Exception ex)
                {
                    // Log unexpected errors but keep service alive.
                    _logger.LogError(ex, "Unexpected error in AutomaticBackupHostedService iteration.");
                    // Continue to next iteration after a short delay to avoid tight loop on persistent failure.
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
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

        private DateTime ComputeNextOccurrence(DateTime now, BackupConfiguration config) => GetNextOccurrence(now, config);

        private DateTime GetNextOccurrence(DateTime now, BackupConfiguration config)
        {
        
            switch (config.Frequency?.ToLowerInvariant())
            {
                case "weekly":
                    var targetDow = config.DayOfWeek ?? 0;
                    // Compute next occurrence on or after now
                    var daysAhead = ((int)targetDow - (int)now.DayOfWeek + 7) % 7;
                    var occurrence = new DateTime(now.Year, now.Month, now.Day, config.Hour, config.Minute, 0).AddDays(daysAhead);
                    if (occurrence < now) occurrence = occurrence.AddDays(7);
                    return occurrence;
                case "monthly":
                    var day = config.DayOfMonth ?? 1;
                    int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
                    if (day > daysInMonth) day = daysInMonth;
                    var occurrenceMonth = new DateTime(now.Year, now.Month, day, config.Hour, config.Minute, 0);
                    if (occurrenceMonth < now)
                    {
                        var nextMonth = now.AddMonths(1);
                        daysInMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
                        var nextDay = config.DayOfMonth ?? 1;
                        if (nextDay > daysInMonth) nextDay = daysInMonth;
                        occurrenceMonth = new DateTime(nextMonth.Year, nextMonth.Month, nextDay, config.Hour, config.Minute, 0);
                    }
                    return occurrenceMonth;
                case "daily":
                default:
                    var occurrenceDaily = new DateTime(now.Year, now.Month, now.Day, config.Hour, config.Minute, 0);
                    if (occurrenceDaily < now) occurrenceDaily = occurrenceDaily.AddDays(1);
                    return occurrenceDaily;
            }
        }
    }

    public class BackupSettings
    {
        public bool Enabled { get; set; } = false;
        public int Hour { get; set; } = 0;
        public int Minute { get; set; } = 0;
        public int RetentionCount { get; set; } = 7;
        public string? BackupDirectory { get; set; }
    }
}
