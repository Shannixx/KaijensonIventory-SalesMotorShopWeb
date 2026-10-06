using KaijensonIventory_SalesMotorShopWeb.ViewModels;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public interface IDeliveryService
    {
        Task<List<DeliveryViewModel>> GetAwaitingDeliveryAsync(bool archived = false);

        Task<DeliveryViewModel?> GetDeliveryDetailsAsync(int id, bool archived = false);

        Task<Result> DeliverAsync(int id, Dictionary<int,int> receiveQuantities, int currentStaffId, string receiptKey, string? remarks = null);

        Task<Result> ArchiveAsync(int id, int currentStaffId);

        Task<Result> RestoreAsync(int id, int currentStaffId);
    }
}
