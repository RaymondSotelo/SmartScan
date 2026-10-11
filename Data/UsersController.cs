using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartScan.Data
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


        public async Task<IActionResult> Index()
        {
            // Retrieve the current logged in admin
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Challenge(); // Prompts login if session is invalid
            }

            // Query only the retail records that belong to this admin
            var adminRetail = await _context.Retails
                .Include(r => r.Status)
                .Where(r => r.AdminOwner == currentUserId)
                .AsNoTracking()
                .ToListAsync();

            return View(adminRetail);
        }

        // [UPDATE THIS LATER TO MANAGE ADMIN ROLE] <<--
        // Currently managing Staff Role <--
        public async Task<IActionResult> User_Management()
        {
            var users = _userManager.Users.ToListAsync();
            var currentUsers = new List<IdentityUser>();

            foreach (var user in await users)
            {
                if (await _userManager.IsInRoleAsync(user, "Cashier"))
                {
                    currentUsers.Add(user);
                }
            }
            return View(currentUsers);
        }

        public async Task<IActionResult> Admin_Approval()
        {
            var unconfirmAdminUser = _userManager.Users.Where(u => u.EmailConfirmed == false);
            var pendingAdmin = new List<IdentityUser>();

            foreach (var user in await unconfirmAdminUser.ToListAsync())
            {
                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    pendingAdmin.Add(user);
                }
            }
            return View(pendingAdmin);
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

            return RedirectToAction(nameof(User_Management));
        }

        public async Task<IActionResult> Disable(string id)
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
                TempData["UserMessage2"] = $"Successfully disables {user.UserName}!";
            }

            return RedirectToAction(nameof(User_Management));
        }
    }
}
