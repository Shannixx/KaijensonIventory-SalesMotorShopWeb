using Microsoft.AspNetCore.Mvc;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public abstract class BaseController : Controller
    {
        protected bool IsSessionValid()
        {
            return HttpContext.Session.GetInt32("StaffId").HasValue;
        }

        protected IActionResult RedirectToLogin()
        {
            TempData["ErrorMessage"] = "Session expired. Please log in again.";
            return RedirectToAction("Login", "Account");
        }

        protected bool IsAdmin()
        {
            return string.Equals(HttpContext.Session.GetString("StaffRole"), "Admin", StringComparison.OrdinalIgnoreCase);
        }

        protected int GetCurrentStaffId()
        {
            return HttpContext.Session.GetInt32("StaffId") ?? 0;
        }

        protected string GetCurrentStaffName()
        {
            return HttpContext.Session.GetString("StaffName") ?? "Unknown";
        }

        protected IActionResult? RedirectIfNotAuthenticated()
        {
            if (!IsSessionValid())
                return RedirectToLogin();
            return null;
        }

        protected IActionResult? RedirectIfNotAdmin()
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;
            if (!IsAdmin())
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }
            return null;
        }
    }
}
