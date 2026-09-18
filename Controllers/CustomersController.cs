
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartScan.Data;
using SmartScan.Models;

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
        customer.StatusId = 1; // Active status
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
    public async Task<IActionResult> Edit(int? customerid, [Bind("CustomerId,FullName,ContactNumber,CreditLimit,StatusId")] Customer customer)
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
                existingCustomer.StatusId = customer.StatusId;

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
}
