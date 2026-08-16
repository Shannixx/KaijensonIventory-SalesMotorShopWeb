using KaijensonIventory_SalesMotorShopWeb.ViewModels;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public interface IDeliveryService
    {
        Task<List<DeliveryViewModel>> GetAwaitingDeliveryAsync();

        Task<DeliveryViewModel?> GetDeliveryDetailsAsync(int id);

        Task<Result> DeliverAsync(int id, Dictionary<int,int> receiveQuantities, int currentStaffId);
    }
}
