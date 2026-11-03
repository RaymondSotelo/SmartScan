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

        // GET: SecurityAlerts/Index
        public async Task<IActionResult> Index(string search)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 1. Get the current user's username
            var currentUserName = User.Identity?.Name;

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
            var alertsQuery = _context.SecurityAlerts
                .Include(a => a.AlertType)
                .Include(a => a.Retail)
                .Where(a => a.RetailId.HasValue && userRetailIds.Contains(a.RetailId.Value));

            // 2. Filter out alerts where the description contains the current user's username
            if (!string.IsNullOrEmpty(currentUserName))
            {
                alertsQuery = alertsQuery.Where(a => !a.Description.Contains(currentUserName));
            }

            // 3. Sort by exact timestamp, with a fallback to the newest ID
            var alerts = await alertsQuery
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.AlertId)
                .AsNoTracking()
                .ToListAsync();

            return View(alerts);
        }

        // MarkAsRead | Marks the alert as read then filters it out from the unread page
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

        // GetLiveAlerts | Display current unread alerts live in the notification menu
        [HttpGet]
        public async Task<IActionResult> GetLiveAlerts()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUserName = User.Identity?.Name;
            var isCashier = User.IsInRole("Cashier");
            var isAdmin = User.IsInRole("Admin");

            if (string.IsNullOrEmpty(currentUserId))
            {
                return Json(new { count = 0, alerts = new List<object>() });
            }

            var userRetailIds = await _context.Retails
                .Where(r => r.AdminOwner == currentUserId || _context.RetailStaffs.Any(rs => rs.RetailId == r.RetailId && rs.StaffId == currentUserId))
                .Select(r => r.RetailId)
                .ToListAsync();

            var alertsQuery = _context.SecurityAlerts
                .Include(a => a.AlertType)
                .Where(a => a.RetailId.HasValue && userRetailIds.Contains(a.RetailId.Value))
                .Where(a => !_context.AdminAlertReads.Any(read => read.AlertId == a.AlertId && read.AdminId == currentUserId && read.IsRead));

            if (!string.IsNullOrEmpty(currentUserName))
            {
                alertsQuery = alertsQuery.Where(a => !a.Description.Contains(currentUserName));
            }

            if (isCashier && !isAdmin)
            {
                var allowedTypes = new List<int> { 4, 5, 6 };
                alertsQuery = alertsQuery.Where(a => allowedTypes.Contains(a.AlertTypeId));
            }

            var topAlerts = await alertsQuery
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.AlertId)
                .Select(a => new {
                    alertId = a.AlertId,
                    alertTypeName = a.AlertType != null ? a.AlertType.AlertTypeName : $"Type {a.AlertTypeId}",
                    storeName = a.Retail != null ? a.Retail.RetailName : "System",
                    description = a.Description,
                    actionUrl = string.IsNullOrEmpty(a.ActionUrl) ? "#" : a.ActionUrl,
                    timeString = a.CreatedAt.ToString("o")
                })
                .ToListAsync();

            return Json(new { count = topAlerts.Count, alerts = topAlerts });
        }

        // MarkAllAsRead | Marks all visible unread alerts as read for the current user
        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUserName = User.Identity?.Name;
            var isCashier = User.IsInRole("Cashier");
            var isAdmin = User.IsInRole("Admin");

            if (string.IsNullOrEmpty(currentUserId)) return Json(new { success = false });

            var userRetailIds = await _context.Retails
                .Where(r => r.AdminOwner == currentUserId || _context.RetailStaffs.Any(rs => rs.RetailId == r.RetailId && rs.StaffId == currentUserId))
                .Select(r => r.RetailId)
                .ToListAsync();

            var alertsQuery = _context.SecurityAlerts
                .Where(a => a.RetailId.HasValue && userRetailIds.Contains(a.RetailId.Value))
                .Where(a => !_context.AdminAlertReads.Any(read => read.AlertId == a.AlertId && read.AdminId == currentUserId && read.IsRead));

            if (!string.IsNullOrEmpty(currentUserName))
            {
                alertsQuery = alertsQuery.Where(a => !a.Description.Contains(currentUserName));
            }

            if (isCashier && !isAdmin)
            {
                var allowedTypes = new List<int> { 4, 5, 6 };
                alertsQuery = alertsQuery.Where(a => allowedTypes.Contains(a.AlertTypeId));
            }

            // Extract the IDs of all alerts that currently appear unread to this user
            var unreadAlertIds = await alertsQuery.Select(a => a.AlertId).ToListAsync();

            if (unreadAlertIds.Any())
            {
                // 1. Fetch any existing tracking records for these alerts (where IsRead is false)
                var existingReads = await _context.AdminAlertReads
                    .Where(ar => ar.AdminId == currentUserId && unreadAlertIds.Contains(ar.AlertId))
                    .ToListAsync();

                // 2. Update existing records to true
                foreach (var existing in existingReads)
                {
                    existing.IsRead = true;
                }

                // 3. Filter down to only the alerts that have NO tracking record at all yet
                var existingReadAlertIds = existingReads.Select(e => e.AlertId).ToHashSet();
                var newAlertIds = unreadAlertIds.Where(id => !existingReadAlertIds.Contains(id));

                // 4. Generate and insert brand new tracking records for the missing ones
                var newReads = newAlertIds.Select(id => new AdminAlertRead
                {
                    AlertId = id,
                    AdminId = currentUserId,
                    IsRead = true
                });

                _context.AdminAlertReads.AddRange(newReads);
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }
    }
}
