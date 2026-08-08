using System.Threading.Tasks;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;

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
