using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
                IQueryable<Staff> query = _context.Staff.AsNoTracking();

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
        public async Task<IActionResult> Create([Bind("StaffName,UserName,ContactNumber,Address,Role")] Staff staff, string Password, string ConfirmPassword)
        {
            var accessCheck = CheckAdminAccess();
            if (accessCheck != null) return accessCheck;

            try
            {
                if (string.IsNullOrWhiteSpace(Password))
                {
                    ModelState.AddModelError("Password", "Password is required.");
                }
                else if (Password != ConfirmPassword)
                {
                    ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
                }
                else if (Password.Length < 6)
                {
                    ModelState.AddModelError("Password", "Password must be at least 6 characters.");
                }

                if (string.IsNullOrWhiteSpace(staff.UserName))
                {
                    ModelState.AddModelError("UserName", "Username is required.");
                }
                else if (await _context.Staff.AnyAsync(s => s.UserName == staff.UserName))
                {
                    ModelState.AddModelError("UserName", "Username already exists.");
                }

                if (string.IsNullOrWhiteSpace(staff.StaffName))
                {
                    ModelState.AddModelError("StaffName", "Staff name is required.");
                }

                if (ModelState.IsValid)
                {
                    staff.PasswordHash = _hashing.HashPassword(Password);
                    if (string.IsNullOrEmpty(staff.Role)) staff.Role = "Manager";

                    _context.Staff.Add(staff);
                    await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
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

                return View(staff);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating staff");
                TempData["ErrorMessage"] = "An error occurred while creating staff. Please try again.";
                return View(staff);
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

            if (id == null || id <= 0) return NotFound();

            try
            {
                if (GetCurrentStaffId() == id)
                {
                    TempData["ErrorMessage"] = "You cannot delete your own account.";
                    return RedirectToAction(nameof(Index));
                }

                Staff? staff = await _context.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.StaffId == id);
                if (staff == null) return NotFound();

                return View(staff);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading staff for deletion ID {StaffId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading staff for deletion. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var accessCheck = CheckAdminAccess();
            if (accessCheck != null) return accessCheck;

            try
            {
                if (GetCurrentStaffId() == id)
                {
                    TempData["ErrorMessage"] = "You cannot delete your own account.";
                    return RedirectToAction(nameof(Index));
                }

                Staff? staff = await _context.Staff.FindAsync(id);
                if (staff == null) return NotFound();

                bool hasTransactions = await _context.SalesTransactions.AnyAsync(st => st.StaffId == id) ||
                                     await _context.ServiceTransactions.AnyAsync(st => st.StaffId == id) ||
                                     await _context.StockIns.AnyAsync(si => si.StaffId == id) ||
                                     await _context.PurchaseOrders.AnyAsync(po => po.StaffId == id);

                if (hasTransactions)
                {
                    TempData["ErrorMessage"] = "Cannot delete staff member. This staff has existing transactions.";
                    return RedirectToAction(nameof(Index));
                }

                string name = staff.StaffName;

                _context.Staff.Remove(staff);
                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    Action = "Delete Staff",
                    Module = "Staff",
                    Description = $"Staff {name} - deleted",
                    StaffId = GetCurrentStaffId(),
                    Timestamp = DateTime.Now
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Staff deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting staff ID {StaffId}", id);
                TempData["ErrorMessage"] = "An error occurred while deleting staff. Please try again.";
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
                    await _context.SaveChangesAsync();

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
                    return RedirectToAction(nameof(Index));
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
    }
}
