using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartScan.Data;
using SmartScan.Models;

[Authorize(Roles = "Admin")]
public class TransactionsController : Controller
{
    private readonly ApplicationDbContext _context;

    public TransactionsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: TRANSACTIONS
    public async Task<IActionResult> Index(string search)    
    {
        ViewData["CurrentFilter"] = search;

        var transactionQuery = _context.Transactions
            .Include(t => t.RetailStaff)
            .Include(t => t.Customer)
            .Include(t => t.Retail)
            .Include(t => t.PaymentMethod)
            .Include(t => t.Status)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            bool isNumeric = decimal.TryParse(search, out decimal parsedPrice);
            //Date handler
            string[] formats = { "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy" };
            bool isDate = DateTime.TryParseExact(search,
                                                 formats,
                                                 System.Globalization.CultureInfo.InvariantCulture,
                                                 System.Globalization.DateTimeStyles.None,
                                                 out DateTime parsedDate);

            DateTime startOfDay = parsedDate.Date;
            DateTime endOfDay = parsedDate.Date.AddDays(1).AddTicks(-1);

            // You can search ->
            transactionQuery = transactionQuery.Where(t =>
                t.ReceiptNumber.Contains(search) || // Receipt Number
               (t.RetailStaff != null &&
                t.RetailStaff.Cashier != null &&
                t.RetailStaff.Cashier.UserName.Contains(search)) || // Cashier
               (t.Customer != null && t.Customer.FullName.Contains(search)) || // Customer
               (t.Retail != null && t.Retail.RetailName.Contains(search)) || //Retail
               (isNumeric && t.TotalAmount == parsedPrice) || // Total Amount
               (isDate && t.CreatedAt >= startOfDay && t.CreatedAt < endOfDay)); // Date
        }

        //Display Retail Staff Name
        var staffList = await _context.RetailStaffs
            .Include(rs => rs.Cashier)
            .Where(rs => rs.Cashier != null)
            .Select(rs => new {
                RetailStaffId = rs.RetailStaffId,
                DisplayName = rs.Cashier.UserName
            })
            .ToListAsync();

        ViewData["RetailStaff"] = new SelectList(_context.RetailStaffs, "RetailStaffId", "DisplayName");
        ViewData["Customer"] = new SelectList(_context.Customers, "CustomerId", "fullName");
        ViewData["Retail"] = new SelectList(_context.Retails, "RetailId", "RetailName");
        ViewData["PaymentMethod"] = new SelectList(_context.PaymentMethods, "PaymentMethodId", "PaymentMethodName");

        return View(await transactionQuery.ToListAsync());
    }

    // GET: TRANSACTIONS/Details/5
    public async Task<IActionResult> Details(int? transactionid)
    {
        if (transactionid == null)
        {
            return NotFound();
        }

        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(m => m.TransactionId == transactionid);
        if (transaction == null)
        {
            return NotFound();
        }

        return View(transaction);
    }

    // GET: TRANSACTIONS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: TRANSACTIONS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("TransactionId,RetailStaffId,CustomerId,RetailId,TotalAmount,PaymentMethodId,StatusId,CreatedAt,ReceiptNumber,Customer,PaymentMethod,Retail,RetailStaff,SecurityAlerts,Status,TransactionItems")] Transaction transaction)
    {
        if (ModelState.IsValid)
        {
            _context.Add(transaction);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(transaction);
    }

    // GET: TRANSACTIONS/Edit/5
    public async Task<IActionResult> Edit(int? transactionid)
    {
        if (transactionid == null)
        {
            return NotFound();
        }

        var transaction = await _context.Transactions.FindAsync(transactionid);
        if (transaction == null)
        {
            return NotFound();
        }
        return View(transaction);
    }

    // POST: TRANSACTIONS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? transactionid, [Bind("TransactionId,RetailStaffId,CustomerId,RetailId,TotalAmount,PaymentMethodId,StatusId,CreatedAt,ReceiptNumber,Customer,PaymentMethod,Retail,RetailStaff,SecurityAlerts,Status,TransactionItems")] Transaction transaction)
    {
        if (transactionid != transaction.TransactionId)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(transaction);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TransactionExists(transaction.TransactionId))
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
        return View(transaction);
    }

    // GET: TRANSACTIONS/Delete/5
    public async Task<IActionResult> Delete(int? transactionid)
    {
        if (transactionid == null)
        {
            return NotFound();
        }

        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(m => m.TransactionId == transactionid);
        if (transaction == null)
        {
            return NotFound();
        }

        return View(transaction);
    }

    // POST: TRANSACTIONS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? transactionid)
    {
        var transaction = await _context.Transactions.FindAsync(transactionid);
        if (transaction != null)
        {
            _context.Transactions.Remove(transaction);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool TransactionExists(int? transactionid)
    {
        return _context.Transactions.Any(e => e.TransactionId == transactionid);
    }
}
