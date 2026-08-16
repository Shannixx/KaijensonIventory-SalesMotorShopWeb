using Microsoft.AspNetCore.Mvc;
using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class SerialController : BaseController
    {
        private readonly ApplicationDbContext _context;
        public SerialController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Serial/Lookup?serialNumber=XYZ
        public async Task<IActionResult> Lookup(string serialNumber)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;

            if (string.IsNullOrWhiteSpace(serialNumber))
                return BadRequest("Serial number is required.");

            var serial = await _context.SerialUnits
                .Include(s => s.Product)
                .Include(s => s.SalesTransaction)
                .FirstOrDefaultAsync(s => s.SerialNumber == serialNumber);

            if (serial == null)
                return NotFound($"Serial number {serialNumber} not found.");

            var result = new
            {
                serial.SerialNumber,
                Product = new { serial.Product.ProductId, serial.Product.ProductName },
                Status = serial.Status,
                Sale = serial.SalesTransaction == null ? null : new { serial.SalesTransaction.TransactionId, serial.SalesTransaction.TransactionDate, serial.SalesTransaction.StaffId },
                CreatedDate = serial.CreatedDate,
                SoldDate = serial.SoldDate
            };

            return Json(result);
        }
    }
}
