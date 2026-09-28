using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartScan.Data;
using SmartScan.Models;

namespace SmartScan.Controllers
{
    [Authorize(Roles = "Cashier")]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        // Constructor to receive the database context
        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return RedirectToAction(
                    "/Account/Login",
                    new { area = "Identity" }
                );
            }

            if (User.IsInRole("Admin"))
            {
                return RedirectToAction(
                    "Index",
                    "Users"
                );
            }

            if (User.IsInRole("Cashier"))
            {
                return RedirectToAction(
                    "Checkout",
                    "Home"
                );
            }

            return View();
        }

        // Allows both /Home/Checkout and /Checkout
        [HttpGet("/Checkout")]
        public IActionResult Checkout()
        {
            return View();
        }

        public IActionResult Payment()
        {
            return View();
        }

        // ------------------------------------
        // Get product details for the scanner
        // ------------------------------------

        [HttpGet]
        public async Task<IActionResult> GetProductDetails(
            string productName)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(
                    p => p.ProductName == productName
                );

            if (product == null)
            {
                return NotFound();
            }

            return Json(new
            {
                name = product.ProductName,
                price = product.Price
            });
        }

        // ------------------------------------
        // Error
        // ------------------------------------

        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true
        )]
        public IActionResult Error()
        {
            return View(
                new ErrorViewModel
                {
                    RequestId =
                        Activity.Current?.Id
                        ?? HttpContext.TraceIdentifier
                }
            );
        }
    }
}