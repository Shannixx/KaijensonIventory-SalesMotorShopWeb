using Microsoft.AspNetCore.SignalR;

namespace KaijensonIventory_SalesMotorShopWeb.Hubs
{
    public class NotificationHub : Hub
    {
        public async Task SendNotification(string message, string type)
        {
            await Clients.All.SendAsync("ReceiveNotification", message, type);
        }

        public async Task UpdateDashboard()
        {
            await Clients.All.SendAsync("DashboardUpdated");
        }

        public async Task UpdateStock(string productName, int newQuantity, string status)
        {
            await Clients.All.SendAsync("StockUpdated", productName, newQuantity, status);
        }
    }
}
