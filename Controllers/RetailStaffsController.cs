
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
            .Where(rs => rs.StaffId != null && rs.StatusId == 1)
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
        if (ModelState.IsValid)
        {
            // Fetch user profile or user entity to resolve the display name
            var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == retailstaff.StaffId);
            string staffDisplayName = targetUser?.UserName ?? "A staff member";

            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(up => up.UserId == retailstaff.StaffId);
            if (userProfile != null && !string.IsNullOrWhiteSpace(userProfile.FirstName))
            {
                staffDisplayName = $"{userProfile.FirstName} {userProfile.LastName}".Trim();
            }

            // Fetch the store name
            var targetRetail = await _context.Retails.FirstOrDefaultAsync(r => r.RetailId == retailstaff.RetailId);
            string retailName = targetRetail?.RetailName ?? "Retail Store";

            // Check for existing assignment
            var existingStaff = await _context.RetailStaffs
                .FirstOrDefaultAsync(rs => rs.StaffId == retailstaff.StaffId && rs.RetailId == retailstaff.RetailId);

            if (existingStaff != null)
            {
                if (existingStaff.StatusId == 2)
                {
                    existingStaff.StatusId = 1;
                    _context.Update(existingStaff);

                    // Reactivation Alert
                    var reactivateAlert = new SecurityAlert
                    {
                        AlertTypeId = 7,
                        Description = $"{staffDisplayName}'s staff access has been restored at {retailName}",
                        CreatedAt = DateTime.Now,
                        RetailId = retailstaff.RetailId,
                        ActionUrl = Url.Action("Create", "RetailStaffs", new { retailId = retailstaff.RetailId })
                    };
                    _context.SecurityAlerts.Add(reactivateAlert);

                    await _context.SaveChangesAsync();

                    TempData["RetailStaffMessage"] = $"{staffDisplayName} was previously inactive and has been successfully reactivated.";
                    return RedirectToAction(nameof(Create), new { retailId = retailstaff.RetailId });
                }

                TempData["ErrorMessage"] = $"{staffDisplayName} is already actively assigned to this retail.";
                return RedirectToAction(nameof(Create), new { retailId = retailstaff.RetailId });
            }
            else
            {
                retailstaff.StatusId = 1;
                _context.Add(retailstaff);

                // New Assignment Alert 
                var newAlert = new SecurityAlert
                {
                    AlertTypeId = 1,
                    Description = $"{staffDisplayName} has been assigned to {retailName}",
                    CreatedAt = DateTime.Now,
                    RetailId = retailstaff.RetailId,
                    ActionUrl = Url.Action("Create", "RetailStaffs", new { retailId = retailstaff.RetailId })
                };
                _context.SecurityAlerts.Add(newAlert);

                await _context.SaveChangesAsync();

                TempData["RetailStaffMessage"] = $"{staffDisplayName} has been assigned successfully.";
                return RedirectToAction(nameof(Create), new { retailId = retailstaff.RetailId });
            }
        }

        TempData["ErrorMessage"] = "Failed to assign staff. Please check your inputs.";
        return RedirectToAction(nameof(Create), new { retailId = retailstaff.RetailId });
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
        string username = retailstaff.Staff?.UserName ?? "Staff member";
        string retailName = retailstaff.Retail?.RetailName ?? "Retail Store";
        string redirectUrl = Url.Action("Index", "RetailStaffs", new { retailId = retailId });

        try
        {
            // Queue removal
            _context.RetailStaffs.Remove(retailstaff);

            // Queue Alert (Hard Delete)
            var hardDeleteAlert = new SecurityAlert
            {
                AlertTypeId = 2,
                Description = $"{username} has been removed from {retailName}",
                CreatedAt = DateTime.Now,
                RetailId = retailId,
                ActionUrl = redirectUrl
            };
            _context.SecurityAlerts.Add(hardDeleteAlert);

            // Save both in one transaction
            await _context.SaveChangesAsync();

            TempData["RetailStaffMessage"] = $"{username} has been removed successfully.";
        }
        catch (DbUpdateException)
        {
            // Detach the failed removal attempt
            _context.Entry(retailstaff).State = EntityState.Unchanged;

            // Soft Delete (Deactivate)
            retailstaff.StatusId = 2;
            _context.Entry(retailstaff).Property(rs => rs.StatusId).IsModified = true;

            // Queue Alert (Soft Delete)
            var softDeleteAlert = new SecurityAlert
            {
                AlertTypeId = 2,
                Description = $"{username} has been deactivated and removed from active staff in {retailName}",
                CreatedAt = DateTime.Now,
                RetailId = retailId,
                ActionUrl = redirectUrl
            };
            _context.SecurityAlerts.Add(softDeleteAlert);

            await _context.SaveChangesAsync();

            TempData["RetailStaffMessage"] = $"{username} has existing records and has been deactivated instead.";
        }

        return RedirectToAction(nameof(Index), new { retailId = retailId });
    }

    private bool RetailStaffExists(int? retailstaffid)
    {
        return _context.RetailStaffs.Any(e => e.RetailStaffId == retailstaffid);
    }
}
