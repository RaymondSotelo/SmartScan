using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Build.Tasks.Deployment.Bootstrapper;
using Microsoft.EntityFrameworkCore;
using SmartScan.Data;
using SmartScan.Models;

[Authorize(Roles = "Admin")]
public class RetailsController : Controller
{
    private readonly ApplicationDbContext _context;

    public RetailsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: RETAILS
    public async Task<IActionResult> Index(string search)    
    {
        ViewData["CurrentFilter"] = search;

        var retailQuery = _context.Retails
            .Include(r => r.Status)
            .Include(r => r.AdminOwnerNavigation)
            //.Where(r => r.StatusId != 2)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            retailQuery = retailQuery.Where(r =>
                r.RetailName.Contains(search) ||
                r.Address.Contains(search) ||
               (r.AdminOwnerNavigation != null && r.AdminOwnerNavigation.UserName.Contains(search)));
        }

        return View(await retailQuery.ToListAsync());
    }

    // GET: RETAILS/Create
    public IActionResult Create()
    {
        ViewData["StatusId"] = new SelectList(_context.Statuses, "StatusId", "StatusName");
        ViewData["AdminOwner"] = new SelectList(_context.Users, "Id", "UserName");
        return View();
    }

    // POST: RETAILS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("RetailName,Address,Passcode")] Retail retail)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(currentUserId))
        {
            return Challenge(); // Prompts login if session expired
        }

        ModelState.Remove("AdminOwnerId");
        ModelState.Remove("AdminOwner");
        ModelState.Remove("Status");
        ModelState.Remove("StatusId");

        retail.AdminOwner = currentUserId;
        retail.StatusId = 1;

        if (ModelState.IsValid)
        {
            _context.Add(retail);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        return View(retail);
    }

    // GET: RETAILS/Edit/5
    public async Task<IActionResult> Edit(int? retailid)
    {
        if (retailid == null)
        {
            return NotFound();
        }

        var retail = await _context.Retails.FindAsync(retailid);
        if (retail == null)
        {
            return NotFound();
        }
        ViewData["StatusId"] = new SelectList(_context.Statuses, "StatusId", "StatusName");
        return View(retail);
    }

    // POST: RETAILS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? retailid, [Bind("RetailId,RetailName,Passcode,Address,StatusId")] Retail retail)
    {
        ModelState.Remove("AdminOwner");
        ModelState.Remove("AdminOwnerId");
        ModelState.Remove("AdminOwnerNavigation");
        ModelState.Remove("Status");
        ModelState.Remove("StatusNavigation");
        ModelState.Remove("Inventories");
        ModelState.Remove("RetailStaffs");
        ModelState.Remove("Transactions");

        if (retailid != retail.RetailId)
        {
            return NotFound();
        }  

        if (ModelState.IsValid)
        {
            var existRetail = await _context.Retails.FindAsync(retailid);
            if (existRetail == null)
            {
                return NotFound();
            }

            existRetail.RetailName = retail.RetailName;
            existRetail.Passcode = retail.Passcode;
            existRetail.Address = retail.Address;
            existRetail.StatusId = retail.StatusId;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!RetailExists(retail.RetailId))
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

        return View(retail);
    }

    // GET: RETAILS/Delete/5
    public async Task<IActionResult> Delete(int? retailid)
    {
        if (retailid == null)
        {
            return NotFound();
        }

        var retail = await _context.Retails
            .FirstOrDefaultAsync(m => m.RetailId == retailid);
        if (retail == null)
        {
            return NotFound();
        }

        return View(retail);
    }

    // POST: RETAILS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? retailid)
    {
        var retail = await _context.Retails.FindAsync(retailid);
        if (retail == null)
        {
            return NotFound();
        }

        try
        {
            _context.Retails.Remove(retail);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Retail deleted successfully.";
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Cannot delete this retail because there is an existing inventory attached to it.";
        }
        return RedirectToAction(nameof(Index));
    }

    private bool RetailExists(int? retailid)
    {
        return _context.Retails.Any(e => e.RetailId == retailid);
    }
}
