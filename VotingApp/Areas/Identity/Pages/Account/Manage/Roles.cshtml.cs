using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VotingApp.Areas.Identity.Pages.Account.Manage
{
    public class RolesModel : PageModel
    {
        public IList<IdentityRole> Roles { get; set; }
        [TempData]
        public string StatusMessage { get; set; }


        private readonly RoleManager<IdentityRole> RoleManager;

        public RolesModel(RoleManager<IdentityRole> roleManager)
        {
            RoleManager = roleManager;
        }

       
        public IActionResult OnGet()
        {
            var identityRoles = RoleManager.Roles.ToList();
            if (identityRoles.Count()<=0)
            {
                return NotFound($"No roles");
            }

            Roles = identityRoles;


            return Page();
        }
    }
}
