using KaijensonIventory_SalesMotorShopWeb.Services;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class DeliveryController : BaseController
    {
        private readonly IDeliveryService _deliveryService;

        public DeliveryController(IDeliveryService deliveryService)
        {
            _deliveryService = deliveryService;
        }

        private IActionResult? CheckAccess()
        {
            if (!IsSessionValid())
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }
            if (!IsOwnerOrManager())
            {
                TempData["ErrorMessage"] = "Access denied. Admin or Manager privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }
            return null;
        }

        public async Task<IActionResult> Index()
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            var deliveries = await _deliveryService.GetAwaitingDeliveryAsync();

            return View(deliveries);
        }

        public async Task<IActionResult> Details(int id)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            var viewModel = await _deliveryService.GetDeliveryDetailsAsync(id);
            if (viewModel == null) return NotFound();

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Receive(ReceiveDeliveryViewModel model)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            if (model == null || model.DeliveryId <= 0)
            {
                TempData["ErrorMessage"] = "Invalid delivery data.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _deliveryService.DeliverAsync(model.DeliveryId, model.ReceiveQuantities, GetCurrentStaffId());

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault()?.Message
                    ?? "An error occurred while processing the delivery. Please try again.";
                return RedirectToAction(nameof(Details), new { id = model.DeliveryId });
            }

            TempData["SuccessMessage"] = "Delivery processed successfully. Stock updated.";
            return RedirectToAction(nameof(Details), new { id = model.DeliveryId });
        }

        // Compatibility action to mark delivery as delivered in one step
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkDelivered(int id)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            // Load delivery details to compute remaining quantities
            var delivery = await _deliveryService.GetDeliveryDetailsAsync(id);
            if (delivery == null) return NotFound();

            if (delivery.Status == "Delivered")
            {
                // Already delivered, just redirect
                TempData["SuccessMessage"] = "Delivery already marked as delivered.";
                return RedirectToAction(nameof(Index));
            }

            // Build dictionary of remaining quantities per item
            var remaining = new Dictionary<int, int>();
            foreach (var item in delivery.Items)
            {
                int remainingQty = item.Quantity - item.ReceivedQuantity;
                if (remainingQty > 0)
                {
                    remaining[item.PurchaseOrderItemId] = remainingQty;
                }
            }

            var result = await _deliveryService.DeliverAsync(id, remaining, GetCurrentStaffId());

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault()?.Message ?? "Failed to mark delivery as delivered.";
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] = "Delivery marked as delivered successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
