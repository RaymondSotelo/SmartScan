using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartScan.Data;
using SmartScan.Models;
using System.Security.Claims;

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
        public IActionResult Index()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Redirect("/Identity/Account/Login");
            }

            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Index", "Users");
            }

            if (User.IsInRole("Cashier"))
            {
                return RedirectToAction(nameof(Checkout));
            }

            return View();
        }

        public IActionResult Checkout()
        {
            return View();
        }

        public IActionResult Payment()
        {
            return View();
        }

        public IActionResult CustomerDisplay()
        {
            return View();
        }

        public IActionResult GenerateReceipt()
        {
            return View();
        }

        // ------------------------------------
        // Get product details for the scanner
        // ------------------------------------

        [HttpGet]
        public async Task<IActionResult> GetProductDetails(string productName)
        {
            if (string.IsNullOrWhiteSpace(productName))
            {
                return NotFound();
            }

            string normalizedName = productName.Trim().ToLower().Replace(" ", "_");
            string rawNameLower = productName.Trim().ToLower();

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductName.ToLower() == normalizedName || p.ProductName.ToLower() == rawNameLower);

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

        [HttpGet]
        public IActionResult CustomerVerification()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Cashier")]
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

            var registeredCustomers = await _context.Customers
                .AsNoTracking()
                .Where(c => c.FaceEmbedding != null && c.FaceEmbedding != "").Select(c => new
                {
                    c.CustomerId,
                    c.FullName,
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

            foreach (var customer in registeredCustomers)
            {
                try
                {
                    var storedVector = JsonSerializer.Deserialize<float[]>(customer.FaceEmbedding!);
                    if (storedVector == null || storedVector.Length != 128) continue;

                    double distance = ComputeEuclideanDistance(capturedVector, storedVector);

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

                return Json(new
                {
                    success = true,
                    customerId = bestMatch.CustomerId,
                    customerName = bestMatch.FullName,
                    confidence = $"{(bestMatch.SimilarityScore * 100):0.0}%",
                    redirectUrl = Url.Action("Payment", "Home", new { search = bestMatch.FullName })
                });
            }

            return Json(new
            {
                success = false,
                message = "Face not recognized. This person is not enrolled in the customer registry."
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
                // --- 1. PRE-CHECK STOCK ---
                foreach (var item in request.Items)
                {
                    string normalizedItemName = item.Name.Trim().ToLower().Replace(" ", "_");
                    string rawItemLower = item.Name.Trim().ToLower();

                    var product = await _context.Products.FirstOrDefaultAsync(p =>
                        p.ProductName.ToLower() == normalizedItemName || p.ProductName.ToLower() == rawItemLower);

                    if (product == null) return Json(new { success = false, message = $"Product '{item.Name}' not found in database." });

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

                // --- 2. CREATE TRANSACTION ---
                string receiptNum = "TX" + DateTime.Now.ToString("HHmmssff");

                int paymentId = 1; // Default to Cash
                DateOnly? transactionDueDate = null; // ?? FIX: Using DateOnly to match your Model

                if (request.PaymentMethod == "Cash In Hand") paymentId = 1;
                else if (request.PaymentMethod == "GCash API") paymentId = 2;
                else if (request.PaymentMethod == "Credit Card") paymentId = 3;
                else if (request.PaymentMethod == "Store Credits")
                {
                    paymentId = 4;

                    var customer = await _context.Customers.FindAsync(2); // Hardcoded to Jhandy for now
                    if (customer == null) return Json(new { success = false, message = "Customer not found." });

                    if (string.IsNullOrWhiteSpace(customer.ContactNumber))
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"Cannot process Store Credit. {customer.FullName} does not have a registered contact number for SMS billing reminders."
                        });
                    }

                    if (customer.CurrentDebt + request.Total > customer.CreditLimit)
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"Store credit limit exceeded. {customer.FullName} already has PHP {customer.CurrentDebt:0.00} in debt. Adding PHP {request.Total:0.00} exceeds their PHP {customer.CreditLimit:0.00} limit."
                        });
                    }

                    customer.CurrentDebt += request.Total;
                    _context.Customers.Update(customer);

                    // ?? FIX: Convert 3 days from now into a DateOnly
                    transactionDueDate = DateOnly.FromDateTime(DateTime.Now.AddDays(3));
                }

                var newTx = new Transaction
                {
                    RetailStaffId = currentRetailStaffId,
                    CustomerId = 2,
                    RetailId = currentRetailId,
                    TotalAmount = request.Total,
                    PaymentMethodId = paymentId,
                    StatusId = 1,
                    CreatedAt = DateTime.Now,
                    ReceiptNumber = receiptNum,
                    DueDate = transactionDueDate
                };

                _context.Transactions.Add(newTx);
                await _context.SaveChangesAsync();

                // --- 3. DEDUCT STOCK AND ADD ITEMS ---
                foreach (var item in request.Items)
                {
                    string normalizedItemName = item.Name.Trim().ToLower().Replace(" ", "_");
                    string rawItemLower = item.Name.Trim().ToLower();

                    var product = await _context.Products.FirstAsync(p =>
                        p.ProductName.ToLower() == normalizedItemName || p.ProductName.ToLower() == rawItemLower);

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
                .Take(5)
                .Select(t => new {
                    receiptNumber = t.ReceiptNumber,
                    date = t.CreatedAt.ToString("MM/dd/yyyy hh:mm tt"),
                    total = t.TotalAmount
                })
                .ToListAsync();

            return Json(new { success = true, transactions });
        }

        // -------------------- [STORE CREDIT PENALTIES] --------------------

        [HttpPost]
        [Authorize(Roles = "Admin,Cashier")]
        public async Task<IActionResult> ApplyLateFees()
        {
            // ?? FIX: Convert today's exact time to DateOnly for the comparison
            var today = DateOnly.FromDateTime(DateTime.Now);

            var overdueTransactions = await _context.Transactions
                .Where(t => t.PaymentMethodId == 4 && t.StatusId == 1 && t.DueDate < today)
                .ToListAsync();

            int updatedCount = 0;

            foreach (var tx in overdueTransactions)
            {
                var customer = await _context.Customers.FindAsync(tx.CustomerId);

                if (customer != null)
                {
                    decimal penaltyAmount = tx.TotalAmount * 0.05m;

                    customer.CurrentDebt += penaltyAmount;
                    tx.TotalAmount += penaltyAmount;

                    // Extend the due date by another 3 days
                    tx.DueDate = tx.DueDate.Value.AddDays(3);

                    _context.Customers.Update(customer);
                    _context.Transactions.Update(tx);
                    updatedCount++;
                }
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"Successfully applied 5% late fees to {updatedCount} overdue transactions."
            });
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

        public class FaceAuthRequest
        {
            public string CapturedEmbeddingJson { get; set; } = string.Empty;
        }

        public class CustomerMatchResult
        {
            public int CustomerId { get; set; }
            public string FullName { get; set; } = string.Empty;
            public double Distance { get; set; }
            public double SimilarityScore { get; set; }
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
}