using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartScan.Data;
using SmartScan.Models;

namespace SmartScan.Areas.Identity.Pages.Account.Manage
{
    public class DisplayModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly SignInManager<IdentityUser> _signInManager;

        public DisplayModel(UserManager<IdentityUser> userManager, ApplicationDbContext context, IWebHostEnvironment environment, SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _context = context;
            _environment = environment;
            _signInManager = signInManager;
        }

        [TempData]
        public string? StatusMessage { get; set; }
        public string? CurrentProfilePicture { get; set; }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required(ErrorMessage = "First name is required.")]
            [StringLength(100, ErrorMessage = "The {0} must be at most {1} characters long.")]
            [Display(Name = "First Name")]
            public string FirstName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Last name is required.")]
            [StringLength(100, ErrorMessage = "The {0} must be at most {1} characters long.")]
            [Display(Name = "Last Name")]
            public string LastName { get; set; } = string.Empty;

            [Display(Name = "Profile Picture")]
            public IFormFile? ProfileImageFile { get; set; }

            public bool SetAsDisplayName { get; set; }
        }

        public async Task LoadAsync(IdentityUser user)
        {
            var profile = await _context.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (profile != null)
            {
                Input = new InputModel
                {
                    FirstName = profile.FirstName,
                    LastName = profile.LastName,
                    SetAsDisplayName = profile.UseAsDisplayName
                };
                CurrentProfilePicture = profile.ProfilePicture;
            }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            var profile = await _context.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null)
            {
                // Insert new profile
                profile = new UserProfile
                {
                    UserId = user.Id,
                    FirstName = Input.FirstName.Trim(),
                    LastName = Input.LastName.Trim(),
                    UseAsDisplayName = Input.SetAsDisplayName
                };
                _context.UserProfiles.Add(profile);
            }
            else
            {
                // Update existing profile
                profile.FirstName = Input.FirstName?.Trim() ?? string.Empty;
                profile.LastName = Input.LastName?.Trim() ?? string.Empty;
                profile.UseAsDisplayName = Input.SetAsDisplayName;
            }

            // Handle profile picture upload
            if (Input.ProfileImageFile != null && Input.ProfileImageFile.Length > 0)
            {
                // Validate file size maximum 2MB
                const long maxFileSize = 2 * 1024 * 1024;
                if (Input.ProfileImageFile.Length > maxFileSize)
                {
                    ModelState.AddModelError("Input.ProfileImageFile", "The profile picture must be less than 2MB.");
                    await LoadAsync(user);
                    return Page();
                }

                // Validate file extension
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(Input.ProfileImageFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("Input.ProfileImageFile", "Only .jpg, .jpeg, .png, and .webp files are allowed.");
                    await LoadAsync(user);
                    return Page();
                }

                // Resolve destination directory
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "profiles");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Delete old picture if it exists and is NOT the default image
                if (!string.IsNullOrEmpty(profile.ProfilePicture) &&
                    !profile.ProfilePicture.Contains("default-pfp.png", StringComparison.OrdinalIgnoreCase))
                {
                    var oldFilePath = Path.Combine(_environment.WebRootPath, profile.ProfilePicture.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                // Generate secure file name and save
                var uniqueFileName = $"profile_{user.Id}_{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await Input.ProfileImageFile.CopyToAsync(stream);
                }

                profile.ProfilePicture = $"/uploads/profiles/{uniqueFileName}";
            }
            else if (string.IsNullOrWhiteSpace(profile.ProfilePicture))
            {
                // Fallback to default picture if none exists
                profile.ProfilePicture = "/images/default-profile-pic.png";
            }

            await _context.SaveChangesAsync();

            // Refresh cookie claim with new display name
            await _signInManager.RefreshSignInAsync(user);

            StatusMessage = "Your display profile has been updated.";
            return RedirectToPage();
        }
    }
}