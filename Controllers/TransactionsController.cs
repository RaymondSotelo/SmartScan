using System.Security.Claims;
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

    // GET: Transactions
    public async Task<IActionResult> Index(string? search)
    {
        ViewData["CurrentFilter"] = search;

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Challenge();
        }

        var transactionQuery = _context.Transactions
            .Include(t => t.RetailStaff).ThenInclude(rs => rs.Staff)
            .Include(t => t.Customer)
            .Include(t => t.Retail)
            .Include(t => t.PaymentMethod)
            .Include(t => t.Status)
            .Include(t => t.TransactionItems)
            .AsNoTracking()
            // Filter: Show transactions ONLY for retails where the current user 
            // is the primary Owner (AdminOwner) OR registered as active staff
            .Where(t => t.Retail != null && (
                t.Retail.AdminOwner == currentUserId ||
                t.Retail.RetailStaffs.Any(rs =>
                    rs.StaffId == currentUserId &&
                    rs.Status != null &&
                    rs.Status.StatusName == "Active"
                )
            ))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            bool isNumeric = decimal.TryParse(search, out decimal parsedPrice);

            string[] formats = { "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy" };
            bool isDate = DateTime.TryParseExact(
                search,
                formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out DateTime parsedDate);

            if (isDate)
            {
                DateTime startOfDay = parsedDate.Date;
                DateTime endOfDay = startOfDay.AddDays(1);

                transactionQuery = transactionQuery.Where(t =>
                    t.ReceiptNumber.Contains(search) ||
                    (t.Retail != null && t.Retail.RetailName.Contains(search)) ||
                    (isNumeric && t.TotalAmount == parsedPrice) ||
                    (t.CreatedAt >= startOfDay && t.CreatedAt < endOfDay));
            }
            else
            {
                transactionQuery = transactionQuery.Where(t =>
                    t.ReceiptNumber.Contains(search) ||
                    (t.Retail != null && t.Retail.RetailName.Contains(search)) ||
                    (t.RetailStaff != null && t.RetailStaff.Staff != null && t.RetailStaff.Staff.UserName.Contains(search)) ||
                    (isNumeric && t.TotalAmount == parsedPrice));
            }
        }

        // Filter dropdowns to only the user's accessible stores
        var accessibleRetails = await _context.Retails
            .Where(r => r.AdminOwner == currentUserId ||
                        r.RetailStaffs.Any(rs => rs.StaffId == currentUserId && rs.Status.StatusName == "Active"))
            .Select(r => new { r.RetailId, r.RetailName })
            .ToListAsync();

        ViewData["Retail"] = new SelectList(accessibleRetails, "RetailId", "RetailName");
        ViewData["PaymentMethod"] = new SelectList(await _context.PaymentMethods.ToListAsync(), "PaymentMethodId", "PaymentMethodName");

        var transactions = await transactionQuery
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return View(transactions);
    }

    // GET: TRANSACTIONS/Details/5
    public async Task<IActionResult> Details(int? transactionid)
    {
        if (transactionid == null)
        {
            return NotFound();
        }

        var transaction = await _context.Transactions
        .Include(t => t.Retail)
        .Include(t => t.Customer)
        .Include(t => t.Status)
        .Include(t => t.PaymentMethod)
        .FirstOrDefaultAsync(m => m.TransactionId == transactionid);

        if (transaction == null)
        {
            return NotFound();
        }

        //Display Retail Staff Name
        var staffList = await _context.RetailStaffs
            .Include(rs => rs.Staff)
            .Where(rs => rs.Staff != null)
            .Select(rs => new {
                RetailStaffId = rs.RetailStaffId,
                DisplayName = rs.Staff.UserName
            })
            .ToListAsync();

        // Display Staff Name
        ViewData["StaffNames"] = await (
        from rs in _context.RetailStaffs
        join u in _context.Users on rs.StaffId equals u.Id
        select new
        {
            rs.RetailStaffId,
            UserName = u.UserName ?? "No Username"
        }
        ).ToDictionaryAsync(x => x.RetailStaffId, x => x.UserName);

        ViewBag.ItemList = await _context.TransactionItems
            .Where(ti => ti.TransactionId == transactionid)
            .Include(ti => ti.Transaction)
            .Include(ti => ti.Product)
            .ToListAsync();

        ViewData["RetailStaff"] = new SelectList(_context.RetailStaffs, "RetailStaffId", "DisplayName");
        ViewData["Customer"] = new SelectList(_context.Customers, "CustomerId", "fullName");
        ViewData["Retail"] = new SelectList(_context.Retails, "RetailId", "RetailName");
        ViewData["PaymentMethod"] = new SelectList(_context.PaymentMethods, "PaymentMethodId", "PaymentMethodName");

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
