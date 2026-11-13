using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartScan.Data;
using SmartScan.Models;

namespace SmartScan.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CustomersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: CUSTOMERS
        public async Task<IActionResult> Index(string? search)
        {
            ViewData["CurrentFilter"] = search;

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userStoresQuery = _context.Retails.Where(r =>
                r.AdminOwner == currentUserId ||
                _context.RetailStaffs.Any(rs => rs.RetailId == r.RetailId && rs.StaffId == currentUserId)
            );

            var permittedRetailIds = await userStoresQuery.Select(r => r.RetailId).ToListAsync();

            var customerQuery = _context.Customers
                .Include(c => c.Status)
                .Include(c => c.Retail)
                .Include(c => c.Transactions)
                .AsNoTracking()
                .Where(c => permittedRetailIds.Contains(c.RetailId))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                // Also support searching when user types formatted spaces (e.g., "0912 345")
                string cleanSearch = new string(search.Where(char.IsDigit).ToArray());

                customerQuery = customerQuery.Where(c =>
                    c.FullName.Contains(search) ||
                    (c.ContactNumber != null && (c.ContactNumber.Contains(search) || (!string.IsNullOrEmpty(cleanSearch) && c.ContactNumber.Contains(cleanSearch)))));
            }

            var sortedCustomers = await customerQuery
                .OrderByDescending(c => c.CurrentDebt)
                .ThenByDescending(c => c.CreatedAt)
                .ToListAsync();

            return View(sortedCustomers);
        }

        // GET: CUSTOMERS/Create
        public async Task<IActionResult> Create()
        {
            await PopulateUserRetailsAsync();
            return View();
        }

        // POST: CUSTOMERS/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("FullName,ContactNumber,RetailId")] Customer customer,
            string? faceEmbeddingJson)
        {
            ModelState.Remove(nameof(customer.Status));
            ModelState.Remove(nameof(customer.Retail));

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Verify the selected RetailId is accessible to this user
            bool isPermittedStore = await _context.Retails.AnyAsync(r =>
                r.RetailId == customer.RetailId &&
                (r.AdminOwner == currentUserId ||
                 _context.RetailStaffs.Any(rs => rs.RetailId == r.RetailId && rs.StaffId == currentUserId)));

            if (!isPermittedStore)
            {
                ModelState.AddModelError(nameof(customer.RetailId), "You do not have access to register customers for this retail store.");
            }

            // Normalize and validate ContactNumber (Required, 11 digits, starts with 09)
            string cleanContact = new string((customer.ContactNumber ?? string.Empty).Where(char.IsDigit).ToArray());
            customer.ContactNumber = cleanContact;
            ModelState.Remove(nameof(customer.ContactNumber));

            if (string.IsNullOrWhiteSpace(cleanContact) || cleanContact.Length != 11 || !cleanContact.StartsWith("09"))
            {
                ModelState.AddModelError(nameof(customer.ContactNumber), "A valid 11-digit contact number starting with 09 is required.");
            }
            else if (isPermittedStore)
            {
                // Restrict duplicate contact number within the same retail store
                bool isDuplicate = await _context.Customers
                    .AnyAsync(c => c.RetailId == customer.RetailId && c.ContactNumber == cleanContact);

                if (isDuplicate)
                {
                    ModelState.AddModelError(nameof(customer.ContactNumber), $"The contact number '{cleanContact}' is already registered to another customer.");
                }
            }

            customer.StatusId = 1;
            customer.FaceEmbedding = !string.IsNullOrWhiteSpace(faceEmbeddingJson)
                ? faceEmbeddingJson.Trim()
                : null;

            customer.CreditLimit = 300.00m;
            customer.CurrentDebt = 0.00m;
            customer.CreatedAt = DateTime.UtcNow;

            if (ModelState.IsValid)
            {
                customer.FullName = customer.FullName.Trim();
                customer.ContactNumber = cleanContact;

                _context.Add(customer);
                await _context.SaveChangesAsync();

                TempData["CustomerMessage"] = $"Customer '{customer.FullName}' registered successfully.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateUserRetailsAsync();
            return View(customer);
        }

        // GET: CUSTOMERS/Edit/5
        public async Task<IActionResult> Edit(int? customerid)
        {
            if (customerid == null) return NotFound();

            var customer = await _context.Customers
                .Include(c => c.Retail)
                .FirstOrDefaultAsync(c => c.CustomerId == customerid);

            if (customer == null) return NotFound();

            await PopulateUserRetailsAsync();
            return View(customer);
        }

        // POST: CUSTOMERS/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int? customerid,
            [Bind("CustomerId,FullName,ContactNumber")] Customer customer,
            string? faceEmbeddingJson)
        {
            if (customerid != customer.CustomerId) return NotFound();

            var existingCustomer = await _context.Customers
                .Include(c => c.Retail)
                .FirstOrDefaultAsync(c => c.CustomerId == customerid);

            if (existingCustomer == null) return NotFound();

            ModelState.Remove(nameof(customer.Status));
            ModelState.Remove(nameof(customer.Retail));

            // Normalize and validate ContactNumber (Required, 11 digits, starts with 09)
            string cleanContact = new string((customer.ContactNumber ?? string.Empty).Where(char.IsDigit).ToArray());
            customer.ContactNumber = cleanContact;
            ModelState.Remove(nameof(customer.ContactNumber));

            if (string.IsNullOrWhiteSpace(cleanContact) || cleanContact.Length != 11 || !cleanContact.StartsWith("09"))
            {
                ModelState.AddModelError(nameof(customer.ContactNumber), "A valid 11-digit contact number starting with 09 is required.");
            }
            else
            {
                // Restrict duplicate contact number on any OTHER customer in the same store
                bool isDuplicate = await _context.Customers
                    .AnyAsync(c => c.RetailId == existingCustomer.RetailId
                                && c.CustomerId != existingCustomer.CustomerId
                                && c.ContactNumber == cleanContact);

                if (isDuplicate)
                {
                    ModelState.AddModelError(nameof(customer.ContactNumber), $"The contact number '{cleanContact}' is already registered to another customer.");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    existingCustomer.FullName = customer.FullName.Trim();
                    existingCustomer.ContactNumber = cleanContact;

                    if (!string.IsNullOrWhiteSpace(faceEmbeddingJson))
                    {
                        existingCustomer.FaceEmbedding = faceEmbeddingJson.Trim();
                    }

                    await _context.SaveChangesAsync();
                    TempData["CustomerMessage"] = $"Profile for '{existingCustomer.FullName}' updated.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CustomerExists(customer.CustomerId)) return NotFound();
                    throw;
                }
            }

            // Restore original properties on validation error
            customer.FaceEmbedding = existingCustomer.FaceEmbedding;
            customer.StatusId = existingCustomer.StatusId;
            customer.RetailId = existingCustomer.RetailId;
            customer.Retail = existingCustomer.Retail;

            await PopulateUserRetailsAsync();
            return View(customer);
        }

        // GET: CUSTOMERS/Delete/5
        public async Task<IActionResult> Delete(int? customerid)
        {
            if (customerid == null) return NotFound();

            var customer = await _context.Customers
                .Include(c => c.Status)
                .Include(c => c.Retail)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.CustomerId == customerid);

            if (customer == null) return NotFound();

            return View(customer);
        }

        // POST: CUSTOMERS/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int? customerid)
        {
            var customer = await _context.Customers.FindAsync(customerid);
            if (customer != null)
            {
                try
                {
                    _context.Customers.Remove(customer);
                    await _context.SaveChangesAsync();
                    TempData["CustomerMessage"] = $"Customer '{customer.FullName}' deleted.";
                }
                catch (DbUpdateException)
                {
                    TempData["ErrorMessage"] = $"Cannot delete '{customer.FullName}' because they have existing records in the Transactions ledger.";
                }
            }

            return RedirectToAction(nameof(Index));
        }

        private bool CustomerExists(int customerid)
        {
            return _context.Customers.Any(e => e.CustomerId == customerid);
        }

        // ================= HELPER METHODS =================
        private async Task PopulateUserRetailsAsync()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            ViewBag.Retails = await _context.Retails
                .AsNoTracking()
                .Where(r => r.AdminOwner == currentUserId ||
                            _context.RetailStaffs.Any(rs => rs.RetailId == r.RetailId && rs.StaffId == currentUserId))
                .Select(r => new { id = r.RetailId, name = r.RetailName })
                .ToListAsync();
        }

        // ================= FACIAL BIOMETRIC API =================
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

            var registeredCustomers = await _context.Customers
                .AsNoTracking()
                .Where(c => c.FaceEmbedding != null && c.FaceEmbedding != "")
                .Select(c => new
                {
                    c.CustomerId,
                    c.FullName,
                    c.FaceEmbedding
                })
                .ToListAsync();

            if (!registeredCustomers.Any())
                return Json(new { success = false, message = "Access Denied: No customers enrolled in biometric registry." });

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
                return Json(new
                {
                    success = true,
                    customerId = bestMatch.CustomerId,
                    customerName = bestMatch.FullName,
                    confidence = $"{(bestMatch.SimilarityScore * 100):0.0}%",
                    redirectUrl = Url.Action("Payment", "Home", new { search = bestMatch.FullName })
                });
            }

            return Json(new { success = false, message = "Face not recognized. Person is not in customer registry." });
        }

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
}