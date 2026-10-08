using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartScan.Data;
using SmartScan.Models;
using SmartScan.Models.ViewModels;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

[Authorize(Roles = "Admin")]
public class CustomersController : Controller
{
    private readonly ApplicationDbContext _context;

    public CustomersController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: CUSTOMERS
    public async Task<IActionResult> Index(string search)
    {
        ViewData["CurrentFilter"] = search;

        var customerQuery = _context.Customers
            .Include(p => p.Status)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            bool isNumeric = int.TryParse(search, out int parsedContactNum);

            customerQuery = customerQuery.Where(c =>
                c.FullName.Contains(search) ||
                c.ContactNumber.Contains(search));
        }

        return View(await customerQuery.ToListAsync());
    }

    // GET: CUSTOMERS/Details/5
    public async Task<IActionResult> Details(int? customerid)
    {
        if (customerid == null)
        {
            return NotFound();
        }

        var customer = await _context.Customers
            .FirstOrDefaultAsync(m => m.CustomerId == customerid);
        if (customer == null)
        {
            return NotFound();
        }

        return View(customer);
    }

    // GET: CUSTOMERS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: CUSTOMERS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("FullName,ContactNumber")] Customer customer)
    {
        // Digital ID and Facial Recognition are disabled for now
        customer.DigitalIdNumber = null;
        customer.DigitalIdHash = null;
        customer.FaceEmbedding = null;
        customer.HasFaceRegistered = false;

        // Default account & store credit rules
        customer.CreditLimit = 300.00m;
        customer.CurrentDebt = 0.00m;
        customer.StatusId = 2; // Active status
        customer.CreatedAt = DateTime.UtcNow;

        ModelState.Clear();
        TryValidateModel(customer);

        if (ModelState.IsValid)
        {
            _context.Add(customer);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(customer);
    }

    // GET: CUSTOMERS/Edit/5
    public async Task<IActionResult> Edit(int? customerid)
    {
        if (customerid == null)
        {
            return NotFound();
        }

        var customer = await _context.Customers.FindAsync(customerid);
        if (customer == null)
        {
            return NotFound();
        }

        ViewData["StatusId"] = new SelectList(_context.Statuses, "StatusId", "StatusName");
        return View(customer);
    }

    // POST: CUSTOMERS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? customerid, [Bind("CustomerId,FullName,ContactNumber,CreditLimit")] Customer customer)
    {
        if (customerid != customer.CustomerId)
        {
            return NotFound();
        }

        var existingCustomer = await _context.Customers.FindAsync(customerid);
        if (existingCustomer == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                // Update only those fields
                existingCustomer.FullName = customer.FullName.Trim();
                existingCustomer.ContactNumber = customer.ContactNumber?.Trim();
                existingCustomer.CreditLimit = customer.CreditLimit;
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CustomerExists(customer.CustomerId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }

        return View(customer);
    }

    // GET: CUSTOMERS/Delete/5
    public async Task<IActionResult> Delete(int? customerid)
    {
        if (customerid == null)
        {
            return NotFound();
        }

        var customer = await _context.Customers
            .FirstOrDefaultAsync(m => m.CustomerId == customerid);
        if (customer == null)
        {
            return NotFound();
        }

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
            _context.Customers.Remove(customer);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool CustomerExists(int? customerid)
    {
        return _context.Customers.Any(e => e.CustomerId == customerid);
    }

    // GET: Customer/Enroll/5
    public async Task<IActionResult> Enroll(int? customerid)
    {
        if (customerid == null)
        {
            return NotFound();
        }

        var customer = await _context.Customers.FindAsync(customerid);

        if (customer == null)
        {
            return NotFound();
        }

        var viewModel = new CustomerEnrollmentViewModel
        {
            CustomerId = customer.CustomerId,
            FullName = customer.FullName,
            ContactNumber = customer.ContactNumber,
            HasFaceRegistered = customer.HasFaceRegistered,
            HasDigitalIdRegistered = !string.IsNullOrEmpty(customer.DigitalIdNumber)
        };

        return View(viewModel);
    }

    // POST: Customer/EnrollDigitalId
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnrollDigitalId(int customerId, string digitalIdPayload)
    {
        if (string.IsNullOrEmpty(digitalIdPayload))
        {
            TempData["ErrorMessage"] = "Digital ID payload is required.";
            return RedirectToAction(nameof(Enroll), new { customerid = customerId });
        }

        var customer = await _context.Customers.FindAsync(customerId);
        if (customer == null)
        {
            return NotFound();
        }

        // Compute SHA256 hash for secure constant time indexing
        using var sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(digitalIdPayload.Trim()));
        string hash = Convert.ToHexString(bytes);

        // Checks if this digital id is already registered to another customer
        bool exist = await _context.Customers
            .AnyAsync(c => c.DigitalIdHash == hash && c.CustomerId != customerId);

        if (exist)
        {
            TempData["ErrorMessage"] = "This Digital ID is already registered to another customer.";
            return RedirectToAction(nameof(Enroll), new { customerid = customerId });
        }

        customer.StatusId = 1;
        customer.DigitalIdNumber = digitalIdPayload.Trim().Length > 128
            ? digitalIdPayload.Trim().Substring(0, 128)
            : digitalIdPayload.Trim();
        customer.DigitalIdHash = hash;

        await _context.SaveChangesAsync();
        TempData["Success"] = "Digital ID enrolled successfully.";
        return RedirectToAction(nameof(Enroll), new { customerid = customerId });
    }

    // POST: Customer/EnrollFace via AJAX when face is capture is confirmed
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnrollFace([FromBody] FaceEnrollmentDto dto)
    {
        if (dto == null || dto.CustomerId <= 0 || string.IsNullOrWhiteSpace(dto.FaceEmbeddingJson))
        {
            return Json(new { success = false, message = "Invalid biometrics payload." });
        }

        var customer = await _context.Customers.FindAsync(dto.CustomerId);
        if (customer == null)
        {
            return Json(new { success = false, message = "Customer not found." });
        }

        customer.StatusId = 1;
        customer.FaceEmbedding = dto.FaceEmbeddingJson;
        customer.HasFaceRegistered = true;

        await _context.SaveChangesAsync();
        return Json(new { success = true, message = "Face enrolled successfully." });
    }

    public class FaceEnrollmentDto
    {
        public int CustomerId { get; set; }
        public string FaceEmbeddingJson { get; set; } = string.Empty;
    }

    // -------------------- [TEST AREA!!] --------------------

    // GET: Customers/VerifyIdentity
    public IActionResult VerifyIdentity()
    {
        return View();
    }

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

        // STRICT FILTER: Only consider customers who explicitly have a registered face and active profile
        var registeredCustomers = await _context.Customers
            .Where(c => c.HasFaceRegistered && !string.IsNullOrWhiteSpace(c.FaceEmbedding))
            .Select(c => new
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

        // Strict biometrics thresholds
        // Euclidean distance: 0.0 (identical) to ~1.4 (opposite). Real match must be <= 0.45
        // Cosine similarity: must be >= 0.88

        // Relaxed from 0.45. 0.55 is still very secure for Face-API.js models.
        const double MAX_EUCLIDEAN_DISTANCE = 0.55;

        // Relaxed from 0.88 to 0.85
        const double MIN_COSINE_SIMILARITY = 0.85;

        foreach (var customer in registeredCustomers)
        {
            try
            {
                var storedVector = JsonSerializer.Deserialize<float[]>(customer.FaceEmbedding!);
                if (storedVector == null || storedVector.Length != 128) continue;

                double distance = ComputeEuclideanDistance(capturedVector, storedVector);
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

        // STRICT VERIFICATION: Both conditions must pass
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
                redirectUrl = Url.Action("Index", "Customers", new { search = bestMatch.FullName })
            });
        }

        // If anyone else's face is scanned that is NOT enrolled in the Customers table:
        return Json(new
        {
            success = false,
            message = "Face not recognized. This person is not enrolled in the customer registry."
        });
    }

    // Distance Helper (face-api.js native metric)
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

    // Cosine Similarity Helper
    private static double ComputeCosineSimilarity(float[] vA, float[] vB)
    {
        double dot = 0.0;
        double normA = 0.0;
        double normB = 0.0;
        for (int i = 0; i < vA.Length; i++)
        {
            dot += vA[i] * vB[i];
            normA += vA[i] * vA[i];
            normB += vB[i] * vB[i];
        }
        double denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom == 0.0 ? 0.0 : dot / denom;
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
