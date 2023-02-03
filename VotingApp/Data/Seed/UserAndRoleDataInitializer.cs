using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VotingApp.Data.Seed
{
    public static class UserAndRoleDataInitializer
    {
        public static void SeedData(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            SeedRoles(roleManager);
            SeedUsers(userManager);
        }

        private static void SeedUsers(UserManager<AppUser> userManager)
        {
            if (userManager.FindByEmailAsync("hchebre@gmail.com").Result == null)
            {
                AppUser user = new AppUser();
                user.UserName = "hchebre@gmail.com";
                user.Email = "hchebre@gmail.com";
                user.FirstName = "Helder";
                user.LastName = "Chebre";

                IdentityResult result = userManager.CreateAsync(user, "Timor123").Result;

                if (result.Succeeded)
                {
                    userManager.AddToRoleAsync(user, "Admin").Wait();
                }
            }
        }

        private static void SeedRoles(RoleManager<IdentityRole> roleManager)
        {
            if (!roleManager.RoleExistsAsync("Admin").Result)
            {
                IdentityRole role = new IdentityRole();
                role.Name = "Admin";
                IdentityResult roleResult = roleManager.
                CreateAsync(role).Result;
            }

        }
    }
}
