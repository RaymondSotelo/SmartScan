
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartScan.Models;
using SmartScan.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

public class RetailStaffsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public RetailStaffsController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
        _context = context;
    }

    // GET: RETAILSTAFFS
    public async Task<IActionResult> Index(int? retailId, string search)    
    {
        ViewData["CurrentFilter"] = search;

        if (retailId == null)
        {
            return NotFound();
        }

        // Fetch store name
        var storeName = await _context.Retails
            .Where(r => r.RetailId == retailId)
            .Select(r => r.RetailName)
            .FirstOrDefaultAsync();

        // Fetch retail staff members
        var retailStaffQuery = _context.RetailStaffs
            .Include(rs => rs.Cashier)
            .Include(rs => rs.Status)
            .Include(rs => rs.Retail)
            .Where(rs => rs.RetailId == retailId)
            .AsQueryable();

        // Store retailId
        ViewBag.RetailId = retailId;

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            retailStaffQuery = retailStaffQuery.Where(rs =>
                (rs.Retail != null && rs.Retail.RetailName.Contains(search)) ||
                (rs.Cashier != null && rs.Cashier.UserName.Contains(search)));
        }

        ViewBag.RetailName = storeName;
        ViewData["CashierId"] = new SelectList(_context.Users, "CashierId", "UserName");

        return View(await retailStaffQuery.ToListAsync());
    }

    // GET: RETAILSTAFFS/Create
    public async Task<IActionResult> Create(int? retailId, string search)
    {
        if (retailId == null)
        {
            return NotFound();
        }

        ViewBag.RetailId = retailId;
        ViewData["CashierId"] = new SelectList(_context.Users, "CashierId", "UserName");

        var assignedCashier = await _context.RetailStaffs
            .Where(rs => rs.CashierId != null)
            .Select(rs => rs.CashierId)
            .ToHashSetAsync();

        ViewBag.CashierList = (await _userManager.GetUsersInRoleAsync("Cashier"))
            .Where(u => u.EmailConfirmed && !assignedCashier.Contains(u.Id))
            .ToList();

        return View();
    }

    // POST: RETAILSTAFFS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("CashierId,StatusId,RetailId")] RetailStaff retailstaff)
    {
        if (ModelState.IsValid)
        {
            retailstaff.StatusId = 1;

            _context.Add(retailstaff);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Create), new { retailId = retailstaff.RetailId });
        }

        ViewData["CashierId"] = new SelectList(_context.Users, "CashierId", "UserName");
        return View(retailstaff);
    }

    // GET: RETAILSTAFFS/Delete/5
    public async Task<IActionResult> Delete(int? retailstaffid)
    {
        if (retailstaffid == null)
        {
            return NotFound();
        }

        var retailstaff = await _context.RetailStaffs
            .FirstOrDefaultAsync(m => m.RetailStaffId == retailstaffid);
        if (retailstaff == null)
        {
            return NotFound();
        }

        return View(retailstaff);
    }

    // POST: RETAILSTAFFS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? retailstaffid)
    {
        if (retailstaffid == null)
        {
            return NotFound();
        }

        var retailstaff = await _context.RetailStaffs.FindAsync(retailstaffid);

        if (retailstaff == null)
        {
            return NotFound();
        }

        var retailId = retailstaff.RetailId;

        _context.RetailStaffs.Remove(retailstaff);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index), new { retailId = retailId });
    }

    private bool RetailStaffExists(int? retailstaffid)
    {
        return _context.RetailStaffs.Any(e => e.RetailStaffId == retailstaffid);
    }
}
