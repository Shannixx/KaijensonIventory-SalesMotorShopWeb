using System.Diagnostics;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            // Validate session
            int? staffId = HttpContext.Session.GetInt32("StaffId");
            if (!staffId.HasValue)
                return RedirectToAction("Login", "Account");
            return RedirectToAction("Index", "Dashboard");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
