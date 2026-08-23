using System.ComponentModel.DataAnnotations;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace KaijensonIventory_SalesMotorShopWeb.ViewModels
{
    public class PurchaseOrderViewModel
    {
        public bool IsReorder { get; set; }
        public int? PurchaseOrderId { get; set; }

        [Display(Name = "PO Number")]
        public string? PurchaseOrderNumber { get; set; }

        [Required(ErrorMessage = "Please select a supplier.")]
        [Display(Name = "Supplier")]
        public int SupplierId { get; set; }

        public string? SupplierName { get; set; }
        public string? ContactPerson { get; set; }
        public string? ContactNumber { get; set; }
        public string? SupplierAddress { get; set; }

        [Required(ErrorMessage = "Order date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Order Date")]
        public DateTime OrderDate { get; set; } = DateTime.Now;

        [DataType(DataType.Date)]
        [Display(Name = "Expected Delivery Date")]
        public DateTime? ExpectedDeliveryDate { get; set; }

        [Display(Name = "Status")]
        public string? Status { get; set; }

        [Display(Name = "Total Amount")]
        public decimal TotalAmount { get; set; }

        [StringLength(500)]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        [Display(Name = "Created By")]
        public string? CreatedByName { get; set; }

        [Display(Name = "Created Date")]
        public DateTime? CreatedDate { get; set; }

        [Display(Name = "Updated Date")]
        public DateTime? UpdatedDate { get; set; }

        public List<PurchaseOrderItemViewModel> Items { get; set; } = new();

        public List<SelectListItem> Suppliers { get; set; } = new();
    }
}
