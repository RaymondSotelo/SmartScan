using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartScan.Data;
using SmartScan.Models;
using SmartScan.Models.ViewModels;

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

            var customerQuery = _context.Customers
                .Include(c => c.Status)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                customerQuery = customerQuery.Where(c =>
                    c.FullName.Contains(search) ||
                    (c.ContactNumber != null && c.ContactNumber.Contains(search)));
            }

            return View(await customerQuery.OrderByDescending(c => c.CreatedAt).ToListAsync());
        }

        // GET: CUSTOMERS/Details/5
        public async Task<IActionResult> Details(int? customerid)
        {
            if (customerid == null) return NotFound();

            var customer = await _context.Customers
                .Include(c => c.Status)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.CustomerId == customerid);

            if (customer == null) return NotFound();

            return View(customer);
        }

        // GET: CUSTOMERS/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: CUSTOMERS/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("FullName,ContactNumber")] Customer customer,
            string? faceEmbeddingJson,
            string? idType,
            int? verifiedAge,
            bool isIdVerified = false)
        {
            // Clear navigation property from validation (scaffolded non-nullable reference)
            ModelState.Remove(nameof(customer.Status));

            // Process Data-Privacy Compliant Identity Check
            if (isIdVerified && verifiedAge.HasValue && verifiedAge.Value > 0)
            {
                customer.IsIdVerified = true;
                customer.Age = verifiedAge.Value;
                customer.StatusId = verifiedAge.Value < 18 ? 3 : 1; // 3 = Underage, 1 = Adult
            }
            else
            {
                customer.IsIdVerified = false;
                customer.Age = null;
                customer.StatusId = 1; // Default to Active
            }

            // Process Facial Biometrics
            customer.FaceEmbedding = !string.IsNullOrWhiteSpace(faceEmbeddingJson)
                ? faceEmbeddingJson.Trim()
                : null;

            customer.CreditLimit = 300.00m;
            customer.CurrentDebt = 0.00m;
            customer.CreatedAt = DateTime.UtcNow;

            if (ModelState.IsValid)
            {
                customer.FullName = customer.FullName.Trim();
                customer.ContactNumber = customer.ContactNumber?.Trim();

                _context.Add(customer);
                await _context.SaveChangesAsync();

                TempData["CustomerMessage"] = customer.StatusId == 3
                    ? $"Customer '{customer.FullName}' registered with Underage status (Age: {customer.Age})."
                    : $"Customer '{customer.FullName}' registered and verified successfully.";

                return RedirectToAction(nameof(Index));
            }

            return View(customer);
        }

        // GET: CUSTOMERS/Edit/5
        public async Task<IActionResult> Edit(int? customerid)
        {
            if (customerid == null) return NotFound();

            var customer = await _context.Customers.FindAsync(customerid);
            if (customer == null) return NotFound();

            return View(customer);
        }

        // POST: CUSTOMERS/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int? customerid,
            [Bind("CustomerId,FullName,ContactNumber")] Customer customer,
            string? faceEmbeddingJson,
            string? idType,
            int? verifiedAge,
            bool isIdVerified = false)
        {
            if (customerid != customer.CustomerId) return NotFound();

            var existingCustomer = await _context.Customers.FindAsync(customerid);
            if (existingCustomer == null) return NotFound();

            // Clear navigation property from validation
            ModelState.Remove(nameof(customer.Status));

            if (ModelState.IsValid)
            {
                try
                {
                    existingCustomer.FullName = customer.FullName.Trim();
                    existingCustomer.ContactNumber = customer.ContactNumber?.Trim();

                    // Update Biometrics only if newly captured
                    if (!string.IsNullOrWhiteSpace(faceEmbeddingJson))
                    {
                        existingCustomer.FaceEmbedding = faceEmbeddingJson.Trim();
                    }

                    // Update PhilSys verification & Age-Gating if re-verified
                    if (isIdVerified && verifiedAge.HasValue && verifiedAge.Value > 0)
                    {
                        existingCustomer.IsIdVerified = true;
                        existingCustomer.Age = verifiedAge.Value;
                        existingCustomer.StatusId = verifiedAge.Value < 18 ? 3 : 1;
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

            // Restore DB properties so Edit.cshtml does not lose verification state on validation error
            customer.FaceEmbedding = existingCustomer.FaceEmbedding;
            customer.IsIdVerified = existingCustomer.IsIdVerified;
            customer.Age = existingCustomer.Age;
            customer.StatusId = existingCustomer.StatusId;

            return View(customer);
        }

        // GET: CUSTOMERS/Delete/5
        public async Task<IActionResult> Delete(int? customerid)
        {
            if (customerid == null) return NotFound();

            var customer = await _context.Customers
                .Include(c => c.Status)
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
                    // Handles foreign key violation when customer has existing transaction records
                    TempData["ErrorMessage"] = $"Cannot delete '{customer.FullName}' because they have existing sales or debt records in the Transactions ledger.";
                }
            }

            return RedirectToAction(nameof(Index));
        }

        private bool CustomerExists(int customerid)
        {
            return _context.Customers.Any(e => e.CustomerId == customerid);
        }

        // POST: /Customers/VerifyGovernmentId
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyGovernmentId([FromBody] GovIdVerifyRequest req)
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

        public class GovIdVerifyRequest
        {
            public string? RawPayload { get; set; }
            public string? ManualDob { get; set; }
        }
    }
}