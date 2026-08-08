using System.Threading.Tasks;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public interface INotificationService
    {
        Task CreateAsync(int productId, string alertType, string message);
    }
}
