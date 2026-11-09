using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VotingApp.Application.Security;

namespace VotingApp.Infrastructure.Identity
{
    /// <summary>
    /// Ensures the Admin role exists and holds every permission claim, and optionally creates the
    /// first admin user from configuration (Seed:AdminEmail / Seed:AdminPassword). Idempotent.
    /// </summary>
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager,
            IConfiguration configuration, ILogger logger)
        {
            var adminRole = await EnsureAdminRoleAsync(roleManager);
            await EnsureAdminPermissionsAsync(roleManager, adminRole, logger);
            await EnsureAdminUserAsync(userManager, configuration, logger);
        }

        private static async Task<IdentityRole> EnsureAdminRoleAsync(RoleManager<IdentityRole> roleManager)
        {
            var role = await roleManager.FindByNameAsync(Permissions.AdminRole);
            if (role != null) return role;

            role = new IdentityRole(Permissions.AdminRole);
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

            foreach (var permission in Permissions.All.Select(p => p.Name).Where(p => !existing.Contains(p)))
            {
                await roleManager.AddClaimAsync(adminRole, new Claim(CustomClaimTypes.Permission, permission));
                logger.LogInformation("Granted {Permission} to role {Role}", permission, adminRole.Name);
            }
        }

        private static async Task EnsureAdminUserAsync(UserManager<AppUser> userManager, IConfiguration configuration, ILogger logger)
        {
            var email = configuration["Seed:AdminEmail"];
            var password = configuration["Seed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
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
            await userManager.AddToRoleAsync(user, Permissions.AdminRole);
            logger.LogInformation("Created seed admin user {Email}", email);
        }
    }
}
