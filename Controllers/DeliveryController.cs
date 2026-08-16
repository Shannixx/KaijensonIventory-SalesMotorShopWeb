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
                TempData["ErrorMessage"] = "Access denied. Owner or Manager privileges required.";
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
    }
}
