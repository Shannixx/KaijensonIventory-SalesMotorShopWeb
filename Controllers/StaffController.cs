using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class StaffController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly HashingService _hashing;
        private readonly ILogger<StaffController> _logger;

        public StaffController(ApplicationDbContext context, HashingService hashing, ILogger<StaffController> logger)
        {
            _context = context;
            _hashing = hashing;
            _logger = logger;
        }

        private IActionResult? CheckAdminAccess()
        {
            if (!IsSessionValid())
                return RedirectToLogin();
            if (!IsAdmin())
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }
            return null;
        }

        public async Task<IActionResult> Index(string? searchString, int page = 1)
        {
            var accessCheck = CheckAdminAccess();
            if (accessCheck != null) return accessCheck;

            try
            {
                int pageSize = 10;
                IQueryable<Staff> query = _context.Staff.AsNoTracking()
                    .Where(s => s.Status == Staff.ActiveStatus || s.Status == Staff.InactiveStatus);

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    string s = searchString.ToLower();
                    query = query.Where(s2 => s2.StaffName.ToLower().Contains(s) || s2.UserName.ToLower().Contains(s));
                }

                int total = await query.CountAsync();

                List<Staff> staff = await query
                    .OrderBy(s => s.StaffName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["CurrentFilter"] = searchString ?? "";
                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["CurrentStaffId"] = GetCurrentStaffId();

                return View(staff);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading staff list");
                TempData["ErrorMessage"] = "An error occurred while loading staff. Please try again.";
                return View(new List<Staff>());
            }
        }

        public async Task<IActionResult> Details(int? id)
        {
            var accessCheck = CheckAdminAccess();
            if (accessCheck != null) return accessCheck;

            if (id == null || id <= 0) return NotFound();

            try
            {
                Staff? staff = await _context.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.StaffId == id);
                if (staff == null) return NotFound();
                return View(staff);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading staff details for ID {StaffId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading staff details. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        public IActionResult Create()
        {
            var accessCheck = CheckAdminAccess();
            if (accessCheck != null) return accessCheck;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StaffCreateViewModel model)
        {
            var accessCheck = CheckAdminAccess();
            if (accessCheck != null) return accessCheck;

            try
            {
                // Additional validation: username uniqueness
                if (await _context.Staff.AnyAsync(s => s.UserName == model.UserName))
                {
                    ModelState.AddModelError("UserName", "Username already exists.");
                }

                if (ModelState.IsValid)
                {
                    var staff = new Staff
                    {
                        StaffName = model.StaffName,
                        UserName = model.UserName,
                        ContactNumber = model.ContactNumber,
                        Address = model.Address,
                        Role = model.Role,
                        PasswordHash = _hashing.HashPassword(model.Password),
                        Status = Staff.ActiveStatus
                    };

                    _context.Staff.Add(staff);
                    await _context.SaveChangesAsync();

                    await _context.ActivityLogs.AddAsync(new ActivityLog
                    {
                        Action = "Create Staff",
                        Module = "Staff",
                        Description = $"Staff {staff.StaffName} - created",
                        StaffId = GetCurrentStaffId(),
                        Timestamp = DateTime.Now
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Staff created successfully.";
                    return RedirectToAction(nameof(Index));
                }

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating staff");
                TempData["ErrorMessage"] = "An error occurred while creating staff. Please try again.";
                return View(model);
            }
        }

        public async Task<IActionResult> Edit(int? id)
        {
            var accessCheck = CheckAdminAccess();
            if (accessCheck != null) return accessCheck;

            if (id == null || id <= 0) return NotFound();

            try
            {
                Staff? staff = await _context.Staff.FindAsync(id);
                if (staff == null) return NotFound();
                return View(staff);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading staff for editing ID {StaffId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading staff for editing. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("StaffId,StaffName,UserName,ContactNumber,Address,Role")] Staff staff)
        {
            var accessCheck = CheckAdminAccess();
            if (accessCheck != null) return accessCheck;

            if (id != staff.StaffId) return NotFound();

            try
            {
                bool isSelf = GetCurrentStaffId() == id;

                if (isSelf)
                {
                    Staff? existing = await _context.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.StaffId == id);
                    if (existing != null && existing.Role != staff.Role)
                    {
                        ModelState.AddModelError("Role", "You cannot change your own role.");
                    }
                }

                if (string.IsNullOrWhiteSpace(staff.StaffName))
                {
                    ModelState.AddModelError("StaffName", "Staff name is required.");
                }

                if (string.IsNullOrWhiteSpace(staff.UserName))
                {
                    ModelState.AddModelError("UserName", "Username is required.");
                }
                else
                {
                    bool usernameExists = await _context.Staff.AnyAsync(s => s.UserName == staff.UserName && s.StaffId != id);
                    if (usernameExists)
                    {
                        ModelState.AddModelError("UserName", "Username already exists.");
                    }
                }

                if (ModelState.IsValid)
                {
                    Staff? existing = await _context.Staff.FindAsync(id);
                    if (existing == null) return NotFound();

                    string oldName = existing.StaffName;
                    string oldRole = existing.Role;

                    existing.StaffName = staff.StaffName;
                    existing.UserName = staff.UserName;
                    existing.ContactNumber = staff.ContactNumber;
                    existing.Address = staff.Address;
                    existing.Role = staff.Role;

                    await _context.SaveChangesAsync();

                    if (isSelf)
                    {
                        HttpContext.Session.SetString("StaffName", existing.StaffName);
                        HttpContext.Session.SetString("StaffRole", existing.Role);
                    }

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        Action = "Edit Staff",
                        Module = "Staff",
                        Description = $"Staff {oldName} -> {staff.StaffName}, Role: {oldRole} -> {staff.Role}",
                        StaffId = GetCurrentStaffId(),
                        Timestamp = DateTime.Now
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Staff updated successfully.";
                    return RedirectToAction(nameof(Index));
                }

                return View(staff);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error editing staff ID {StaffId}", id);
                if (!await _context.Staff.AnyAsync(s => s.StaffId == id))
                    return NotFound();
                TempData["ErrorMessage"] = "The staff record was modified by another user. Please try again.";
                return View(staff);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing staff ID {StaffId}", id);
                TempData["ErrorMessage"] = "An error occurred while updating staff. Please try again.";
                return View(staff);
            }
        }

        public async Task<IActionResult> Delete(int? id)
        {
            var accessCheck = CheckAdminAccess();
            if (accessCheck != null) return accessCheck;

            TempData["ErrorMessage"] = "Staff accounts are preserved. Use the Active/Inactive status control instead.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var accessCheck = CheckAdminAccess();
            if (accessCheck != null) return accessCheck;

            TempData["ErrorMessage"] = "Staff accounts are preserved. Use the Active/Inactive status control instead.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var accessCheck = CheckAdminAccess();
            if (accessCheck != null) return accessCheck;

            if (id <= 0) return NotFound();

            try
            {
                Staff? staff = await _context.Staff.FindAsync(id);
                if (staff == null) return NotFound();

                if (staff.StaffId == GetCurrentStaffId())
                {
                    TempData["ErrorMessage"] = "You cannot deactivate your own account.";
                    return RedirectToAction(nameof(Index));
                }

                string oldStatus = staff.Status;
                if (!string.Equals(staff.Status, Staff.ActiveStatus, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(staff.Status, Staff.InactiveStatus, StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = "This account has an invalid legacy status. Resolve it before changing the account state.";
                    return RedirectToAction(nameof(Index));
                }

                string newStatus = string.Equals(staff.Status, Staff.ActiveStatus, StringComparison.OrdinalIgnoreCase)
                    ? Staff.InactiveStatus
                    : Staff.ActiveStatus;

                if (newStatus == Staff.InactiveStatus &&
                    string.Equals(staff.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    int activeAdmins = await _context.Staff.CountAsync(s =>
                        s.Role == "Admin" && s.Status == Staff.ActiveStatus);
                    if (activeAdmins <= 1)
                    {
                        TempData["ErrorMessage"] = "Cannot deactivate the last active administrator account.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                staff.Status = newStatus;
                _context.ActivityLogs.Add(new ActivityLog
                {
                    Action = newStatus == Staff.ActiveStatus ? "Activate Staff" : "Deactivate Staff",
                    Module = "Staff",
                    Description = $"Staff {staff.StaffName} ({staff.StaffId}) status changed from {oldStatus} to {newStatus}.",
                    StaffId = GetCurrentStaffId(),
                    Timestamp = DateTime.Now
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Staff account {newStatus.ToLowerInvariant()} successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling status for staff ID {StaffId}", id);
                TempData["ErrorMessage"] = "An error occurred while updating the staff status. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> ChangePassword(int? id)
        {
            if (!IsSessionValid())
                return RedirectToLogin();

            if (id == null || id <= 0) return NotFound();

            try
            {
                Staff? staff = await _context.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.StaffId == id);
                if (staff == null) return NotFound();

                bool isSelf = GetCurrentStaffId() == id;
                bool isAdmin = IsAdmin();

                if (!isSelf && !isAdmin)
                {
                    TempData["ErrorMessage"] = "Access denied. You can only change your own password.";
                    return RedirectToAction("Index", "Dashboard");
                }

                ViewData["TargetStaffId"] = id;
                ViewData["TargetStaffName"] = staff.StaffName;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading password change form for staff ID {StaffId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading password change form. Please try again.";
                return RedirectToAction("Index", "Dashboard");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(int id, string CurrentPassword, string NewPassword, string ConfirmNewPassword)
        {
            if (!IsSessionValid())
                return RedirectToLogin();

            try
            {
                Staff? staff = await _context.Staff.FindAsync(id);
                if (staff == null) return NotFound();

                bool isSelf = GetCurrentStaffId() == id;
                bool isAdmin = IsAdmin();

                if (!isSelf && !isAdmin)
                {
                    TempData["ErrorMessage"] = "Access denied. You can only change your own password.";
                    return RedirectToAction("Index", "Dashboard");
                }

                if (isSelf)
                {
                    if (!_hashing.VerifyPassword(CurrentPassword ?? "", staff.PasswordHash))
                    {
                        ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                    }
                }

                if (string.IsNullOrWhiteSpace(NewPassword))
                {
                    ModelState.AddModelError("NewPassword", "New password is required.");
                }
                else if (NewPassword.Length < 6)
                {
                    ModelState.AddModelError("NewPassword", "New password must be at least 6 characters.");
                }
                else if (NewPassword != ConfirmNewPassword)
                {
                    ModelState.AddModelError("ConfirmNewPassword", "Passwords do not match.");
                }

                if (ModelState.IsValid)
                {
                     staff.PasswordHash = _hashing.HashPassword(NewPassword);
                     // Reset forced-change flag after successful password update
                     staff.MustChangePassword = false;
                     await _context.SaveChangesAsync();
 
                     // Clear forced change session flag
                     HttpContext.Session.Remove("MustChangePassword");
 
                     _context.ActivityLogs.Add(new ActivityLog
                     {
                         Action = "Change Password",
                         Module = "Staff",
                         Description = $"Password changed for staff {staff.StaffName}",
                         StaffId = GetCurrentStaffId(),
                         Timestamp = DateTime.Now
                     });
                     await _context.SaveChangesAsync();
 
                     TempData["SuccessMessage"] = "Password changed successfully.";
                     // Redirect to Dashboard after password change
                     return RedirectToAction("Index", "Dashboard");
                }

                ViewData["TargetStaffId"] = id;
                ViewData["TargetStaffName"] = staff.StaffName;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for staff ID {StaffId}", id);
                TempData["ErrorMessage"] = "An error occurred while changing password. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }
        // Admin resets a manager's password (offline flow)
        [HttpGet]
        public async Task<IActionResult> ResetPassword(int? id)
        {
            var accessCheck = CheckAdminAccess();
            if (accessCheck != null) return accessCheck;
            if (id == null) return NotFound();

            Staff? staff = await _context.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.StaffId == id);
            if (staff == null) return NotFound();
            if (staff.Role != "Manager")
            {
                TempData["ErrorMessage"] = "Can only reset password for Manager accounts.";
                return RedirectToAction(nameof(Index));
            }
            ViewData["TargetStaffId"] = id;
            ViewData["TargetStaffName"] = staff.StaffName;
            return View(); // view should contain fields for temporary password & confirm
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id, string TemporaryPassword, string ConfirmTemporaryPassword)
        {
            var accessCheck = CheckAdminAccess();
            if (accessCheck != null) return accessCheck;

            if (string.IsNullOrWhiteSpace(TemporaryPassword))
                ModelState.AddModelError("TemporaryPassword", "Temporary password is required.");
            else if (TemporaryPassword.Length < 6)
                ModelState.AddModelError("TemporaryPassword", "Password must be at least 6 characters.");
            if (TemporaryPassword != ConfirmTemporaryPassword)
                ModelState.AddModelError("ConfirmTemporaryPassword", "Passwords do not match.");

            if (!ModelState.IsValid)
            {
                ViewData["TargetStaffId"] = id;
                ViewData["TargetStaffName"] = (await _context.Staff.FindAsync(id))?.StaffName ?? "";
                return View();
            }

Staff? staff = await _context.Staff.FindAsync(id);
                     if (staff == null) return NotFound();
                     if (staff.Role != "Manager")
                     {
                         TempData["ErrorMessage"] = "Can only reset password for Manager accounts.";
                         return RedirectToAction(nameof(Index));
                     }

                     // Hash and set temporary password, enforce password change on next login
                     staff.PasswordHash = _hashing.HashPassword(TemporaryPassword);
                     staff.MustChangePassword = true;
            await _context.SaveChangesAsync();

            // Log activity without storing passwords
            _context.ActivityLogs.Add(new ActivityLog
            {
                Action = "Reset Password",
                Module = "Staff",
                Description = $"Administrator reset the password for Manager: {staff.StaffName}.",
                StaffId = GetCurrentStaffId(),
                Timestamp = DateTime.Now
            });
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Password reset successfully. Manager must change password on next login.";
            return RedirectToAction(nameof(Index));
        }
    }
}
