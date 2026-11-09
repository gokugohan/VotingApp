using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VotingApp.Application.Security;
using VotingApp.Infrastructure.Identity;

namespace VotingApp.Controllers
{
    public sealed record UserRolesRow(AppUser User, IList<string> Roles);

    public sealed record RolePermissionsRow(IdentityRole Role, HashSet<string> Permissions);

    /// <summary>User-to-role assignment and role permissions. Admins only.</summary>
    [Authorize(Roles = Permissions.AdminRole)]
    public class AdminController : BaseController
    {
        private readonly UserManager<AppUser> users;
        private readonly RoleManager<IdentityRole> roles;

        public AdminController(UserManager<AppUser> users, RoleManager<IdentityRole> roles)
        {
            this.users = users;
            this.roles = roles;
        }

        public async Task<IActionResult> Users()
        {
            var rows = new List<UserRolesRow>();
            foreach (var user in await users.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync(HttpContext.RequestAborted))
            {
                rows.Add(new UserRolesRow(user, await users.GetRolesAsync(user)));
            }
            ViewData["AllRoles"] = await roles.Roles.AsNoTracking().OrderBy(r => r.Name).Select(r => r.Name).ToListAsync(HttpContext.RequestAborted);
            return View(rows);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetUserRoles(string userId, List<string> roleNames)
        {
            var user = await users.FindByIdAsync(userId);
            if (user == null) return NotFound();
            var wanted = (roleNames ?? new List<string>()).ToHashSet();

            if (user.Id == users.GetUserId(User) && !wanted.Contains(Permissions.AdminRole))
            {
                Flash("You cannot remove the Admin role from your own account.", error: true);
                return RedirectToAction(nameof(Users));
            }

            var current = await users.GetRolesAsync(user);
            var toAdd = wanted.Except(current).Where(r => roles.Roles.Any(x => x.Name == r)).ToList();
            var toRemove = current.Except(wanted).ToList();
            var add = await users.AddToRolesAsync(user, toAdd);
            var remove = await users.RemoveFromRolesAsync(user, toRemove);
            var ok = add.Succeeded && remove.Succeeded;
            // Role changes take effect at the user's next sign-in; refresh the security stamp to force it.
            if (ok) await users.UpdateSecurityStampAsync(user);
            Flash(ok ? $"Roles updated for {user.Email}." : "Could not update roles.", error: !ok);
            return RedirectToAction(nameof(Users));
        }

        public async Task<IActionResult> Roles()
        {
            var rows = new List<RolePermissionsRow>();
            foreach (var role in await roles.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(HttpContext.RequestAborted))
            {
                var claims = await roles.GetClaimsAsync(role);
                rows.Add(new RolePermissionsRow(role, claims.Where(c => c.Type == CustomClaimTypes.Permission).Select(c => c.Value).ToHashSet()));
            }
            return View(rows);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(string name)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name) || await roles.RoleExistsAsync(name))
            {
                Flash("Enter a new, unique role name.", error: true);
                return RedirectToAction(nameof(Roles));
            }
            var result = await roles.CreateAsync(new IdentityRole(name));
            Flash(result.Succeeded ? $"Role {name} created. Now choose its permissions." : "Could not create the role.", error: !result.Succeeded);
            return RedirectToAction(nameof(Roles));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetRolePermissions(string roleId, List<string> permissions)
        {
            var role = await roles.FindByIdAsync(roleId);
            if (role == null) return NotFound();
            if (role.Name == Permissions.AdminRole)
            {
                Flash("The Admin role always has every permission.", error: true);
                return RedirectToAction(nameof(Roles));
            }

            var wanted = (permissions ?? new List<string>()).Where(Permissions.IsKnown).ToHashSet();
            var claims = (await roles.GetClaimsAsync(role)).Where(c => c.Type == CustomClaimTypes.Permission).ToList();
            foreach (var claim in claims.Where(c => !wanted.Contains(c.Value)))
            {
                await roles.RemoveClaimAsync(role, claim);
            }
            foreach (var permission in wanted.Except(claims.Select(c => c.Value)))
            {
                await roles.AddClaimAsync(role, new Claim(CustomClaimTypes.Permission, permission));
            }
            Flash($"Permissions saved for {role.Name}.");
            return RedirectToAction(nameof(Roles));
        }
    }
}
