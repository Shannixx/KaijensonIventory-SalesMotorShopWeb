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
            string? role = HttpContext.Session.GetString("StaffRole");
            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        // Admin or Manager role check (no Owner)
        protected bool IsOwnerOrManager()
                {
                    string? role = HttpContext.Session.GetString("StaffRole");
                    return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);
                }

        protected IActionResult? RedirectIfNotOwnerOrManager()
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;
            if (!IsOwnerOrManager())
                            {
                                TempData["ErrorMessage"] = "Access denied. Admin or Manager privileges required.";
                                return RedirectToAction("Index", "Dashboard");
                            }
            return null;
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
            // Enforce forced password change if flag set in session
            var mustChange = HttpContext.Session.GetString("MustChangePassword");
            if (string.Equals(mustChange, "true", StringComparison.OrdinalIgnoreCase))
            {
                // Allow access to ChangePassword actions only
                var path = HttpContext.Request.Path.Value ?? string.Empty;
                if (!path.Contains("ChangePassword", StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction("ChangePassword", "Staff", new { id = GetCurrentStaffId() });
                }
            }
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
