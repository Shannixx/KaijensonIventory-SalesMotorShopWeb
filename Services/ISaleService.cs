using System.Threading.Tasks;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using KaijensonIventory_SalesMotorShopWeb.Models;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public interface ISaleService
    {
        Task<SalesTransaction> ProcessSaleAsync(
            CartViewModel cart,
            decimal amountPaid,
            string checkoutKey,
            int staffId);
    }
}
