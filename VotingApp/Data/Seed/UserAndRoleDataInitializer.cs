using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using VotingApp.Authorization.Claims;
using static VotingApp.Helpers.Helper;

namespace VotingApp.Data.Seed
{
    public static class UserAndRoleDataInitializer
    {
        /// <summary>
        /// Ensures the Admin role exists and holds every permission claim, and optionally creates
        /// the first admin user from configuration (Seed:AdminEmail / Seed:AdminPassword).
        /// Safe to run on every start-up.
        /// </summary>
        public static async Task SeedDataAsync(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager,
            IConfiguration configuration, ILogger logger)
        {
            var adminRole = await EnsureAdminRoleAsync(roleManager);
            await EnsureAdminPermissionsAsync(roleManager, adminRole, logger);
            await EnsureAdminUserAsync(userManager, configuration, logger);
        }

        private static async Task<IdentityRole> EnsureAdminRoleAsync(RoleManager<IdentityRole> roleManager)
        {
            var role = await roleManager.FindByNameAsync(Permission.ADMIN_ROLE);
            if (role != null) return role;

            role = new IdentityRole(Permission.ADMIN_ROLE);
            var result = await roleManager.CreateAsync(role);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("Could not create Admin role: " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
            return role;
        }

        private static async Task EnsureAdminPermissionsAsync(RoleManager<IdentityRole> roleManager, IdentityRole adminRole, ILogger logger)
        {
            var existing = (await roleManager.GetClaimsAsync(adminRole))
                .Where(c => c.Type == CustomClaimTypes.Permission)
                .Select(c => c.Value)
                .ToHashSet();

            foreach (var permission in Permission.List().Select(p => p.Name).Distinct())
            {
                if (existing.Contains(permission)) continue;
                await roleManager.AddClaimAsync(adminRole, new System.Security.Claims.Claim(CustomClaimTypes.Permission, permission));
                logger.LogInformation("Granted {Permission} to role {Role}", permission, adminRole.Name);
            }
        }

        private static async Task EnsureAdminUserAsync(UserManager<AppUser> userManager, IConfiguration configuration, ILogger logger)
        {
            var email = configuration["Seed:AdminEmail"];
            var password = configuration["Seed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return; // Nothing configured: don't create an account with a default password.
            }

            if (await userManager.FindByEmailAsync(email) != null) return;

            var user = new AppUser
            {
                UserName = email,
                Email = email,
                FirstName = configuration["Seed:AdminFirstName"],
                LastName = configuration["Seed:AdminLastName"],
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                logger.LogError("Could not create seed admin user {Email}: {Errors}", email,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }
            await userManager.AddToRoleAsync(user, Permission.ADMIN_ROLE);
            logger.LogInformation("Created seed admin user {Email}", email);
        }
    }
}
