using System.Threading.Tasks;
using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public class BackupConfigurationService : IBackupConfigurationService
    {
        private readonly ApplicationDbContext _context;

        public BackupConfigurationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<BackupConfiguration> GetAsync()
        {
            // Ensure there is always a singleton row
            var config = await _context.BackupConfigurations.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new BackupConfiguration();
                _context.BackupConfigurations.Add(config);
                await _context.SaveChangesAsync();
            }
            return config;
        }

        public async Task SaveAsync(BackupConfiguration config)
        {
            // Ensure NextAutomaticRun is set appropriately before persisting
            if (config.Enabled)
            {
                config.NextAutomaticRun = ComputeNextOccurrence(DateTime.Now, config);
            }
            else
            {
                config.NextAutomaticRun = null;
            }

            var existing = await _context.BackupConfigurations.FirstOrDefaultAsync();
            if (existing == null)
            {
                _context.BackupConfigurations.Add(config);
            }
            else
            {
                // Update fields
                existing.Enabled = config.Enabled;
                existing.Frequency = config.Frequency;
                existing.Hour = config.Hour;
                existing.Minute = config.Minute;
                existing.DayOfWeek = config.DayOfWeek;
                existing.DayOfMonth = config.DayOfMonth;
                existing.RetentionCount = config.RetentionCount;
                existing.BackupDirectory = config.BackupDirectory;
                existing.LastAutomaticRun = config.LastAutomaticRun;
                // Persist NextAutomaticRun (already computed on config)
                existing.NextAutomaticRun = config.NextAutomaticRun;
            }
            await _context.SaveChangesAsync();
        }

        private DateTime ComputeNextOccurrence(DateTime now, BackupConfiguration config)
        {
            switch (config.Frequency?.ToLowerInvariant())
            {
                case "weekly":
                    var targetDow = config.DayOfWeek ?? 0;
                    var daysAhead = ((int)targetDow - (int)now.DayOfWeek + 7) % 7;
                    var occurrence = new DateTime(now.Year, now.Month, now.Day, config.Hour, config.Minute, 0).AddDays(daysAhead);
                    if (occurrence < now) occurrence = occurrence.AddDays(7);
                    return occurrence;
                case "monthly":
                    var day = config.DayOfMonth ?? 1;
                    int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
                    if (day > daysInMonth) day = daysInMonth;
                    var occMonth = new DateTime(now.Year, now.Month, day, config.Hour, config.Minute, 0);
                    if (occMonth < now)
                    {
                        var nextMonth = now.AddMonths(1);
                        daysInMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
                        var nextDay = config.DayOfMonth ?? 1;
                        if (nextDay > daysInMonth) nextDay = daysInMonth;
                        occMonth = new DateTime(nextMonth.Year, nextMonth.Month, nextDay, config.Hour, config.Minute, 0);
                    }
                    return occMonth;
                case "daily":
                default:
                    var occDaily = new DateTime(now.Year, now.Month, now.Day, config.Hour, config.Minute, 0);
                    if (occDaily < now) occDaily = occDaily.AddDays(1);
                    return occDaily;
            }
        }
    }
}
