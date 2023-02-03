using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using System.Threading.Tasks;
using VotingApp.Authorization.Claims;
using VotingApp.Authorization.Requirements;
using VotingApp.Data;

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
            if (context.User == null) return;

            // Get all the roles the user belongs to and check if any of the roles has the permission required
            // for the authorization to succeed.
            var user = await this.userManager.GetUserAsync(context.User);
            if (user == null) return;
            var userRoleNames = await this.userManager.GetRolesAsync(user);
            var userRoles = this.roleManager.Roles.Where(m => userRoleNames.Contains(m.Name));


            // Check if the roles wich user belongs to has a claims or wether user has claims to access the resource
            if (await IsRoleHasClaims(requirement, userRoles) || await IsUserHasClaims(requirement, user))
            {
                context.Succeed(requirement);
                return;
            }


        }

        private async Task<bool> IsUserHasClaims(PermissionRequirement requirement, AppUser user)
        {
            var userClaims = await this.userManager.GetClaimsAsync(user);

            var permissions = userClaims.Where(m => m.Type == CustomClaimTypes.Permission && m.Value == requirement.Permission).Select(m => m.Value);

            if (permissions.Any())
            {
                return true;
            }
            return false;
        }

        private async Task<bool> IsRoleHasClaims(PermissionRequirement requirement, IQueryable<IdentityRole> userRoles)
        {
            foreach (var role in userRoles)
            {
                var roleClaims = await this.roleManager.GetClaimsAsync(role);
                var permissions = roleClaims.Where(m => m.Type == CustomClaimTypes.Permission &&
                    m.Value == requirement.Permission).Select(m => m.Value);

                if (permissions.Any())
                {
                    return true;
                }
            }

            return false;
        }
    }
}
