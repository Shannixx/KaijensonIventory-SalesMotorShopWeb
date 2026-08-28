using KaijensonIventory_SalesMotorShopWeb.Data;
using Microsoft.Extensions.Logging;
using KaijensonIventory_SalesMotorShopWeb.Models;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public class ActivityLogService : IActivityLogService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ActivityLogService> _logger;

        public ActivityLogService(ApplicationDbContext context, ILogger<ActivityLogService> logger)
        {
            _context = context;
            _logger = logger;
        }

public async Task LogAsync(string action, string module, string description, int? staffId)
        {
            // Truncate description to match database column length (500)
            const int maxLength = 500;
            if (!string.IsNullOrEmpty(description) && description.Length > maxLength)
            {
                description = description.Substring(0, maxLength);
            }
            try
            {
                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = staffId,
                    Action = action,
                    Module = module,
                    Description = description
                });
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log the failure using injected logger without throwing further to preserve original operation flow
                _logger?.LogError(ex, "Failed to write ActivityLog entry: {Action} {Module}", action, module);
            }
        }
    }
}
