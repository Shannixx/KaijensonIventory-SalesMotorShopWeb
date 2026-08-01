namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public interface IActivityLogService
    {
        Task LogAsync(string action, string module, string description, int? staffId);
    }
}
