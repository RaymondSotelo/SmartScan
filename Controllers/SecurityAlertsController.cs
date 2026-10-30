using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartScan.Data;
using SmartScan.Models;

namespace SmartScan.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SecurityAlertsController : Controller
    {
        private readonly ApplicationDbContext _context; 

        public SecurityAlertsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string search)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Get the IDs of all alerts this admin has already read
            ViewBag.ReadAlertIds = await _context.AdminAlertReads
                .Where(ar => ar.AdminId == currentUserId && ar.IsRead)
                .Select(ar => ar.AlertId)
                .ToHashSetAsync();

            // Fetch all RetailIds the user owns OR is assigned to as a Co-Admin/Staff
            var userRetailIds = await _context.Retails
                .Where(r => r.AdminOwner == currentUserId ||
                            _context.RetailStaffs.Any(rs => rs.RetailId == r.RetailId && rs.StaffId == currentUserId))
                .Select(r => r.RetailId)
                .ToListAsync();

            // Query alerts tied to those specific stores
            var alerts = await _context.SecurityAlerts
                .Include(a => a.AlertType)
                .Include(a => a.Retail)    
                .Where(a => a.RetailId.HasValue && userRetailIds.Contains(a.RetailId.Value))
                .OrderByDescending(a => a.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            return View(alerts);
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int alertId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Json(new { success = false });

            // Check if a read record already exists for this alert and this specific admin
            var existingRead = await _context.AdminAlertReads
                .FirstOrDefaultAsync(ar => ar.AlertId == alertId && ar.AdminId == currentUserId);

            if (existingRead == null)
            {
                var newReadRecord = new AdminAlertRead
                {
                    AlertId = alertId,
                    AdminId = currentUserId,
                    IsRead = true
                };
                _context.AdminAlertReads.Add(newReadRecord);
            }
            else
            {
                existingRead.IsRead = true;
                _context.AdminAlertReads.Update(existingRead);
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
