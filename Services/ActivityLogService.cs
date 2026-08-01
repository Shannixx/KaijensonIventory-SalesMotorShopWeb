using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public class ActivityLogService : IActivityLogService
    {
        private readonly ApplicationDbContext _context;

        public ActivityLogService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(string action, string module, string description, int? staffId)
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
    }
}
