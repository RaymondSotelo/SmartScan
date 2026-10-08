using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartScan.Data;

namespace SmartScan.Services
{
    public class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<IdentityUser, IdentityRole>
    {
        private readonly ApplicationDbContext _context;

        public ApplicationUserClaimsPrincipalFactory(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> optionsAccessor,
            ApplicationDbContext context)
            : base(userManager, roleManager, optionsAccessor)
        {
            _context = context;
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(IdentityUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);

            var profile = await _context.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (profile != null && profile.UseAsDisplayName)
            {
                var fullName = $"{profile.FirstName} {profile.LastName}".Trim();
                if (!string.IsNullOrWhiteSpace(fullName))
                {
                    // Remove default username/email claim
                    var existingNameClaim = identity.FindFirst(ClaimTypes.Name);
                    if (existingNameClaim != null)
                    {
                        identity.RemoveClaim(existingNameClaim);
                    }

                    // Replace with the user's Full Name
                    identity.AddClaim(new Claim(ClaimTypes.Name, fullName));
                }
            }

            return identity;
        }
    }
}
