using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartScan.Data;
using SmartScan.Models.ViewModels;

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

            // 1. Get the current logged-in user's ID
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 2. Base Query: Only stores owned by or assigned to the current user
            var userStoresQuery = _context.Retails.Where(r =>
                r.AdminOwner == currentUserId ||
                _context.RetailStaffs.Any(rs => rs.RetailId == r.RetailId && rs.StaffId == currentUserId)
            );

            // 3. Calculate KPIs for Store Outlets (unaffected by search)
            vm.TotalRetailCount = await userStoresQuery.CountAsync();
            vm.ActiveRetailCount = await userStoresQuery.CountAsync(r => r.StatusId == 1);

            // 4. Build the Retail Overview list from the base user query
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

            // 5. Fetch Profile Map for the displayed retails
            var ownerIds = vm.Retails
                .Select(r => r.AdminOwner)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            vm.ProfileMap = await _context.UserProfiles
                .Where(up => ownerIds.Contains(up.UserId) && !string.IsNullOrEmpty(up.ProfilePicture))
                .GroupBy(up => up.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First().ProfilePicture, StringComparer.OrdinalIgnoreCase);


            // --- Remaining KPIs (Customers, Sales, Overdue) ---
            vm.TotalCustomers = await _context.Customers.CountAsync();
            vm.VerifiedCustomersCount = await _context.Customers.CountAsync(c => c.IsIdVerified);
            vm.UnderageCustomersCount = await _context.Customers.CountAsync(c => c.StatusId == 3);
            vm.TotalOutstandingDebt = await _context.Customers.SumAsync(c => c.CurrentDebt);

            // Establish date baselines
            var todayDateOnly = DateOnly.FromDateTime(DateTime.Today);
            var todayStartUtc = DateTime.UtcNow.Date; // Matches UTC CreatedAt stamp

            // Today's Sales (DateTime comparison)
            vm.TodaysSales = await _context.Transactions
                .Where(t => t.CreatedAt >= todayStartUtc)
                .SumAsync(t => (decimal?)t.TotalAmount) ?? 0.00m;

            // Section 2 Critical Attention: Overdue Debts
            vm.OverdueAccounts = await _context.Customers
                .AsNoTracking()
                .Where(c => c.CurrentDebt > 0 &&
                            c.Transactions.Any(t => t.DueDate.HasValue && t.DueDate.Value < todayDateOnly))
                .Select(c => new OverdueDebtItem
                {
                    CustomerId = c.CustomerId,
                    FullName = c.FullName,
                    ContactNumber = c.ContactNumber,
                    AmountDue = c.CurrentDebt,
                    DueDate = c.Transactions
                        .Where(t => t.DueDate.HasValue && t.DueDate.Value < todayDateOnly)
                        .Min(t => t.DueDate)!.Value
                })
                .OrderBy(c => c.DueDate)
                .Take(4)
                .ToListAsync();

            vm.OverdueDebtAmount = await _context.Customers
                .Where(c => c.CurrentDebt > 0 &&
                            c.Transactions.Any(t => t.DueDate.HasValue && t.DueDate.Value < todayDateOnly))
                .SumAsync(c => c.CurrentDebt);

            // Section 2 Critical Attention: Recent Age-Gating & Verifications
            vm.RecentVerifications = await _context.Customers
                .AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .Take(4)
                .Select(c => new ComplianceNoticeItem
                {
                    CustomerMaskedName = c.FullName,
                    Age = c.Age ?? 0,
                    IsUnderage = c.StatusId == 3,
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
