using System.Threading.Tasks;
using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(int productId, string alertType, string message)
        {
            var notification = new Notification
            {
                ProductId = productId,
                AlertType = alertType,
                Message = message,
                IsRead = false,
                CreatedAt = System.DateTime.Now
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }
    }
}
