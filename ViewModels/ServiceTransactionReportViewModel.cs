
using System;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class ServiceTransactionReportViewModel
    {
        public int ServiceJobId { get; set; }
        public string ServiceJobNumber { get; set; } = string.Empty;
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal AmountPaid { get; set; }
        public decimal ChangeAmount { get; set; }
        public decimal ServicePrice { get; set; }
        public DateTime ServiceDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public int? SalesTransactionId { get; set; }
    }
}
