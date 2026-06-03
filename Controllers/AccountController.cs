using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.Services;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly HashingService _hashing;

        public AccountController(ApplicationDbContext context, HashingService hashing)
        {
            _context = context;
            _hashing = hashing;
        }

        public IActionResult Login()
        {
            // Clear any existing session to ensure clean login
            HttpContext.Session.Clear();
            
            // Check if already logged in (shouldn't happen due to Clear above, but just in case)
            if (HttpContext.Session.GetInt32("StaffId") != null)
                return RedirectToAction("Index", "Dashboard");
                
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                // Sanitize input
                if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
                {
                    ModelState.AddModelError("", "Please enter both username and password.");
                    return View(model);
                }

                string hashed = _hashing.HashData(model.Password);
                Staff? staff = await _context.Staff
                    .FirstOrDefaultAsync(s => s.UserName == model.Username && s.PasswordHash == hashed);

                if (staff == null)
                {
                    // Log failed login attempt (in a real app, implement rate limiting)
                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        Action = "Failed Login",
                        Module = "Auth",
                        Description = $"Failed login attempt for username: {model.Username}"
                    });
                    await _context.SaveChangesAsync();

                    ModelState.AddModelError("", "Invalid username or password.");
                    return View(model);
                }

                HttpContext.Session.SetInt32("StaffId", staff.StaffId);
                HttpContext.Session.SetString("StaffName", staff.StaffName);
                HttpContext.Session.SetString("StaffRole", staff.Role);

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = staff.StaffId,
                    Action = "Login",
                    Module = "Auth",
                    Description = $"Staff {staff.StaffName} logged in."
                });
                await _context.SaveChangesAsync();

                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception ex)
            {
                // Log the exception in a real application
                ModelState.AddModelError("", "An error occurred during login. Please try again.");
                return View(model);
            }
        }

        public IActionResult Register() => View();

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                // Additional validation
                if (string.IsNullOrWhiteSpace(model.StaffName) || 
                    string.IsNullOrWhiteSpace(model.Username) || 
                    string.IsNullOrWhiteSpace(model.Password))
                {
                    ModelState.AddModelError("", "All fields are required.");
                    return View(model);
                }

                // Check password strength
                if (model.Password.Length < 6)
                {
                    ModelState.AddModelError("", "Password must be at least 6 characters long.");
                    return View(model);
                }

                if (await _context.Staff.AnyAsync(s => s.UserName == model.Username))
                {
                    ModelState.AddModelError("", "Username already exists.");
                    return View(model);
                }

                Staff staff = new()
                {
                    StaffName = model.StaffName.Trim(),
                    UserName = model.Username.Trim(),
                    PasswordHash = _hashing.HashData(model.Password),
                    Role = "Manager"
                };

                _context.Staff.Add(staff);
                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    Action = "Register",
                    Module = "Auth",
                    Description = $"New staff registered: {staff.StaffName} ({staff.UserName})"
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Registration successful. Please log in.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                // Log the exception in a real application
                ModelState.AddModelError("", "An error occurred during registration. Please try again.");
                return View(model);
            }
        }

        public IActionResult Logout()
        {
            try
            {
                int? staffId = HttpContext.Session.GetInt32("StaffId");
                string staffName = HttpContext.Session.GetString("StaffName") ?? "Unknown";

                if (staffId != null)
                {
                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        StaffId = staffId,
                        Action = "Logout",
                        Module = "Auth",
                        Description = $"Staff {staffName} logged out."
                    });
                    _context.SaveChanges();
                }

                HttpContext.Session.Clear();
                TempData["SuccessMessage"] = "You have been logged out successfully.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                // Even if logging fails, still clear the session
                HttpContext.Session.Clear();
                TempData["SuccessMessage"] = "You have been logged out successfully.";
                return RedirectToAction("Login");
            }
        }
    }
}
