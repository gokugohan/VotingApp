using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VotingApp.Application.Security;

namespace VotingApp.Areas.Identity.Pages.Account.Manage
{
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = VotingApp.Application.Security.Permissions.AdminRole)]
    public class PermissionsModel : PageModel
    {
        public IList<PermissionItem> PermissionItems { get; set; }
        [TempData]
        public string StatusMessage { get; set; }


        public void OnGet()
        {
            PermissionItems = Permissions.All.OrderBy(m => m.Group).ThenBy(m => m.Description).ToList();
        }
    }
}
