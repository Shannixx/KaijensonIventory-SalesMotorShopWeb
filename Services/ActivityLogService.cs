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
    const int maxLength = 500;
    if (!string.IsNullOrEmpty(description) && description.Length > maxLength)
        description = description.Substring(0, maxLength);

    _context.ActivityLogs.Add(new ActivityLog
    {
        StaffId = staffId,
        Action = action,
        Module = module,
        Description = description
    });

    await _context.SaveChangesAsync();
}
    }
}
