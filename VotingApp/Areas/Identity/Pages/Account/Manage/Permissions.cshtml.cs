using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VotingApp.Helpers;
using static VotingApp.Helpers.Helper;

namespace VotingApp.Areas.Identity.Pages.Account.Manage
{
    public class PermissionsModel : PageModel
    {
        public IList<PermissionItem> PermissionItems { get; set; }
        [TempData]
        public string StatusMessage { get; set; }


        public void OnGet()
        {
            PermissionItems = Permission.List().OrderBy(m => m.Description).ToList();
        }
    }
}
