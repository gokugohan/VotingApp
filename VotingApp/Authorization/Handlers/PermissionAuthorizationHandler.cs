using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using System.Threading.Tasks;
using VotingApp.Application.Security;
using VotingApp.Authorization.Requirements;
using VotingApp.Infrastructure.Identity;

namespace VotingApp.Authorization.Handlers
{
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly UserManager<AppUser> userManager;
        private readonly RoleManager<IdentityRole> roleManager;

        public PermissionAuthorizationHandler(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            if (context.User?.Identity?.IsAuthenticated != true) return;

            var user = await this.userManager.GetUserAsync(context.User);
            if (user == null) return;

            if (await UserHasAnyPermissionAsync(user, requirement) ||
                await AnyRoleHasAnyPermissionAsync(user, requirement))
            {
                context.Succeed(requirement);
            }
        }

        private async Task<bool> UserHasAnyPermissionAsync(AppUser user, PermissionRequirement requirement)
        {
            var claims = await this.userManager.GetClaimsAsync(user);
            return claims.Any(c => c.Type == CustomClaimTypes.Permission && requirement.Permissions.Contains(c.Value));
        }

        private async Task<bool> AnyRoleHasAnyPermissionAsync(AppUser user, PermissionRequirement requirement)
        {
            // Look roles up one by one instead of roles.Where(r => names.Contains(r.Name)):
            // EF Core 8 translates Contains on a list to OPENJSON, which needs SQL Server compat level >= 130.
            foreach (var roleName in await this.userManager.GetRolesAsync(user))
            {
                var role = await this.roleManager.FindByNameAsync(roleName);
                if (role == null) continue;

                var claims = await this.roleManager.GetClaimsAsync(role);
                if (claims.Any(c => c.Type == CustomClaimTypes.Permission && requirement.Permissions.Contains(c.Value)))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
