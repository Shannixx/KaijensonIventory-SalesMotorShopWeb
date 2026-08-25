using System;
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
        private readonly IActivityLogService _activityLogService;

        public NotificationService(ApplicationDbContext context,
                                   IActivityLogService activityLogService)
        {
            _context = context;
            _activityLogService = activityLogService;
        }

        public async Task CreateAsync(int productId, string alertType, string message, int? staffId = null)
        {
            var notification = new Notification
            {
                ProductId = productId,
                AlertType = alertType,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // Activity Log: only when a new notification is actually inserted
            await _activityLogService.LogAsync(
                "Notification Created",
                "Notification",
                $"{GetAlertLabel(alertType)} notification - {message}",
                staffId);
        }

        public async Task CreateOnceAsync(int productId, string alertType, string message, int? staffId = null)
        {
            bool unreadExists = await _context.Notifications
                .AsNoTracking()
                .AnyAsync(n => n.ProductId == productId
                            && n.AlertType == alertType
                            && !n.IsRead);
            if (unreadExists)
                return; // condition still active and already reported - do not spam

            await CreateAsync(productId, alertType, message, staffId);
        }

        public async Task ResolveUnreadAsync(int productId, string alertType, int? staffId = null)
        {
            var active = await _context.Notifications
                .Include(n => n.Product)
                .Where(n => n.ProductId == productId
                         && n.AlertType == alertType
                         && !n.IsRead)
                .ToListAsync();

            if (active.Count == 0)
                return;

            foreach (var notification in active)
            {
                notification.IsRead = true;

                // Activity Log: only for notifications that were actually changed
                await _activityLogService.LogAsync(
                    "Notification Resolved",
                    "Notification",
                    ResolveDescription(alertType, GetProductLabel(notification)),
                    staffId);
            }

            await _context.SaveChangesAsync();
        }

        // Active dropdown shows unread notifications only; read ones stay in the database for history.
        public async Task<List<Notification>> GetRecentAsync(int take)
        {
            return await _context.Notifications
                .Include(n => n.Product)
                .AsNoTracking()
                .Where(n => !n.IsRead)
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

        public async Task MarkAsReadAsync(int notificationId, int? staffId = null)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == notificationId);
            if (notification == null || notification.IsRead)
                return;

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            // Activity Log: only on an actual false -> true transition
            await _activityLogService.LogAsync(
                "Notification Read",
                "Notification",
                $"Notification marked as read: {GetAlertLabel(notification.AlertType)} - {notification.Message}",
                staffId);
        }

        public async Task<int> MarkAllAsReadAsync(int? staffId = null)
        {
            var unread = await _context.Notifications
                .Where(n => !n.IsRead)
                .ToListAsync();
            if (unread.Count == 0)
                return 0;

            foreach (var notification in unread)
                notification.IsRead = true;

            await _context.SaveChangesAsync();

            // One Activity Log entry for the whole batch, only when count > 0
            await _activityLogService.LogAsync(
                "Notifications Cleared",
                "Notification",
                $"Marked {unread.Count} notifications as read.",
                staffId);

            return unread.Count;
        }

        private static string GetAlertLabel(string alertType) => alertType switch
        {
            "LowStock" => "Low Stock",
            "OutOfStock" => "Out of Stock",
            "Reorder" => "Reorder",
            _ => "Notification"
        };

        private static string GetProductLabel(Notification notification) =>
            !string.IsNullOrWhiteSpace(notification.Product?.ProductName)
                ? notification.Product!.ProductName
                : (notification.ProductId.HasValue
                    ? $"Product #{notification.ProductId.Value}"
                    : "product");

        private static string ResolveDescription(string alertType, string product) => alertType switch
        {
            "LowStock" => $"Low Stock notification resolved for {product} because inventory returned above the threshold.",
            "OutOfStock" => $"Out of Stock notification resolved for {product} because stock became available again.",
            "Reorder" => $"Reorder notification resolved for {product} because quantity is back above the reorder level.",
            _ => $"Notification resolved for {product}."
        };
    }
}
