using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;
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

        // Centralized Philippine Standard Time (UTC+8) helper
        private static DateTime PhilippineNow => DateTime.UtcNow.AddHours(8);

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        // -===================- [GET METHODS] -===================-

        // GET: Home
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            ViewData["HeaderTitle"] = "Welcome to SmartScan";

            // Allow unauthenticated visitors to view the Home Page directly
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return View();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (User.IsInRole("Cashier"))
            {
                bool isAssigned = await _context.RetailStaffs.AnyAsync(rs => rs.StaffId == currentUserId);
                if (isAssigned)
                    return RedirectToAction("Checkout", "Home");

                return RedirectToAction("Unassigned", "Home");
            }

            if (User.IsInRole("Admin"))
                return RedirectToAction("Index", "Users");

            return View();
        }

        // GET: Home/Unassigned
        [HttpGet]
        public async Task<IActionResult> Unassigned()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (User.IsInRole("Cashier"))
            {
                bool isAssigned = await _context.RetailStaffs
                    .AnyAsync(rs => rs.StaffId == currentUserId && rs.StatusId == 1);
                
                if (isAssigned)
                {
                    return RedirectToAction("Checkout", "Home");
                }
            }

            ViewData["HeaderTitle"] = "Terminal Status";
            return View();
        }

        // GET: Home/Payment
        [HttpGet]
        public async Task<IActionResult> Payment(string search)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var staffAssignment = await _context.RetailStaffs
                .Include(rs => rs.Retail)
                .FirstOrDefaultAsync(rs => rs.StaffId == currentUserId && rs.StatusId == 1);

            if (staffAssignment?.Retail == null)
            {
                return RedirectToAction("Unassigned", "Home");
            }

            ViewData["HeaderTitle"] = staffAssignment.Retail.RetailName;
            if (!string.IsNullOrEmpty(search)) ViewData["CustomerName"] = search;

            return View();
        }

        // GET: Home/CustomerDisplay
        [HttpGet]
        public async Task<IActionResult> CustomerDisplay()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var staffAssignment = await _context.RetailStaffs
                .Include(rs => rs.Retail)
                .FirstOrDefaultAsync(rs => rs.StaffId == currentUserId && rs.StatusId == 1);

            if (staffAssignment?.Retail == null)
            {
                return RedirectToAction("Unassigned", "Home");
            }

            ViewData["HeaderTitle"] = staffAssignment.Retail.RetailName;
            return View();
        }

        // GET: Home/GenerateReceipt
        [HttpGet]
        public async Task<IActionResult> GenerateReceipt()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var staffAssignment = await _context.RetailStaffs
                .Include(rs => rs.Retail)
                .FirstOrDefaultAsync(rs => rs.StaffId == currentUserId && rs.StatusId == 1);

            if (staffAssignment?.Retail == null)
            {
                return RedirectToAction("Unassigned", "Home");
            }

            ViewData["HeaderTitle"] = staffAssignment.Retail.RetailName;
            return View();
        }

        // GET: Home/Checkout
        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var staffAssignment = await _context.RetailStaffs
                .Include(rs => rs.Retail)
                .FirstOrDefaultAsync(rs => rs.StaffId == currentUserId && rs.StatusId == 1);

            if (staffAssignment?.Retail == null)
            {
                return RedirectToAction("Unassigned", "Home");
            }

            ViewData["HeaderTitle"] = staffAssignment.Retail.RetailName;
            return View();
        }

        // GET: CustomerVerification
        [HttpGet]
        public async Task<IActionResult> CustomerVerification()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var staffAssignment = await _context.RetailStaffs
                .Include(rs => rs.Retail)
                .FirstOrDefaultAsync(rs => rs.StaffId == currentUserId && rs.StatusId == 1);

            if (staffAssignment?.Retail == null)
            {
                return RedirectToAction("Unassigned", "Home");
            }

            // --- ACCESS RESTRICTION: Block direct URL typing ---
            string? referer = Request.Headers.Referer.ToString();

            // Direct typing in address bar results in an empty referer header
            if (string.IsNullOrWhiteSpace(referer))
            {
                return RedirectToAction("Checkout", "Home");
            }

            // Allow entry only if arriving directly from Checkout or Payment
            bool isFromAllowedPages = referer.Contains("/Home/Checkout", StringComparison.OrdinalIgnoreCase)
                                   || referer.Contains("/Home/Payment", StringComparison.OrdinalIgnoreCase);

            if (!isFromAllowedPages)
            {
                return RedirectToAction("Checkout", "Home");
            }

            ViewData["HeaderTitle"] = staffAssignment.Retail.RetailName;
            return View();
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult PaymentSuccess()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult DigitalReceipt()
        {
            return View();
        }

        // -===================- [LOGIC METHODS] -===================-

        // GetProductDetails | Retrieves product details inside inventory for the current cashier's retail store
        [HttpGet]
        public async Task<IActionResult> GetProductDetails(string productName)
        {
            if (string.IsNullOrWhiteSpace(productName))
                return Json(new { success = false, message = "Empty query." });

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var staffAssignment = await _context.RetailStaffs
                .AsNoTracking()
                .FirstOrDefaultAsync(rs => rs.StaffId == currentUserId && rs.StatusId == 1);

            int currentRetailId = staffAssignment?.RetailId ?? 0;
            if (currentRetailId == 0)
                return Json(new { success = false, message = "Cashier is not assigned to a retail store." });

            string cleanQuery = productName.Trim().ToLower();

            // Try EXACT match on ProductName or Barcode within this store's inventory first
            var matchingInventories = await _context.Inventories
                .AsNoTracking()
                .Include(i => i.Product)
                .Where(i => i.RetailId == currentRetailId &&
                            i.Product != null &&
                            (i.Product.ProductName.ToLower() == cleanQuery ||
                             (i.Product.Barcode != null && i.Product.Barcode.ToLower() == cleanQuery)))
                .ToListAsync();

            // Fallback to partial match ONLY if no exact match exists
            if (!matchingInventories.Any())
            {
                matchingInventories = await _context.Inventories
                    .AsNoTracking()
                    .Include(i => i.Product)
                    .Where(i => i.RetailId == currentRetailId &&
                                i.Product != null &&
                                (i.Product.ProductName.ToLower().Contains(cleanQuery) ||
                                 (i.Product.Barcode != null && i.Product.Barcode.ToLower().Contains(cleanQuery))))
                    .ToListAsync();
            }

            if (!matchingInventories.Any())
                return Json(new { success = false, message = "Product not found in this store's inventory." });

            var targetProductId = matchingInventories.First().ProductId;
            var productRows = matchingInventories.Where(i => i.ProductId == targetProductId).ToList();
            var primary = productRows.First();


            return Json(new
            {
                success = true,
                productId = primary.ProductId,
                name = primary.Product!.ProductName,
                barcode = primary.Product.Barcode,
                price = primary.LocalPrice > 0 ? primary.LocalPrice : primary.Product.Price,
                stock = productRows.Sum(r => r.Stock),
                isAgeRestricted = primary.Product.IsAgeRestricted
            });
        }

        // SearchProducts | Filters products matching either name or barcode inside the store's inventory
        [HttpGet]
        public async Task<IActionResult> SearchProducts(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return Json(new List<object>());

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var staffAssignment = await _context.RetailStaffs
                .AsNoTracking()
                .FirstOrDefaultAsync(rs => rs.StaffId == currentUserId && rs.StatusId == 1);

            int currentRetailId = staffAssignment?.RetailId ?? 0;
            if (currentRetailId == 0)
                return Json(new List<object>());

            string cleanTerm = term.Trim().ToLower();

            // Query strictly from THIS retail store's Inventories table
            var storeInventories = await _context.Inventories
                .AsNoTracking()
                .Include(i => i.Product)
                .Where(i => i.RetailId == currentRetailId &&
                            i.Product != null &&
                            (i.Product.ProductName.ToLower().Contains(cleanTerm) ||
                             (i.Product.Barcode != null && i.Product.Barcode.ToLower().Contains(cleanTerm))))
                .ToListAsync();

            // Group by ProductId so duplicate inventory rows for the same product sum accurately
            var results = storeInventories
                .GroupBy(i => i.ProductId)
                .Select(g =>
                {
                    var first = g.First();
                    return new
                    {
                        productId = first.ProductId,
                        name = first.Product!.ProductName,
                        barcode = first.Product.Barcode,
                        price = first.LocalPrice > 0 ? first.LocalPrice : first.Product.Price,
                        stock = g.Sum(x => x.Stock)
                    };
                })
                .OrderByDescending(x => x.name.ToLower() == cleanTerm || (x.barcode != null && x.barcode.ToLower() == cleanTerm))
                .ThenBy(x => x.name)
                .Take(10)
                .ToList();

            return Json(results);
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // GetCustomerCredit | Retrieves customer credit limits and debt
        [HttpGet]
        public async Task<IActionResult> GetCustomerCredit(string customerName)
        {
            if (string.IsNullOrWhiteSpace(customerName)) return Json(new { success = false });

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var staffAssignment = await _context.RetailStaffs.FirstOrDefaultAsync(rs => rs.StaffId == currentUserId && rs.StatusId == 1);
            int currentRetailId = staffAssignment?.RetailId ?? 0;

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.FullName.ToLower() == customerName.ToLower() && (currentRetailId == 0 || c.RetailId == currentRetailId));

            if (customer == null) return Json(new { success = false });

            decimal available = customer.CreditLimit - customer.CurrentDebt;

            var earliestTx = await _context.Transactions
                .Where(t => t.CustomerId == customer.CustomerId
                         && t.PaymentMethodId == 4
                         && t.StatusId == 4
                         && t.DueDate != null)
                .OrderBy(t => t.DueDate)
                .FirstOrDefaultAsync();

            string nextDueDate = earliestTx != null && earliestTx.DueDate.HasValue
                ? earliestTx.DueDate.Value.ToString("MMM dd, yyyy")
                : "No pending dues";

            return Json(new
            {
                success = true,
                customerId = customer.CustomerId,
                creditLimit = customer.CreditLimit,
                currentDebt = customer.CurrentDebt,
                availableBalance = available > 0 ? available : 0,
                nextDueDate = nextDueDate
            });
        }

        // IncreaseCreditLimit | Increases credit limit and extends due date
        [HttpPost]
        public async Task<IActionResult> IncreaseCreditLimit([FromBody] IncreaseLimitRequest req)
        {
            var customer = await _context.Customers.FindAsync(req.CustomerId);
            if (customer == null) return Json(new { success = false, message = "Customer not found." });

            customer.CreditLimit += 300;
            _context.Customers.Update(customer);

            var activeTransactions = await _context.Transactions
                .Where(t => t.CustomerId == customer.CustomerId
                         && t.PaymentMethodId == 4
                         && t.StatusId == 4
                         && t.DueDate != null)
                .ToListAsync();

            foreach (var tx in activeTransactions)
            {
                if (tx.DueDate.HasValue)
                {
                    tx.DueDate = tx.DueDate.Value.AddDays(3);
                    _context.Transactions.Update(tx);
                }
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, newLimit = customer.CreditLimit });
        }

        // AuthenticateFace | Verifies facial biometrics against customer registry
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AuthenticateFace([FromBody] FaceAuthRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.CapturedEmbeddingJson))
                return Json(new { success = false, message = "No facial biometric data captured." });

            float[]? capturedVector;
            try
            {
                capturedVector = JsonSerializer.Deserialize<float[]>(request.CapturedEmbeddingJson);
                if (capturedVector == null || capturedVector.Length != 128)
                    return Json(new { success = false, message = "Invalid face embedding dimensions. Must be 128 features." });
            }
            catch
            {
                return Json(new { success = false, message = "Malformed biometric data." });
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var staffAssignment = await _context.RetailStaffs.FirstOrDefaultAsync(rs => rs.StaffId == currentUserId && rs.StatusId == 1);
            int currentRetailId = staffAssignment?.RetailId ?? 0;

            var registeredCustomers = await _context.Customers
                .AsNoTracking()
                .Where(c => c.FaceEmbedding != null && c.FaceEmbedding != "" && (currentRetailId == 0 || c.RetailId == currentRetailId))
                .Select(c => new
                {
                    c.CustomerId,
                    c.FullName,
                    c.FaceEmbedding
                })
                .ToListAsync();

            if (!registeredCustomers.Any())
                return Json(new { success = false, message = "Access Denied: No customers are enrolled in this store's biometric registry." });

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
                catch { continue; }
            }

            if (bestMatch != null && bestMatch.Distance <= MAX_EUCLIDEAN_DISTANCE && bestMatch.SimilarityScore >= MIN_COSINE_SIMILARITY)
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

            return Json(new { success = false, message = "Face not recognized. This person is not enrolled in the customer registry." });
        }

        // NationalIDVerification | Verifies PhilSys QR structure & age in-memory without database storage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult NationalIDVerification([FromBody] GovIdVerifyRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.RawPayload) || string.IsNullOrWhiteSpace(req.ManualDob))
                return Json(new { isValid = false, message = "PCN and Date of Birth are required." });

            string digitsOnly = new string(req.RawPayload.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length != 16)
                return Json(new { isValid = false, message = "PCN must be exactly 16 digits long." });

            if (!DateTime.TryParse(req.ManualDob, out DateTime dob))
                return Json(new { isValid = false, message = "Invalid Date of Birth format." });

            // If submitted via QR Scan (Camera or Image), enforce authentic PhilSys JWT structure & claims
            if (req.IsFromQrScan)
            {
                if (string.IsNullOrWhiteSpace(req.RawQrToken))
                    return Json(new { isValid = false, message = "Missing PhilSys QR cryptographic token." });

                var parts = req.RawQrToken.Trim().Split('.');
                if (parts.Length != 3 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[2]))
                    return Json(new { isValid = false, message = "Invalid or forged QR format: Not a valid signed PhilSys JWS token." });

                try
                {
                    // Validate JWT Header
                    string headerJson = DecodeBase64Url(parts[0]);
                    using var headerDoc = JsonDocument.Parse(headerJson);
                    var headerRoot = headerDoc.RootElement;

                    string alg = headerRoot.TryGetProperty("alg", out var algProp) ? (algProp.GetString() ?? "") : "";
                    if (string.IsNullOrWhiteSpace(alg) || alg.Equals("none", StringComparison.OrdinalIgnoreCase))
                        return Json(new { isValid = false, message = "Security Alert: Unsigned or tampered QR token rejected." });

                    // Validate JWT Payload & PSA Schema
                    string payloadJson = DecodeBase64Url(parts[1]);
                    using var payloadDoc = JsonDocument.Parse(payloadJson);
                    var root = payloadDoc.RootElement;

                    // Check Issuer ("PSA" or official PhilSys issuer URL)
                    string issuer = root.TryGetProperty("iss", out var issProp) ? (issProp.GetString() ?? "") :
                                    root.TryGetProperty("Issuer", out var issuerProp) ? (issuerProp.GetString() ?? "") : "";

                    if (!string.IsNullOrWhiteSpace(issuer) &&
                        !issuer.Contains("PSA", StringComparison.OrdinalIgnoreCase) &&
                        !issuer.Contains("philsys", StringComparison.OrdinalIgnoreCase))
                    {
                        return Json(new { isValid = false, message = $"Invalid QR Issuer ('{issuer}'). Only official PSA PhilSys IDs are accepted." });
                    }

                    // Extract subject object (PhilSys v1/v2 nests claims inside "subject")
                    JsonElement subject = root.TryGetProperty("subject", out var subjProp) ? subjProp : root;

                    // Verify PCN inside token matches submitted PCN
                    string tokenPcnRaw = GetFirstStringProperty(subject, "pcn", "PCN");
                    string tokenPcnDigits = new string(tokenPcnRaw.Where(char.IsDigit).ToArray());

                    if (tokenPcnDigits.Length != 16 || tokenPcnDigits != digitsOnly)
                        return Json(new { isValid = false, message = "PCN mismatch or missing 16-digit PCN inside PhilSys QR token." });

                    // Verify Date of Birth inside token matches submitted DOB
                    string tokenDobRaw = GetFirstStringProperty(subject, "bd", "DOB", "dob", "DateOfBirth", "birthDate");
                    if (string.IsNullOrWhiteSpace(tokenDobRaw) || !DateTime.TryParse(tokenDobRaw, out DateTime tokenDob))
                        return Json(new { isValid = false, message = "Missing or invalid Date of Birth inside PhilSys QR token." });

                    if (tokenDob.Date != dob.Date)
                        return Json(new { isValid = false, message = "Date of Birth mismatch: Input DOB does not match the PhilSys QR record." });

                    // Verify required PhilSys identity name fields exist in subject
                    string fName = GetFirstStringProperty(subject, "fName", "fname", "n_f");
                    string lName = GetFirstStringProperty(subject, "lName", "lname", "n_l");
                    if (string.IsNullOrWhiteSpace(fName) || string.IsNullOrWhiteSpace(lName))
                        return Json(new { isValid = false, message = "Incomplete PhilSys QR identity claims (missing holder name)." });
                }
                catch (JsonException)
                {
                    return Json(new { isValid = false, message = "Corrupted or unreadable PhilSys QR payload." });
                }
                catch (FormatException)
                {
                    return Json(new { isValid = false, message = "Malformed Base64 encoding in PhilSys QR token." });
                }
            }

            int age = PhilippineNow.Year - dob.Year;
            if (PhilippineNow.Date < dob.Date.AddYears(age)) age--;

            if (age < 5 || age > 120)
                return Json(new { isValid = false, message = "Age calculation resulted in an invalid range. Please check the DOB." });

            bool isUnderage = age < 18;

            return Json(new
            {
                isValid = true,
                idNumber = digitsOnly,
                age = age,
                isUnderage = isUnderage
            });
        }

        private static string DecodeBase64Url(string base64Url)
        {
            string base64 = base64Url.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            byte[] bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(bytes);
        }

        private static string GetFirstStringProperty(JsonElement element, params string[] propertyNames)
        {
            foreach (var propName in propertyNames)
            {
                if (element.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    var val = prop.GetString();
                    if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
                }
            }
            return string.Empty;
        }

        // CreateXenditCheckout | Dynamic invoice generation for GCash & Credit Card
        [HttpPost]
        public async Task<IActionResult> CreateXenditCheckout([FromBody] TransactionPayload request)
        {
            try
            {
                using var client = new HttpClient();
                const string SecretKey = "xnd_development_i2JEYU0yrrXSwm0GivqR0pEL9CcLk7znFZIwQ2QDGAPVM7xc1KPLgL6pHykZ";
                var url = "https://api.xendit.co/v2/invoices";

                var authToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{SecretKey}:"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

                string xenditMethod = request.PaymentMethod == "Credit Card" ? "CREDIT_CARD" : "GCASH";

                var payload = new
                {
                    external_id = "TX-" + PhilippineNow.ToString("yyyyMMddHHmmssffff"),
                    amount = (double)request.Total,
                    description = $"SmartScan Order - {request.CustomerName}",
                    currency = "PHP",
                    payment_methods = new[] { xenditMethod },
                    // Redirect to the auto-closing success page instead of /Home/Checkout
                    success_redirect_url = $"{Request.Scheme}://{Request.Host}/Home/PaymentSuccess",
                    failure_redirect_url = $"{Request.Scheme}://{Request.Host}/Home/Checkout"
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(url, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = "Xendit Error: " + responseString });
                }

                using var doc = JsonDocument.Parse(responseString);
                var checkoutUrl = doc.RootElement.GetProperty("invoice_url").GetString();

                return Json(new { success = true, checkoutUrl = checkoutUrl });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // SaveTransaction | Completes and records transaction
        [HttpPost]
        public async Task<IActionResult> SaveTransaction([FromBody] TransactionPayload request)
        {
            if (request == null || (!request.Items.Any() && request.DebtTransactionIds == null))
                return Json(new { success = false, message = "Invalid transaction payload." });

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
                return Json(new { success = false, message = "Session expired. Please log in again." });

            var retailStaff = await _context.RetailStaffs.FirstOrDefaultAsync(rs => rs.StaffId == currentUserId && rs.StatusId == 1);
            if (retailStaff == null)
                return Json(new { success = false, message = "Error: Your account is not assigned to a retail store." });

            int currentRetailId = retailStaff.RetailId;
            int currentRetailStaffId = retailStaff.RetailStaffId;
            int? actualCustomerId = request.CustomerId > 0 ? request.CustomerId : null;

            bool isSettlingDebt = request.DebtTransactionIds != null && request.DebtTransactionIds.Any();
            string receiptNum = "TX" + PhilippineNow.ToString("HHmmssff");
            int paymentId = 1;

            if (request.PaymentMethod == "Cash In Hand") paymentId = 1;
            else if (request.PaymentMethod == "GCash API") paymentId = 2;
            else if (request.PaymentMethod == "Credit Card") paymentId = 3;
            else if (request.PaymentMethod == "Store Credits") paymentId = 4;

            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // =========================================================================
                // CASE 1: SETTLING EXISTING DEBT (Bulk Updating Old Transactions)
                // =========================================================================
                if (isSettlingDebt)
                {
                    var debtsToSettle = await _context.Transactions
                        .Where(t => request.DebtTransactionIds!.Contains(t.TransactionId) && t.RetailId == currentRetailId)
                        .ToListAsync();

                    foreach (var tx in debtsToSettle)
                    {
                        tx.PaymentMethodId = paymentId;
                        tx.StatusId = 3; // 3 = Paid / Completed
                        tx.DueDate = null; // Clear due date
                        _context.Transactions.Update(tx);
                    }

                    if (actualCustomerId.HasValue)
                    {
                        var customer = await _context.Customers.FindAsync(actualCustomerId.Value);
                        if (customer != null)
                        {
                            customer.CurrentDebt = Math.Max(0, customer.CurrentDebt - request.Total);

                            if (customer.CurrentDebt == 0)
                            {
                                customer.CreditLimit = 300.00m;
                            }

                            _context.Customers.Update(customer);
                        }
                    }

                    await _context.SaveChangesAsync();
                    await dbTransaction.CommitAsync();

                    return Json(new { success = true, receiptNumber = receiptNum, dueDate = (string?)null });
                }

                // =========================================================================
                // CASE 2: NEW STORE CREDIT CHARGE (StatusId = 4)
                // =========================================================================
                if (paymentId == 4)
                {
                    if (!actualCustomerId.HasValue)
                        return Json(new { success = false, message = "Store Credit requires a verified customer account." });

                    var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerId == actualCustomerId.Value && c.RetailId == currentRetailId);

                    if (customer == null || string.IsNullOrWhiteSpace(customer.ContactNumber))
                        return Json(new { success = false, message = "Invalid customer profile or missing contact number." });

                    if (customer.CurrentDebt + request.Total > customer.CreditLimit)
                        return Json(new { success = false, message = $"Store credit limit exceeded. Available balance is PHP {(customer.CreditLimit - customer.CurrentDebt):0.00}." });

                    customer.CurrentDebt += request.Total;
                    _context.Customers.Update(customer);
                }

                // =========================================================================
                // CASE 3: STANDARD NEW SALE & INVENTORY DEDUCTION
                // =========================================================================
                DateOnly? transactionDueDate = paymentId == 4 ? DateOnly.FromDateTime(PhilippineNow.AddDays(7)) : null;

                var targetTx = new Transaction
                {
                    RetailStaffId = currentRetailStaffId,
                    CustomerId = actualCustomerId,
                    RetailId = currentRetailId,
                    TotalAmount = request.Total,
                    PaymentMethodId = paymentId,
                    StatusId = paymentId == 4 ? 4 : 3,
                    CreatedAt = PhilippineNow, // Stamped in local Philippine Time
                    ReceiptNumber = receiptNum,
                    DueDate = transactionDueDate
                };
                _context.Transactions.Add(targetTx);
                await _context.SaveChangesAsync(); // Generates targetTx.TransactionId

                // Tracker lists for Low Stock & Depleted Stock Security Alerts
                var lowStockProducts = new List<string>();
                var depletedProducts = new List<string>();
                const int LOW_STOCK_THRESHOLD = 5;
                DateTime todayStart = PhilippineNow.Date;

                foreach (var item in request.Items)
                {
                    string normalizedItemName = item.Name.Trim().ToLower().Replace(" ", "_");
                    string rawItemLower = item.Name.Trim().ToLower();

                    var product = await _context.Products.FirstAsync(p =>
                        p.ProductName.ToLower() == normalizedItemName ||
                        p.ProductName.ToLower() == rawItemLower);

                    var txItem = new TransactionItem
                    {
                        TransactionId = targetTx.TransactionId,
                        ProductId = product.ProductId,
                        Quantity = item.Qty,
                        UnitPrice = item.Price
                    };
                    _context.TransactionItems.Add(txItem);

                    var inventory = await _context.Inventories.FirstAsync(i =>
                        i.ProductId == product.ProductId && i.RetailId == currentRetailId);

                    int oldStock = inventory.Stock;
                    inventory.Stock -= item.Qty;
                    _context.Inventories.Update(inventory);

                    if (inventory.Stock <= 0 && oldStock > 0)
                    {
                        // Always alert when stock hits 0 (AlertTypeId = 5)
                        bool alreadyDepletedToday = await _context.SecurityAlerts.AnyAsync(a =>
                            a.RetailId == currentRetailId &&
                            a.AlertTypeId == 5 &&
                            a.CreatedAt >= todayStart &&
                            a.Description.Contains(product.ProductName));

                        if (!alreadyDepletedToday)
                        {
                            depletedProducts.Add(product.ProductName);
                        }
                    }
                    else if (inventory.Stock > 0 && inventory.Stock < LOW_STOCK_THRESHOLD)
                    {
                        // Trigger if stock crossed below 5 OR if no Low Stock alert was already logged today for this product
                        bool crossedThreshold = oldStock >= LOW_STOCK_THRESHOLD;
                        bool alreadyAlertedToday = await _context.SecurityAlerts.AnyAsync(a =>
                            a.RetailId == currentRetailId &&
                            a.AlertTypeId == 4 &&
                            a.CreatedAt >= todayStart &&
                            a.Description.Contains(product.ProductName));

                        if (crossedThreshold || !alreadyAlertedToday)
                        {
                            lowStockProducts.Add(product.ProductName);
                        }
                    }
                }

                string inventoryUrl = Url.Action("Index", "Inventories", new { retailId = currentRetailId }) ?? "/Inventories";

                if (lowStockProducts.Any())
                {
                    _context.SecurityAlerts.Add(new SecurityAlert
                    {
                        AlertTypeId = 4, // Low Stock Warning
                        Description = $"Low stock warning (below {LOW_STOCK_THRESHOLD} items left): {string.Join(", ", lowStockProducts)}",
                        CreatedAt = PhilippineNow,
                        RetailId = currentRetailId,
                        ActionUrl = inventoryUrl
                    });
                }

                if (depletedProducts.Any())
                {
                    _context.SecurityAlerts.Add(new SecurityAlert
                    {
                        AlertTypeId = 5, // Stock Depleted
                        Description = $"Stock completely depleted for: {string.Join(", ", depletedProducts)}",
                        CreatedAt = PhilippineNow,
                        RetailId = currentRetailId,
                        ActionUrl = inventoryUrl
                    });
                }

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                return Json(new { success = true, receiptNumber = receiptNum, dueDate = transactionDueDate?.ToString("MMM dd, yyyy") });
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // GET: /Home/GetOldTransactions
        [HttpGet]
        public async Task<IActionResult> GetOldTransactions()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var staffAssignment = await _context.RetailStaffs.FirstOrDefaultAsync(rs => rs.StaffId == currentUserId && rs.StatusId == 1);
            int currentRetailId = staffAssignment?.RetailId ?? 0;

            // Start of today in local Philippine time
            var todayLocal = PhilippineNow.Date;

            var transactions = await _context.Transactions
                .Include(t => t.Retail) 
                .Include(t => t.PaymentMethod)
                .Include(t => t.Status)
                .Include(t => t.Customer)
                .Include(t => t.TransactionItems)
                    .ThenInclude(ti => ti.Product)
                .Where(t => t.RetailId == currentRetailId && t.CreatedAt >= todayLocal)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    transactionId = t.TransactionId,
                    receiptNumber = t.ReceiptNumber,
                    date = t.CreatedAt.ToString("MMM dd, yyyy h:mm tt"),
                    retailName = t.Retail != null ? t.Retail.RetailName : "SmartScan Retail", // <-- 2. Add this property
                    total = t.TotalAmount,
                    paymentMethodId = t.PaymentMethodId,
                    paymentMethod = t.PaymentMethod != null ? t.PaymentMethod.PaymentMethodName : "Cash In Hand",
                    status = t.Status != null ? t.Status.StatusName : "Completed",
                    statusId = t.StatusId,
                    customerName = t.Customer != null ? t.Customer.FullName : null,
                    dueDate = t.DueDate.HasValue ? t.DueDate.Value.ToString("yyyy-MM-dd") : null,
                    items = t.TransactionItems.Select(ti => new
                    {
                        name = ti.Product != null ? ti.Product.ProductName : "Unknown Product",
                        qty = ti.Quantity,
                        price = ti.UnitPrice,
                        subtotal = ti.Quantity * ti.UnitPrice
                    }).ToList()
                })
                .ToListAsync();

            return Json(new { success = true, transactions });
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomerDebtTransaction(int customerId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var staffAssignment = await _context.RetailStaffs.FirstOrDefaultAsync(rs => rs.StaffId == currentUserId && rs.StatusId == 1);
            int currentRetailId = staffAssignment?.RetailId ?? 0;

            // AUTOMATICALLY APPLY LATE FEES BEFORE FETCHING DEBT
            var today = DateOnly.FromDateTime(PhilippineNow);
            var overdueTransactions = await _context.Transactions
                .Where(t => t.CustomerId == customerId
                         && t.RetailId == currentRetailId
                         && t.PaymentMethodId == 4
                         && t.StatusId == 4
                         && t.DueDate < today)
                .ToListAsync();

            if (overdueTransactions.Any())
            {
                var customerToUpdate = await _context.Customers.FindAsync(customerId);
                if (customerToUpdate != null)
                {
                    foreach (var tx in overdueTransactions)
                    {
                        decimal fee = tx.TotalAmount * 0.05m;
                        customerToUpdate.CurrentDebt += fee;
                        tx.TotalAmount += fee;
                        tx.DueDate = tx.DueDate!.Value.AddDays(3);
                        _context.Transactions.Update(tx);
                    }
                    _context.Customers.Update(customerToUpdate);
                    await _context.SaveChangesAsync();
                }
            }

            // FETCH THE NOW-UPDATED TRANSACTIONS
            var debtTxs = await _context.Transactions
                .AsNoTracking()
                .Include(t => t.TransactionItems)
                    .ThenInclude(ti => ti.Product)
                .Where(t => t.CustomerId == customerId
                         && t.RetailId == currentRetailId
                         && t.PaymentMethodId == 4
                         && t.StatusId == 4)
                .ToListAsync();

            if (!debtTxs.Any())
            {
                return Json(new { success = false, message = "No active unpaid credit transactions found for this customer." });
            }

            var transactionIds = debtTxs.Select(t => t.TransactionId).ToList();
            var totalDebt = debtTxs.Sum(t => t.TotalAmount);

            // Group by both Product Name and UnitPrice to prevent pricing math conflicts
            var mergedItems = debtTxs.SelectMany(t => t.TransactionItems)
                .GroupBy(ti => new
                {
                    Name = ti.Product != null ? ti.Product.ProductName : "Unknown Item",
                    ti.UnitPrice
                })
                .Select(g => new
                {
                    name = g.Key.Name,
                    qty = g.Sum(ti => ti.Quantity),
                    price = g.Key.UnitPrice,
                    subtotal = g.Sum(ti => ti.Quantity * ti.UnitPrice)
                }).ToList();

            decimal itemsSum = mergedItems.Sum(i => (decimal)i.subtotal);
            decimal penaltyAmount = totalDebt > itemsSum ? (totalDebt - itemsSum) : 0m;

            // Retrieve earliest non-null due date
            var earliestDueDate = debtTxs
                .Where(t => t.DueDate.HasValue)
                .OrderBy(t => t.DueDate)
                .Select(t => t.DueDate!.Value.ToString("MMM dd, yyyy"))
                .FirstOrDefault();

            return Json(new
            {
                success = true,
                transactionIds = transactionIds,
                total = totalDebt,
                items = mergedItems,
                penaltyAmount = penaltyAmount,
                dueDate = earliestDueDate
            });
        }

        // ApplyLateFees | Applies 5% penalty to overdue credit transactions
        [HttpPost]
        [Authorize(Roles = "Cashier")]
        public async Task<IActionResult> ApplyLateFees()
        {
            var today = DateOnly.FromDateTime(PhilippineNow);

            var overdueTransactions = await _context.Transactions
                .Where(t => t.PaymentMethodId == 4 && t.StatusId == 4 && t.DueDate < today && t.CustomerId != null)
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
                    tx.DueDate = tx.DueDate!.Value.AddDays(3);

                    _context.Customers.Update(customer);
                    _context.Transactions.Update(tx);
                    updatedCount++;
                }
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Successfully applied 5% late fees to {updatedCount} overdue transactions." });
        }

        // POST: Home/LogCheckoutAnomaly | Logs real-time checkout anomalies
        [HttpPost]
        public async Task<IActionResult> LogCheckoutAnomaly([FromBody] CheckoutAnomalyPayload request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.AnomalyDetails))
                return Json(new { success = false, message = "Invalid anomaly payload." });

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
                return Json(new { success = false, message = "Session expired." });

            var retailStaff = await _context.RetailStaffs.FirstOrDefaultAsync(rs => rs.StaffId == currentUserId && rs.StatusId == 1);
            if (retailStaff == null)
                return Json(new { success = false, message = "User not assigned to a retail store." });

            try
            {
                var anomalyAlert = new SecurityAlert
                {
                    AlertTypeId = 6,
                    Description = $"Checkout Anomaly: {request.AnomalyDetails}",
                    CreatedAt = PhilippineNow,
                    RetailId = retailStaff.RetailId,
                    ActionUrl = ""
                };

                _context.SecurityAlerts.Add(anomalyAlert);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: Home/RegisterCustomerVerification
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterCustomerVerification([FromBody] CustomerRegistrationPayload request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.FullName))
            {
                return Json(new { success = false, message = "Customer full name is required." });
            }

            var cleanContact = request.ContactNumber?.Trim();
            if (string.IsNullOrWhiteSpace(cleanContact) || cleanContact.Length != 11 || !cleanContact.StartsWith("09"))
            {
                return Json(new { success = false, message = "A valid 11-digit contact number starting with 09 is required." });
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var retailStaff = await _context.RetailStaffs.FirstOrDefaultAsync(rs => rs.StaffId == currentUserId && rs.StatusId == 1);
            if (retailStaff == null)
            {
                return Json(new { success = false, message = "Error: Your cashier account is not assigned to any retail store." });
            }

            // --- CHECK FOR DUPLICATE CONTACT NUMBER IN STORE REGISTRY ---
            bool isDuplicate = await _context.Customers
                .AnyAsync(c => c.RetailId == retailStaff.RetailId && c.ContactNumber == cleanContact);

            if (isDuplicate)
            {
                return Json(new { success = false, message = $"The contact number '{cleanContact}' is already registered to another customer." });
            }

            var newCustomer = new Customer
            {
                FullName = request.FullName.Trim(),
                ContactNumber = cleanContact,
                RetailId = retailStaff.RetailId,
                StatusId = 1,
                CreditLimit = 300.00m,
                CurrentDebt = 0.00m,
                CreatedAt = PhilippineNow,
                FaceEmbedding = !string.IsNullOrWhiteSpace(request.FaceEmbeddingJson)
                    ? request.FaceEmbeddingJson.Trim()
                    : null
            };

            _context.Customers.Add(newCustomer);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                customerId = newCustomer.CustomerId,
                customerName = newCustomer.FullName
            });
        }

        // GET: /Home/GetCustomerByContact?contactNumber=09123456789
        [HttpGet]
        public async Task<IActionResult> GetCustomerByContact(string contactNumber)
        {
            if (string.IsNullOrWhiteSpace(contactNumber))
            {
                return Json(new { success = false, message = "Contact number is required." });
            }

            var digitsOnly = new string(contactNumber.Where(char.IsDigit).ToArray());

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var staffAssignment = await _context.RetailStaffs
                .FirstOrDefaultAsync(rs => rs.StaffId == currentUserId && rs.StatusId == 1);

            var query = _context.Customers
                .AsNoTracking()
                .AsQueryable();

            if (staffAssignment != null)
            {
                query = query.Where(c => c.RetailId == staffAssignment.RetailId);
            }

            var customer = await query.FirstOrDefaultAsync(c => c.ContactNumber == digitsOnly);
            if (customer == null)
            {
                return Json(new
                {
                    success = false,
                    message = $"No registered customer found with contact number '{digitsOnly}'."
                });
            }

            return Json(new
            {
                success = true,
                customerId = customer.CustomerId,
                customerName = customer.FullName,
                contactNumber = customer.ContactNumber,
                creditLimit = customer.CreditLimit,
                currentDebt = customer.CurrentDebt,
                redirectUrl = Url.Action("Payment", "Home", new { search = customer.FullName })
            });
        }

        // Math Helpers
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

    // Helper Class
    public class TransactionPayload
    {
        public List<CartItemPayload> Items { get; set; } = new();
        public decimal Total { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public List<int>? DebtTransactionIds { get; set; }
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
        public string? RawQrToken { get; set; }
        public bool IsFromQrScan { get; set; }
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

    public class IncreaseLimitRequest
    {
        public int CustomerId { get; set; }
    }

    public class CheckoutAnomalyPayload
    {
        public string AnomalyDetails { get; set; } = string.Empty;
    }

    public class CustomerRegistrationPayload
    {
        public string FullName { get; set; } = string.Empty;
        public string? ContactNumber { get; set; }
        public string? FaceEmbeddingJson { get; set; }
    }
}