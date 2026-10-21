using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using DocumentFormat.OpenXml.Wordprocessing;
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

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            // If user is not logged in, just show the standard landing page
            if (!User.Identity.IsAuthenticated)
            {
                return View();
            }

            // Get the logged-in User's ID
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (User.IsInRole("Cashier"))
            {
                // Check if they are assigned to a store
                bool isAssigned = await _context.RetailStaffs.AnyAsync(rs => rs.StaffId == currentUserId);

                if (isAssigned)
                {
                    return RedirectToAction("Checkout", "Home");
                }
                return View();
            }

            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Index", "Users");
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Payment(string search)
        {
            ViewData["HeaderTitle"] = "Payment";

            if (!string.IsNullOrEmpty(search))
            {
                ViewData["CustomerName"] = search;
            }

            // Get logged in Cashier ID
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(currentUserId))
            {
                // If the user is a cashier, verify their assignment
                if (User.IsInRole("Cashier"))
                {
                    var staffAssignment = await _context.RetailStaffs
                        .Include(rs => rs.Retail)
                        .FirstOrDefaultAsync(rs => rs.StaffId == currentUserId);

                    if (staffAssignment == null || staffAssignment.Retail == null)
                    {
                        return RedirectToAction("Index", "Home");
                    }

                    ViewData["HeaderTitle"] = staffAssignment.Retail.RetailName;
                }
            }
            return View();
        }

        public async Task<IActionResult> CustomerDisplay()
        {
            // Default fallback title
            ViewData["HeaderTitle"] = "Customer View";

            // Get logged in Cashier ID
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(currentUserId))
            {
                // If the user is a cashier, verify their assignment
                if (User.IsInRole("Cashier"))
                {
                    var staffAssignment = await _context.RetailStaffs
                        .Include(rs => rs.Retail)
                        .FirstOrDefaultAsync(rs => rs.StaffId == currentUserId);

                    if (staffAssignment == null || staffAssignment.Retail == null)
                    {
                        return RedirectToAction("Index", "Home");
                    }

                    ViewData["HeaderTitle"] = staffAssignment.Retail.RetailName;
                }
            }
            return View();
        }

        public async Task<IActionResult> GenerateReceipt()
        {
            ViewData["HeaderTitle"] = "Payment";

            // Get logged in Cashier ID
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(currentUserId))
            {
                // If the user is a cashier, verify their assignment
                if (User.IsInRole("Cashier"))
                {
                    var staffAssignment = await _context.RetailStaffs
                        .Include(rs => rs.Retail)
                        .FirstOrDefaultAsync(rs => rs.StaffId == currentUserId);

                    if (staffAssignment == null || staffAssignment.Retail == null)
                    {
                        return RedirectToAction("Index", "Home");
                    }

                    ViewData["HeaderTitle"] = staffAssignment.Retail.RetailName;
                }
            }
            return View();
        }

        //GET: Display Checkout Page
        public async Task<IActionResult> Checkout()
        {
            // Default fallback title
            ViewData["HeaderTitle"] = "Checkout";

            // Get logged in Cashier ID
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(currentUserId))
            {
                // If the user is a cashier, verify their assignment
                if (User.IsInRole("Cashier"))
                {
                    var staffAssignment = await _context.RetailStaffs
                        .Include(rs => rs.Retail)
                        .FirstOrDefaultAsync(rs => rs.StaffId == currentUserId); 

                    if (staffAssignment == null || staffAssignment.Retail == null)
                    {
                        return RedirectToAction("Index", "Home");
                    }

                    ViewData["HeaderTitle"] = staffAssignment.Retail.RetailName;
                }
            }
            return View();
        }

        // GET: Product Details
        [HttpGet]
        public async Task<IActionResult> GetProductDetails(string productName)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductName == productName);

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

        [AllowAnonymous]
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
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                }
            );
        }

        // -------------------- [CUSTOMER VERIFICATION] --------------------
        
        // GET: Customer Verification Page
        [HttpGet]
        public async Task<IActionResult> CustomerVerification()
        {
            // Default fallback title
            ViewData["HeaderTitle"] = "Customer Verification";

            // Get logged in Cashier ID
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(currentUserId))
            {
                // If the user is a cashier, verify their assignment
                if (User.IsInRole("Cashier"))
                {
                    var staffAssignment = await _context.RetailStaffs
                        .Include(rs => rs.Retail)
                        .FirstOrDefaultAsync(rs => rs.StaffId == currentUserId);

                    if (staffAssignment == null || staffAssignment.Retail == null)
                    {
                        return RedirectToAction("Index", "Home");
                    }

                    ViewData["HeaderTitle"] = staffAssignment.Retail.RetailName;
                }
            }
            return View();
        }

        // POST: Authenticate Face
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AuthenticateFace([FromBody] FaceAuthRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.CapturedEmbeddingJson))
            {
                return Json(new { success = false, message = "No facial biometric data captured." });
            }

            float[]? capturedVector;
            try
            {
                capturedVector = JsonSerializer.Deserialize<float[]>(request.CapturedEmbeddingJson);
                if (capturedVector == null || capturedVector.Length != 128)
                {
                    return Json(new { success = false, message = "Invalid face embedding dimensions. Must be 128 features." });
                }
            }
            catch
            {
                return Json(new { success = false, message = "Malformed biometric data." });
            }

            // Use AsNoTracking() so EF Core doesn't maintain tracking graphs for the embeddings
            var registeredCustomers = await _context.Customers
                .AsNoTracking()
                .Where(c => c.FaceEmbedding != null && c.FaceEmbedding != "").Select(c => new
                {
                    c.CustomerId,
                    c.FullName,
                    c.Age,
                    c.FaceEmbedding
                })
                .ToListAsync();

            if (!registeredCustomers.Any())
            {
                return Json(new { success = false, message = "Access Denied: No customers are enrolled in the biometric registry." });
            }

            CustomerMatchResult? bestMatch = null;
            double lowestEuclideanDistance = double.MaxValue;
            double bestSimilarity = -1.0;

            const double MAX_EUCLIDEAN_DISTANCE = 0.55;
            const double MIN_COSINE_SIMILARITY = 0.85;

            // Fast-fail loop
            foreach (var customer in registeredCustomers)
            {
                try
                {
                    var storedVector = JsonSerializer.Deserialize<float[]>(customer.FaceEmbedding!);
                    if (storedVector == null || storedVector.Length != 128) continue;

                    double distance = ComputeEuclideanDistance(capturedVector, storedVector);

                    // Skip computing cosine similarity if Euclidean distance already fails
                    if (distance > MAX_EUCLIDEAN_DISTANCE) continue;

                    double similarity = ComputeCosineSimilarity(capturedVector, storedVector);

                    if (distance < lowestEuclideanDistance)
                    {
                        lowestEuclideanDistance = distance;
                        bestSimilarity = similarity;
                        bestMatch = new CustomerMatchResult
                        {
                            CustomerId = customer.CustomerId,
                            FullName = customer.FullName,
                            Age = customer.Age,
                            Distance = distance,
                            SimilarityScore = similarity
                        };
                    }
                }
                catch
                {
                    continue;
                }
            }

            if (bestMatch != null &&
                bestMatch.Distance <= MAX_EUCLIDEAN_DISTANCE &&
                bestMatch.SimilarityScore >= MIN_COSINE_SIMILARITY)
            {
                TempData["Success"] = $"Verified Customer: {bestMatch.FullName}";

                // Check age directly
                bool hasAge = bestMatch.Age.HasValue && bestMatch.Age.Value > 0;
                bool isUnderage = hasAge && bestMatch.Age.Value < 18;

                return Json(new
                {
                    success = true,
                    customerId = bestMatch.CustomerId,
                    customerName = bestMatch.FullName,
                    confidence = $"{(bestMatch.SimilarityScore * 100):0.0}%",
                    hasAge = hasAge,
                    age = bestMatch.Age,
                    isUnderage = isUnderage,
                    redirectUrl = Url.Action("Payment", "Home", new { search = bestMatch.FullName })
                });
            }

            return Json(new
            {
                success = false,
                message = "Face not recognized. This person is not enrolled in the customer registry."
            });
        }

        // POST: /Home/NationalIDVerification
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult NationalIDVerification([FromBody] GovIdVerifyRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.RawPayload) || string.IsNullOrWhiteSpace(req.ManualDob))
            {
                return Json(new { isValid = false, message = "PCN and Date of Birth are required." });
            }

            string digitsOnly = new string(req.RawPayload.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length != 16)
            {
                return Json(new { isValid = false, message = "PCN must be exactly 16 digits long." });
            }

            if (!DateTime.TryParse(req.ManualDob, out DateTime dob))
            {
                return Json(new { isValid = false, message = "Invalid Date of Birth format." });
            }

            // Age calculation matching exact birthday passage
            int age = DateTime.UtcNow.Year - dob.Year;
            if (DateTime.UtcNow.Date < dob.Date.AddYears(age)) age--;

            if (age < 5 || age > 120)
            {
                return Json(new { isValid = false, message = "Age calculation resulted in an invalid range. Please check the DOB." });
            }

            bool isUnderage = age < 18;
            int statusId = isUnderage ? 3 : 1;

            return Json(new
            {
                isValid = true,
                idNumber = digitsOnly, // Returned solely for UI masking; never stored
                age = age,
                isUnderage = isUnderage,
                assignedStatusId = statusId
            });
        }

        // -------------------- [TRANSACTION & INVENTORY] --------------------

        [HttpPost]
        public async Task<IActionResult> SaveTransaction([FromBody] TransactionPayload request)
        {
            if (request == null || !request.Items.Any())
            {
                return Json(new { success = false, message = "Cart is empty." });
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Json(new { success = false, message = "Session expired. Please log in again." });
            }

            var retailStaff = await _context.RetailStaffs
                .FirstOrDefaultAsync(rs => rs.StaffId == currentUserId);

            if (retailStaff == null)
            {
                return Json(new { success = false, message = "Error: Your account is not assigned to a retail store." });
            }

            int currentRetailId = retailStaff.RetailId;
            int currentRetailStaffId = retailStaff.RetailStaffId;

            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // --- 1. PRE-CHECK STOCK BEFORE SAVING ANYTHING ---
                foreach (var item in request.Items)
                {
                    var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductName == item.Name);
                    if (product == null) return Json(new { success = false, message = $"Product '{item.Name}' not found." });

                    var inventory = await _context.Inventories
                        .FirstOrDefaultAsync(i => i.ProductId == product.ProductId && i.RetailId == currentRetailId);

                    if (inventory == null)
                        return Json(new { success = false, message = $"Product '{product.ProductName}' does not exist in this store's inventory." });

                    if (inventory.Stock < item.Qty)
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"Insufficient stock for '{product.ProductName}'. You tried to checkout {item.Qty}, but only {inventory.Stock} are left."
                        });
                    }
                }

                // --- 2. IF STOCK IS GOOD, CREATE TRANSACTION ---
                string receiptNum = "REC" + DateTime.Now.ToString("yyyyMMddHHmmss");

                // Map to the exact IDs in your SQL Database
                int paymentId = 1; // Default to Cash
                if (request.PaymentMethod == "Cash In Hand") paymentId = 1;
                else if (request.PaymentMethod == "GCash API") paymentId = 2;
                else if (request.PaymentMethod == "Credit Card") paymentId = 3;
                else if (request.PaymentMethod == "Store Credits") paymentId = 4;
                var newTx = new Transaction
                {
                    RetailStaffId = currentRetailStaffId,
                    CustomerId = 2, // Note: If Customer 2 doesn't exist, this will throw an inner exception
                    RetailId = currentRetailId,
                    TotalAmount = request.Total,
                    PaymentMethodId = paymentId,
                    StatusId = 1,
                    CreatedAt = DateTime.Now,
                    ReceiptNumber = receiptNum
                };

                _context.Transactions.Add(newTx);
                await _context.SaveChangesAsync();

                // --- 3. DEDUCT STOCK AND ADD ITEMS ---
                foreach (var item in request.Items)
                {
                    var product = await _context.Products.FirstAsync(p => p.ProductName == item.Name);

                    var txItem = new TransactionItem
                    {
                        TransactionId = newTx.TransactionId,
                        ProductId = product.ProductId,
                        Quantity = item.Qty,
                        UnitPrice = item.Price
                    };
                    _context.TransactionItems.Add(txItem);

                    var inventory = await _context.Inventories
                        .FirstAsync(i => i.ProductId == product.ProductId && i.RetailId == currentRetailId);

                    inventory.Stock -= item.Qty;
                    _context.Inventories.Update(inventory);
                }

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                return Json(new { success = true, receiptNumber = receiptNum });
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();

                // This grabs the exact SQL error so we know what is missing!
                string errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = errorMessage });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetOldTransactions()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var retailStaff = await _context.RetailStaffs.FirstOrDefaultAsync(rs => rs.StaffId == currentUserId);

            if (retailStaff == null) return Json(new { success = false });

            var transactions = await _context.Transactions
                .Where(t => t.RetailId == retailStaff.RetailId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5) // Shows the 5 most recent receipts
                .Select(t => new {
                    receiptNumber = t.ReceiptNumber,
                    date = t.CreatedAt.ToString("MM/dd/yyyy hh:mm tt"),
                    total = t.TotalAmount
                })
                .ToListAsync();

            return Json(new { success = true, transactions });
        }

        // -------------------- [BIOMETRICS HELPERS] --------------------

        private static double ComputeEuclideanDistance(float[] vA, float[] vB)
        {
            double sum = 0.0;
            for (int i = 0; i < vA.Length; i++)
            {
                double diff = vA[i] - vB[i];
                sum += diff * diff;
            }
            return Math.Sqrt(sum);
        }

        private static double ComputeCosineSimilarity(float[] vA, float[] vB)
        {
            double sum = 0.0;
            double normA = 0.0;
            double normB = 0.0;
            for (int i = 0; i < vA.Length; i++)
            {
                sum += (double)vA[i] * vB[i];
                normA += (double)vA[i] * vA[i];
                normB += (double)vB[i] * vB[i];
            }
            return normA == 0.0 || normB == 0.0 ? 0.0 : sum / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }  
    }

    // -------------------- [TRANSACTION DTOs] --------------------
    public class TransactionPayload
    {
        public List<CartItemPayload> Items { get; set; } = new();
        public decimal Total { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
    }

    public class CartItemPayload
    {
        public string Name { get; set; } = string.Empty;
        public int Qty { get; set; }
        public decimal Price { get; set; }
    }

    public class GovIdVerifyRequest
    {
        public string? RawPayload { get; set; }
        public string? ManualDob { get; set; }
    }
    public class FaceAuthRequest
    {
        public string CapturedEmbeddingJson { get; set; } = string.Empty;
    }

    public class CustomerMatchResult
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public int? Age { get; set; }
        public double Distance { get; set; }
        public double SimilarityScore { get; set; }
    }
}