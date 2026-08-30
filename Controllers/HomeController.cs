using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Added for Database queries
using SmartScan.Data;                // Added to access ApplicationDbContext
using SmartScan.Models;

namespace SmartScan.Controllers
{
    [Authorize(Roles = "Cashier")]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context; // 1. Added the database context variable

        // 2. Updated constructor to receive the database context
        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("Admin"))
                {
                    return RedirectToAction("Index", "Users", new { area = "" });
                }

                if (User.IsInRole("Cashier"))
                {
                    return View();
                }
            }
            return View();
        }

        public IActionResult Checkout()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // 3. ADDED YOUR NEW CHECKOUT SCANNER ENDPOINT HERE
        [HttpGet]
        public async Task<IActionResult> GetProductDetails(string productName)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductName == productName);

            if (product == null)
            {
                return NotFound();
            }

            return Json(new { name = product.ProductName, price = product.Price });
        }
    }
}