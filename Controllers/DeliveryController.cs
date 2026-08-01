using KaijensonIventory_SalesMotorShopWeb.Services;
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
        public async Task<IActionResult> MarkDelivered(int id)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;

            var result = await _deliveryService.DeliverAsync(id, GetCurrentStaffId());

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault()?.Message
                    ?? "An error occurred while marking the purchase order as delivered. Please try again.";
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["SuccessMessage"] = "Purchase order marked as delivered. Stock updated.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
