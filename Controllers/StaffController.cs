using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class StaffController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly HashingService _hashing;

        public StaffController(ApplicationDbContext context, HashingService hashing)
        {
            _context = context;
            _hashing = hashing;
        }

        public async Task<IActionResult> Index(string? searchString, int page = 1)
        {
            // Validate session and role (Admin only)
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            string? staffRole = HttpContext.Session.GetString("StaffRole");
            
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }
            
            if (!string.Equals(staffRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }

            try
            {
                int pageSize = 10;
                IQueryable<Staff> query = _context.Staff.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    query = query.Where(s => s.StaffName.Contains(searchString) || s.UserName.Contains(searchString));
                }

                int total = await query.CountAsync();

                List<Staff> staff = await query
                    .OrderBy(s => s.StaffName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["CurrentFilter"] = searchString;
                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["CurrentStaffId"] = staffId;

                return View(staff);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading staff. Please try again.";
                return View(new List<Staff>());
            }
        }

        public async Task<IActionResult> Details(int? id)
        {
            // Validate session and role (Admin only)
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            string? staffRole = HttpContext.Session.GetString("StaffRole");
            
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }
            
            if (!string.Equals(staffRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }

            if (id == null) return NotFound();

            try
            {
                Staff? staff = await _context.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.StaffId == id);
                if (staff == null) return NotFound();

                return View(staff);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading staff details. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        public IActionResult Create()
        {
            // Validate session and role (Admin only)
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            string? staffRole = HttpContext.Session.GetString("StaffRole");
            
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }
            
            if (!string.Equals(staffRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("StaffName,UserName,ContactNumber,Address,Role")] Staff staff, string Password, string ConfirmPassword)
        {
            // Validate session and role (Admin only)
            int? currentStaffId = HttpContext.Session.GetInt32("StaffId");
            string? staffRole = HttpContext.Session.GetString("StaffRole");
            
            if (!currentStaffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }
            
            if (!string.Equals(staffRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }

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
                        StaffId = currentStaffId,
                        Timestamp = DateTime.Now
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Staff created successfully.";
                    return RedirectToAction(nameof(Index));
                }

                return View(staff);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while creating staff. Please try again.";
                return View(staff);
            }
        }

        public async Task<IActionResult> Edit(int? id)
        {
            // Validate session and role (Admin only)
            int? currentStaffId = HttpContext.Session.GetInt32("StaffId");
            string? staffRole = HttpContext.Session.GetString("StaffRole");
            
            if (!currentStaffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }
            
            if (!string.Equals(staffRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }

            if (id == null) return NotFound();

            try
            {
                Staff? staff = await _context.Staff.FindAsync(id);
                if (staff == null) return NotFound();

                return View(staff);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading staff for editing. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("StaffId,StaffName,UserName,ContactNumber,Address,Role")] Staff staff)
        {
            // Validate session and role (Admin only)
            int? currentStaffId = HttpContext.Session.GetInt32("StaffId");
            string? staffRole = HttpContext.Session.GetString("StaffRole");
            
            if (!currentStaffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }
            
            if (!string.Equals(staffRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }

            if (id != staff.StaffId) return NotFound();

            bool isSelf = currentStaffId == id;

            if (isSelf)
            {
                Staff? existing = await _context.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.StaffId == id);
                if (existing != null && existing.Role != staff.Role)
                {
                    ModelState.AddModelError("Role", "You cannot change your own role.");
                }
            }

            // Validate required fields
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
                // Check if username is already taken by another staff member
                bool usernameExists = await _context.Staff.AnyAsync(s => s.UserName == staff.UserName && s.StaffId != id);
                if (usernameExists)
                {
                    ModelState.AddModelError("UserName", "Username already exists.");
                }
            }

            if (ModelState.IsValid)
            {
                try
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

                    // Update session if editing self
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
                        StaffId = currentStaffId,
                        Timestamp = DateTime.Now
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Staff updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Staff.AnyAsync(s => s.StaffId == id))
                        return NotFound();

                    TempData["ErrorMessage"] = "The staff record was modified by another user. Please try again.";
                    return View(staff);
                }
                catch
                {
                    TempData["ErrorMessage"] = "An error occurred while updating staff. Please try again.";
                    return View(staff);
                }
            }

            return View(staff);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            // Validate session and role (Admin only)
            int? currentStaffId = HttpContext.Session.GetInt32("StaffId");
            string? staffRole = HttpContext.Session.GetString("StaffRole");
            
            if (!currentStaffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }
            
            if (!string.Equals(staffRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }

            if (id == null) return NotFound();

            try
            {
                // Prevent self-deletion
                if (currentStaffId == id)
                {
                    TempData["ErrorMessage"] = "You cannot delete your own account.";
                    return RedirectToAction(nameof(Index));
                }

                Staff? staff = await _context.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.StaffId == id);
                if (staff == null) return NotFound();

                return View(staff);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading staff for deletion. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // Validate session and role (Admin only)
            int? currentStaffId = HttpContext.Session.GetInt32("StaffId");
            string? staffRole = HttpContext.Session.GetString("StaffRole");
            
            if (!currentStaffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }
            
            if (!string.Equals(staffRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Dashboard");
            }

            // Prevent self-deletion
            if (currentStaffId == id)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                Staff? staff = await _context.Staff.FindAsync(id);
                if (staff == null) return NotFound();

                // Check if staff has any transactions
                bool hasTransactions = await _context.SalesTransactions.AnyAsync(st => st.StaffId == id) ||
                                     await _context.ServiceTransactions.AnyAsync(st => st.StaffId == id) ||
                                     await _context.StockIns.AnyAsync(si => si.StaffId == id);

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
                    StaffId = currentStaffId,
                    Timestamp = DateTime.Now
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Staff deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while deleting staff. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> ChangePassword(int? id)
        {
            // Validate session
            int? currentStaffId = HttpContext.Session.GetInt32("StaffId");
            if (!currentStaffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            if (id == null) return NotFound();

            try
            {
                Staff? staff = await _context.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.StaffId == id);
                if (staff == null) return NotFound();

                // Only allow self-password change or admin changing any password
                string? staffRole = HttpContext.Session.GetString("StaffRole");
                bool isSelf = currentStaffId == id;
                bool isAdmin = string.Equals(staffRole, "Admin", StringComparison.OrdinalIgnoreCase);
                
                if (!isSelf && !isAdmin)
                {
                    TempData["ErrorMessage"] = "Access denied. You can only change your own password.";
                    return RedirectToAction("Index", "Dashboard");
                }

                ViewData["TargetStaffId"] = id;
                ViewData["TargetStaffName"] = staff.StaffName;
                return View();
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading password change form. Please try again.";
                return RedirectToAction("Index", "Dashboard");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(int id, string CurrentPassword, string NewPassword, string ConfirmNewPassword)
        {
            // Validate session
            int? currentStaffId = HttpContext.Session.GetInt32("StaffId");
            if (!currentStaffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                Staff? staff = await _context.Staff.FindAsync(id);
                if (staff == null) return NotFound();

                // Only allow self-password change or admin changing any password
                string? staffRole = HttpContext.Session.GetString("StaffRole");
                bool isSelf = currentStaffId == id;
                bool isAdmin = string.Equals(staffRole, "Admin", StringComparison.OrdinalIgnoreCase);
                
                if (!isSelf && !isAdmin)
                {
                    TempData["ErrorMessage"] = "Access denied. You can only change your own password.";
                    return RedirectToAction("Index", "Dashboard");
                }

                // For self-password change, verify current password
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
                        StaffId = currentStaffId,
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
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while changing password. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
