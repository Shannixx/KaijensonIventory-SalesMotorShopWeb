using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.EntityFrameworkCore;

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

        public async Task CreateOnceAsync(int productId, string alertType, string message)
        {
            bool unreadExists = await _context.Notifications
                .AsNoTracking()
                .AnyAsync(n => n.ProductId == productId
                            && n.AlertType == alertType
                            && !n.IsRead);
            if (unreadExists)
                return; // condition still active and already reported - do not spam

            await CreateAsync(productId, alertType, message);
        }

        public async Task ResolveUnreadAsync(int productId, string alertType)
        {
            var active = await _context.Notifications
                .Where(n => n.ProductId == productId
                         && n.AlertType == alertType
                         && !n.IsRead)
                .ToListAsync();

            if (active.Count == 0)
                return;

            foreach (var notification in active)
                notification.IsRead = true;

            await _context.SaveChangesAsync();
        }

        public async Task<List<Notification>> GetRecentAsync(int take)
        {
            return await _context.Notifications
                .Include(n => n.Product)
                .AsNoTracking()
                .OrderByDescending(n => n.CreatedAt)
                .Take(take)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync()
        {
            return await _context.Notifications
                .AsNoTracking()
                .CountAsync(n => !n.IsRead);
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == notificationId);
            if (notification == null || notification.IsRead)
                return;

            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }

        public async Task MarkAllAsReadAsync()
        {
            var unread = await _context.Notifications
                .Where(n => !n.IsRead)
                .ToListAsync();
            if (unread.Count == 0)
                return;

            foreach (var notification in unread)
                notification.IsRead = true;

            await _context.SaveChangesAsync();
        }
    }
}
