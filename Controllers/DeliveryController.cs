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
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }
            return null;
        }

        public async Task<IActionResult> Index(bool archived = false)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;
            if (archived && !IsAdmin()) return Forbid();

            var deliveries = await _deliveryService.GetAwaitingDeliveryAsync(archived);
            ViewBag.ShowArchived = archived;
            return View(deliveries);
        }

        public async Task<IActionResult> Details(int id, bool archived = false)
        {
            var accessCheck = CheckAccess();
            if (accessCheck != null) return accessCheck;
            if (archived && !IsAdmin()) return Forbid();

            var viewModel = await _deliveryService.GetDeliveryDetailsAsync(id, archived);
            if (viewModel == null) return NotFound();

            ViewBag.ShowArchived = archived;
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            var accessCheck = RedirectIfNotAdmin();
            if (accessCheck != null) return accessCheck;

            var result = await _deliveryService.ArchiveAsync(id, GetCurrentStaffId());
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault()?.Message ?? "Unable to archive delivery.";
                return RedirectToAction(nameof(Details), new { id, archived = true });
            }

            TempData["SuccessMessage"] = "Delivery archived without changing inventory.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var accessCheck = RedirectIfNotAdmin();
            if (accessCheck != null) return accessCheck;

            var result = await _deliveryService.RestoreAsync(id, GetCurrentStaffId());
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault()?.Message ?? "Unable to restore delivery.";
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] = "Delivery restored without replaying inventory.";
            return RedirectToAction(nameof(Details), new { id });
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

            var result = await _deliveryService.DeliverAsync(model.DeliveryId, model.ReceiveQuantities, GetCurrentStaffId(), model.Remarks);

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
