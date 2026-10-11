
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartScan.Models;
using SmartScan.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using System.Data.Common;
using System.Security.Claims;

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
            .Include(rs => rs.Staff)
            .Include(rs => rs.Status)
            .Include(rs => rs.Retail)
            .Where(rs => rs.RetailId == retailId)
            .Where(rs => rs.StatusId == 1)
            .AsQueryable();

        // Store retailId
        ViewBag.RetailId = retailId;

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            retailStaffQuery = retailStaffQuery.Where(rs =>
                (rs.Retail != null && rs.Retail.RetailName.Contains(search)) ||
                (rs.Staff != null && rs.Staff.UserName.Contains(search)));
        }

        ViewBag.RetailName = storeName;
        ViewData["StaffId"] = new SelectList(_context.Users, "StaffId", "UserName");

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
        ViewData["CurrentFilter"] = search;

        // Get the store to identify the store owner (Head Admin)
        var retail = await _context.Retails
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RetailId == retailId.Value);

        if (retail == null)
        {
            return NotFound();
        }

        // Fetch all User IDs already assigned to ANY retail branch
        var assignedStaffIds = await _context.RetailStaffs
            .Where(rs => rs.StaffId != null)
            .Select(rs => rs.StaffId)
            .ToHashSetAsync();

        // Add the Head Admin's ID to the exclusion set so the owner isn't listed
        if (!string.IsNullOrEmpty(retail.AdminOwner))
        {
            assignedStaffIds.Add(retail.AdminOwner);
        }

        // Query all confirmed users from UserManager excluding assigned staff and the owner
        var availableUsersQuery = _userManager.Users
            .Where(u => u.EmailConfirmed && !assignedStaffIds.Contains(u.Id));

        // Apply server-side search if provided
        if (!string.IsNullOrWhiteSpace(search))
        {
            availableUsersQuery = availableUsersQuery.Where(u =>
                (u.UserName != null && u.UserName.Contains(search)) ||
                (u.Email != null && u.Email.Contains(search)));
        }

        var availableUsers = await availableUsersQuery.ToListAsync();

        // Pass the unassigned user list to the view
        ViewBag.CashierList = availableUsers;
        ViewData["StaffId"] = new SelectList(availableUsers, "Id", "UserName");

        return View();
    }

    // POST: RETAILSTAFFS/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("StaffId,StatusId,RetailId")] RetailStaff retailstaff)
    {
        // Fetch user directly so username is always available for notifications
        var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == retailstaff.StaffId);
        string username = targetUser?.UserName ?? "Staff member";

        // Check for an existing assignment in this specific store
        var existingStaff = await _context.RetailStaffs
            .FirstOrDefaultAsync(rs => rs.StaffId == retailstaff.StaffId && rs.RetailId == retailstaff.RetailId);

        if (existingStaff != null)
        {
            // Reactivate inactive record
            if (existingStaff.StatusId == 2)
            {
                existingStaff.StatusId = 1;
                _context.Update(existingStaff);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"{username} was previously inactive and has been successfully reactivated.";
                return RedirectToAction(nameof(Create), new { retailId = retailstaff.RetailId });
            }

            // Prevent duplicate active assignment
            TempData["ErrorMessage"] = $"{username} is already actively assigned to this retail.";
            return RedirectToAction(nameof(Create), new { retailId = retailstaff.RetailId });
        }
        else
        {
            // Create brand new active assignment
            retailstaff.StatusId = 1;
            _context.Add(retailstaff);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{username} has been assigned successfully.";
            return RedirectToAction(nameof(Create), new { retailId = retailstaff.RetailId });
        }
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

        var retailstaff = await _context.RetailStaffs
            .Include(rs => rs.Staff)
            .Include(rs => rs.Retail)
            .FirstOrDefaultAsync(rs => rs.RetailStaffId == retailstaffid);

        if (retailstaff == null)
        {
            return NotFound();
        }

        var retailId = retailstaff.RetailId;
        string username = retailstaff.Staff?.UserName ?? "Unknown User";

        try
        {
            _context.RetailStaffs.Remove(retailstaff);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{username} has been removed successfully.";
        }
        catch(DbUpdateException)
        {
            // Reset EF tracking state because .Remove() marked it as Deleted
            _context.Entry(retailstaff).State = EntityState.Unchanged;

            // Soft Delete
            retailstaff.StatusId = 2;
            _context.Entry(retailstaff).Property(rs => rs.StatusId).IsModified = true;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{username} has existing transactions and has been deactivated instead.";
        }

        return RedirectToAction(nameof(Index), new { retailId = retailId });
    }

    private bool RetailStaffExists(int? retailstaffid)
    {
        return _context.RetailStaffs.Any(e => e.RetailStaffId == retailstaffid);
    }
}
