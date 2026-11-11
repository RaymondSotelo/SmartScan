using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartScan.Data;
using SmartScan.Models.ViewModels;
using Microsoft.AspNetCore.Http;

namespace SmartScan.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;

        public UsersController(UserManager<IdentityUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }


        public async Task<IActionResult> Index(string? search)
        {
            var vm = new DashboardViewModel
            {
                CurrentFilter = search
            };

            // Get the current logged-in user's ID
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Only stores owned by or assigned to the current user (owner or co-owner/staff)
            var userStoresQuery = _context.Retails.Where(r =>
                r.AdminOwner == currentUserId ||
                _context.RetailStaffs.Any(rs => rs.RetailId == r.RetailId && rs.StaffId == currentUserId)
            );

            // List of store IDs accessible to the current user
            var userRetailIds = await userStoresQuery.Select(r => r.RetailId).ToListAsync();

            // Calculate KPIs for Store Outlets (unaffected by search)
            vm.TotalRetailCount = await userStoresQuery.CountAsync();
            vm.ActiveRetailCount = await userStoresQuery.CountAsync(r => r.StatusId == 1);

            // Build the Retail Overview list from the base user query
            var retailQuery = userStoresQuery
                .Include(r => r.Status)
                .Include(r => r.AdminOwnerNavigation)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                retailQuery = retailQuery.Where(r =>
                    r.RetailName.Contains(search) ||
                    (r.Address != null && r.Address.Contains(search)));
            }

            vm.Retails = await retailQuery.ToListAsync();

            // Fetch Profile Map for the displayed retails
            var ownerIds = vm.Retails
                .Select(r => r.AdminOwner)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            vm.ProfileMap = await _context.UserProfiles
                .Where(up => ownerIds.Contains(up.UserId) && !string.IsNullOrEmpty(up.ProfilePicture))
                .GroupBy(up => up.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First().ProfilePicture, StringComparer.OrdinalIgnoreCase);

            // ===== SCOPED CUSTOMER KPIS (RESTRICTED ONLY TO CURRENT USER'S / CO-OWNED STORES) =====
            var scopedCustomers = _context.Customers
                .Where(c => userRetailIds.Contains(c.RetailId));

            // Total Customers registered in user's stores
            vm.TotalCustomers = await scopedCustomers.CountAsync();

            // Verified Shoppers KPI: Enrolled face descriptor vector and active status in user's stores
            vm.VerifiedCustomersCount = await scopedCustomers
                .CountAsync(c => !string.IsNullOrEmpty(c.FaceEmbedding) && c.StatusId == 1);

            // Receivables KPI: Sum of outstanding debt strictly across user's store registrations
            vm.TotalOutstandingDebt = await scopedCustomers
                .SumAsync(c => (decimal?)c.CurrentDebt) ?? 0.00m;

            // Establish date baselines
            var todayDateOnly = DateOnly.FromDateTime(DateTime.Today);
            var todayStartUtc = DateTime.UtcNow.Date;

            // Today's Sales scoped to user's retail stores
            vm.TodaysSales = await _context.Transactions
                .Where(t => userRetailIds.Contains(t.RetailId) && t.CreatedAt >= todayStartUtc)
                .SumAsync(t => (decimal?)t.TotalAmount) ?? 0.00m;

            // Overdue Debt Watchlist scoped to user's stores
            vm.OverdueAccounts = await scopedCustomers
                .AsNoTracking()
                .Where(c => c.CurrentDebt > 0 &&
                            c.Transactions.Any(t => userRetailIds.Contains(t.RetailId) && t.DueDate.HasValue && t.DueDate.Value < todayDateOnly))
                .Select(c => new OverdueDebtItem
                {
                    CustomerId = c.CustomerId,
                    FullName = c.FullName,
                    ContactNumber = c.ContactNumber,
                    AmountDue = c.CurrentDebt,
                    DueDate = c.Transactions
                        .Where(t => userRetailIds.Contains(t.RetailId) && t.DueDate.HasValue && t.DueDate.Value < todayDateOnly)
                        .Min(t => t.DueDate)!.Value
                })
                .OrderBy(c => c.DueDate)
                .Take(4)
                .ToListAsync();

            vm.OverdueDebtAmount = await scopedCustomers
                .Where(c => c.CurrentDebt > 0 &&
                            c.Transactions.Any(t => userRetailIds.Contains(t.RetailId) && t.DueDate.HasValue && t.DueDate.Value < todayDateOnly))
                .SumAsync(c => c.CurrentDebt);

            // Newly Registered Customer Feed (Replaced PhilSys Verification Log)
            vm.RecentVerifications = await scopedCustomers
                .AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .Take(4)
                .Select(c => new ComplianceNoticeItem
                {
                    CustomerMaskedName = c.FullName,
                    IsUnderage = false,
                    RegisteredAt = c.CreatedAt
                })
                .ToListAsync();

            return View(vm);
        }

        // [UPDATE THIS LATER TO MANAGE ADMIN ROLE] <<--
        // Currently managing Staff Role <--
        public async Task<IActionResult> UserManagement(string search)
        {
            ViewData["CurrentFilter"] = search;

            // Fetch cashier role accounts
            var cashierRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Cashier");
            if (cashierRole == null)
            {
                return View(new List<IdentityUser>());
            }

            // Query only cashier accounts
            var cashierQuery = (from user in _context.Users
                         join userRole in _context.UserRoles on user.Id equals userRole.UserId
                         where userRole.RoleId == cashierRole.Id
                         select user)
                 .AsNoTracking()
                 .AsQueryable();

            // Search Filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                cashierQuery = cashierQuery.Where(u =>
                    (u.UserName != null && u.UserName.Contains(search)) ||
                    (u.Email != null && u.Email.Contains(search))
                );
            }

            var cashier = await cashierQuery.OrderBy(u => u.UserName).ToListAsync();

            return View(cashier);
        }

        public async Task<IActionResult> AdminApproval(string search)
        {
            ViewData["CurrentFilter"] = search;

            // Retrieve the current logged in admin
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Challenge();
            }

            // Fetch admin role accounts 
            var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
            if (adminRole == null)
            {
                return View(new List<ApplicationUser>());
            }

            // Query users that have not been approved yet and are in the Admin role
            var adminQuery = (from user in _context.Users
                              join userRole in _context.UserRoles on user.Id equals userRole.UserId
                              where userRole.RoleId == adminRole.Id
                                    && !user.EmailConfirmed
                                    && user.Id != currentUserId
                              select user)
                      .AsNoTracking()
                      .AsQueryable();

            // Search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                adminQuery = adminQuery.Where(u =>
                    (u.UserName != null && u.UserName.Contains(search)) ||
                    (u.Email != null && u.Email.Contains(search))
                );
            }

            var pendingAdmins = await adminQuery.OrderBy(u => u.UserName).ToListAsync();
            return View(pendingAdmins);
        }

        public async Task<IActionResult> Approve(string id)
        {
            if(id == null)
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);

            if(user == null)
            {
                return NotFound();
            }

            user.EmailConfirmed = true;
            var result = await _userManager.UpdateAsync(user);

            if(result.Succeeded)
            {
                TempData["UserMessage"] = $"Successfully approved {user.UserName}!";
            }

            return RedirectToAction(nameof(AdminApproval));
        }

        public async Task<IActionResult> Unblock(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            user.EmailConfirmed = true;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["UserMessage"] = $"Successfully approved {user.UserName}!";
            }

            return RedirectToAction(nameof(UserManagement));
        }

        public async Task<IActionResult> Block(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            user.EmailConfirmed = false;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["UserMessage2"] = $"Successfully disabled {user.UserName}!";
            }

            return RedirectToAction(nameof(UserManagement));
        }
    }
}
