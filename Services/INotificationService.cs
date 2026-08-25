using System.Collections.Generic;
using System.Threading.Tasks;
using KaijensonIventory_SalesMotorShopWeb.Models;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public interface INotificationService
    {
        Task CreateAsync(int productId, string alertType, string message);

        // Creates a notification only when no unread notification with the same
        // ProductId + AlertType already exists (prevents duplicate spam).
        Task CreateOnceAsync(int productId, string alertType, string message);

        // Marks unread notifications of the given type for the product as read
        // (used for recovery, e.g. stock received above the threshold).
        Task ResolveUnreadAsync(int productId, string alertType);

        Task<List<Notification>> GetRecentAsync(int take);
        Task<int> GetUnreadCountAsync();
        Task MarkAsReadAsync(int notificationId);
        Task MarkAllAsReadAsync();
    }
}
