using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CustomersController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? searchString, int page = 1)
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                int pageSize = 10;
                IQueryable<Customer> query = _context.Customers.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    string s = searchString.ToLower();
                    query = query.Where(c => c.CustomerName.ToLower().Contains(s)
                        || (c.ContactNumber != null && c.ContactNumber.Contains(s))
                        || (c.Email != null && c.Email.ToLower().Contains(s)));
                }

                int total = await query.CountAsync();
                List<Customer> customers = await query
                    .OrderByDescending(c => c.LastPurchaseDate)
                    .ThenBy(c => c.CustomerName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);
                ViewData["CurrentFilter"] = searchString;

                return View(customers);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading customers. Please try again.";
                return View(new List<Customer>());
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                Customer? customer = await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CustomerId == id);

                if (customer == null) return NotFound();

                ViewBag.RecentSales = await _context.SalesTransactions
                    .Include(t => t.Staff)
                    .Where(t => t.CustomerId == id)
                    .OrderByDescending(t => t.TransactionDate)
                    .Take(10)
                    .AsNoTracking()
                    .ToListAsync();

                ViewBag.RecentServices = await _context.ServiceTransactions
                    .Include(t => t.Mechanic)
                    .Where(t => t.CustomerId == id)
                    .OrderByDescending(t => t.Date)
                    .Take(10)
                    .AsNoTracking()
                    .ToListAsync();

                return View(customer);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading customer details. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        public IActionResult Create()
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                if (string.IsNullOrWhiteSpace(customer.CustomerName))
                    ModelState.AddModelError("CustomerName", "Customer name is required.");

                if (ModelState.IsValid)
                {
                    _context.Customers.Add(customer);
                    await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        StaffId = staffId,
                        Action = "Create",
                        Module = "Customers",
                        Description = $"Created customer: {customer.CustomerName}"
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Customer created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                return View(customer);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while creating the customer. Please try again.";
                return View(customer);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                Customer? customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == id);
                if (customer == null) return NotFound();
                return View(customer);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading the customer for editing. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer customer)
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            if (id != customer.CustomerId) return NotFound();

            try
            {
                if (string.IsNullOrWhiteSpace(customer.CustomerName))
                    ModelState.AddModelError("CustomerName", "Customer name is required.");

                if (ModelState.IsValid)
                {
                    Customer? existing = await _context.Customers.FindAsync(id);
                    if (existing == null) return NotFound();

                    existing.CustomerName = customer.CustomerName;
                    existing.ContactNumber = customer.ContactNumber;
                    existing.Email = customer.Email;
                    existing.Address = customer.Address;
                    existing.Notes = customer.Notes;

                    await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        StaffId = staffId,
                        Action = "Edit",
                        Module = "Customers",
                        Description = $"Edited customer: {customer.CustomerName}"
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Customer updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                return View(customer);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while updating the customer. Please try again.";
                return View(customer);
            }
        }

        public async Task<IActionResult> Delete(int id)
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                Customer? customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == id);
                if (customer == null) return NotFound();

                bool hasSales = await _context.SalesTransactions.AnyAsync(t => t.CustomerId == id);
                bool hasServices = await _context.ServiceTransactions.AnyAsync(t => t.CustomerId == id);
                if (hasSales || hasServices)
                {
                    TempData["ErrorMessage"] = "Cannot delete customer. This customer has existing transactions.";
                    return RedirectToAction(nameof(Index));
                }

                return View(customer);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading the customer for deletion. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                Customer? customer = await _context.Customers.FindAsync(id);
                if (customer == null) return NotFound();

                string name = customer.CustomerName;
                _context.Customers.Remove(customer);

                _context.ActivityLogs.Add(new ActivityLog
                {
                    StaffId = staffId,
                    Action = "Delete",
                    Module = "Customers",
                    Description = $"Deleted customer: {name}"
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Customer deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the customer. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
