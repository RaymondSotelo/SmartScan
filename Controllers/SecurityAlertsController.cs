using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartScan.Data;
using SmartScan.Models;

namespace SmartScan.Controllers
{
    [Authorize(Roles = "Admin,Cashier")]
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
            var currentUserName = User.Identity?.Name;
            var isCashier = User.IsInRole("Cashier");
            var isAdmin = User.IsInRole("Admin");

            ViewBag.ReadAlertIds = await _context.AdminAlertReads
                .Where(ar => ar.AdminId == currentUserId && ar.IsRead)
                .Select(ar => ar.AlertId)
                .ToHashSetAsync();

            bool isCashierOnly = isCashier && !isAdmin;
            var userRetailIds = await GetPermittedRetailIdsAsync(currentUserId!, isCashierOnly);

            var alertsQuery = _context.SecurityAlerts
                .Include(a => a.AlertType)
                .Include(a => a.Retail)
                .Where(a => a.RetailId.HasValue && userRetailIds.Contains(a.RetailId.Value));

            if (!string.IsNullOrEmpty(currentUserName))
            {
                alertsQuery = alertsQuery.Where(a => !a.Description.Contains(currentUserName));
            }

            // Restrict Cashiers to Low Stock (4), Depleted (5), and Anomaly (6)
            if (isCashier && !isAdmin)
            {
                var allowedTypes = new List<int> { 4, 5, 6 };
                alertsQuery = alertsQuery.Where(a => allowedTypes.Contains(a.AlertTypeId));
            }

            var alerts = await alertsQuery
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.AlertId)
                .AsNoTracking()
                .ToListAsync();

            return View(alerts);
        }

        // MarkAsRead
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int alertId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Json(new { success = false });

            var existingRead = await _context.AdminAlertReads
                .FirstOrDefaultAsync(ar => ar.AlertId == alertId && ar.AdminId == currentUserId);

            if (existingRead == null)
            {
                _context.AdminAlertReads.Add(new AdminAlertRead
                {
                    AlertId = alertId,
                    AdminId = currentUserId,
                    IsRead = true
                });
            }
            else
            {
                existingRead.IsRead = true;
                _context.AdminAlertReads.Update(existingRead);
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // GetLiveAlerts
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

            bool isCashierOnly = isCashier && !isAdmin;
            var userRetailIds = await GetPermittedRetailIdsAsync(currentUserId!, isCashierOnly);

            var alertsQuery = _context.SecurityAlerts
                .Include(a => a.AlertType)
                .Where(a => a.RetailId.HasValue && userRetailIds.Contains(a.RetailId.Value))
                .Where(a => !_context.AdminAlertReads.Any(read => read.AlertId == a.AlertId && read.AdminId == currentUserId && read.IsRead));

            if (!string.IsNullOrEmpty(currentUserName))
            {
                alertsQuery = alertsQuery.Where(a => !a.Description.Contains(currentUserName));
            }

            bool restrictActionUrl = isCashier && !isAdmin;
            if (restrictActionUrl)
            {
                var allowedTypes = new List<int> { 4, 5, 6 };
                alertsQuery = alertsQuery.Where(a => allowedTypes.Contains(a.AlertTypeId));
            }

            var topAlerts = await alertsQuery
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.AlertId)
                .Select(a => new
                {
                    alertId = a.AlertId,
                    alertTypeName = a.AlertType != null ? a.AlertType.AlertTypeName : $"Type {a.AlertTypeId}",
                    storeName = a.Retail != null ? a.Retail.RetailName : "System",
                    description = a.Description,
                    // Prevent Cashiers from navigating to Admin-only pages like /Inventories/Index
                    actionUrl = (restrictActionUrl || string.IsNullOrEmpty(a.ActionUrl)) ? "#" : a.ActionUrl,
                    timeString = a.CreatedAt.ToString("o")
                })
                .ToListAsync();

            return Json(new { count = topAlerts.Count, alerts = topAlerts });
        }

        // MarkAllAsRead
        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUserName = User.Identity?.Name;
            var isCashier = User.IsInRole("Cashier");
            var isAdmin = User.IsInRole("Admin");

            if (string.IsNullOrEmpty(currentUserId)) return Json(new { success = false });

            bool isCashierOnly = isCashier && !isAdmin;
            var userRetailIds = await GetPermittedRetailIdsAsync(currentUserId!, isCashierOnly);

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

            var unreadAlertIds = await alertsQuery.Select(a => a.AlertId).ToListAsync();

            if (unreadAlertIds.Any())
            {
                var existingReads = await _context.AdminAlertReads
                    .Where(ar => ar.AdminId == currentUserId && unreadAlertIds.Contains(ar.AlertId))
                    .ToListAsync();

                foreach (var existing in existingReads)
                {
                    existing.IsRead = true;
                }

                var existingReadAlertIds = existingReads.Select(e => e.AlertId).ToHashSet();
                var newAlertIds = unreadAlertIds.Where(id => !existingReadAlertIds.Contains(id));

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

        //Helper method
        private async Task<List<int>> GetPermittedRetailIdsAsync(string currentUserId, bool isCashierOnly)
        {
            if (isCashierOnly)
            {
                return await _context.RetailStaffs
                    .Where(rs => rs.StaffId == currentUserId && rs.StatusId == 1)
                    .Select(rs => rs.RetailId)
                    .Distinct()
                    .ToListAsync();
            }

            return await _context.Retails
                .Where(r => r.AdminOwner == currentUserId ||
                            _context.RetailStaffs.Any(rs => rs.RetailId == r.RetailId && rs.StaffId == currentUserId && rs.StatusId == 1))
                .Select(r => r.RetailId)
                .ToListAsync();
        }
    }
}