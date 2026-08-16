using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class ReceiveDeliveryViewModel
    {
        public int DeliveryId { get; set; }

        // Key: PurchaseOrderItemId, Value: quantity to receive now
        public Dictionary<int, int> ReceiveQuantities { get; set; } = new();
    }
}