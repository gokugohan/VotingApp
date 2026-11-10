using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VotingApp.Application.Common;
using VotingApp.Application.Elections;
using VotingApp.Application.Security;
using VotingApp.Domain.Elections;
using VotingApp.Models;

namespace VotingApp.Controllers
{
    /// <summary>Closed party lists for parliamentary elections: editing (managers) and public view.</summary>
    public class PartyListsController : BaseController
    {
        private readonly PartyListService lists;
        private readonly IAuthorizationService authz;

        public PartyListsController(PartyListService lists, IAuthorizationService authz)
        {
            this.lists = lists;
            this.authz = authz;
        }

        [Authorize(Policy = Permissions.ManageElections)]
        public async Task<IActionResult> Edit(Guid id)
        {
            var list = await lists.GetAsync(id, HttpContext.RequestAborted);
            if (list == null || list.Election.Type != ElectionType.Parliamentary) return NotFound();
            return View(new PartyListPage
            {
                List = list,
                Form = new PartyListForm
                {
                    ContestantId = id,
                    Effective = PartyListService.Format(list.Effective),
                    Substitutes = PartyListService.Format(list.Substitutes),
                },
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageElections)]
        public async Task<IActionResult> Edit([Bind(Prefix = "Form")] PartyListForm form)
        {
            var (effective, effectiveErrors) = PartyListService.Parse(form.Effective);
            var (substitutes, substituteErrors) = PartyListService.Parse(form.Substitutes);
            foreach (var e in effectiveErrors) ModelState.AddModelError("Form.Effective", "Effective candidates, " + e);
            foreach (var e in substituteErrors) ModelState.AddModelError("Form.Substitutes", "Substitutes, " + e);

            if (ModelState.IsValid)
            {
                var result = await lists.SaveAsync(form.ContestantId, effective, substitutes, HttpContext.RequestAborted);
                if (result.Succeeded)
                {
                    var errors = result.Value.Where(i => i.Severity == Domain.Elections.Rules.IssueSeverity.Error).ToList();
                    Flash(errors.Count == 0
                        ? $"List saved: {effective.Count} effective candidates and {substitutes.Count} substitutes."
                        : "List saved as a draft, but it does not meet the rules yet: " + string.Join(" ", errors.Select(i => i.Message)),
                        error: errors.Count > 0);
                    return RedirectToAction(nameof(Edit), new { id = form.ContestantId });
                }
                if (result.Failure == FailureKind.NotFound) return NotFound();
                AddErrors(result);
            }

            var list = await lists.GetAsync(form.ContestantId, HttpContext.RequestAborted);
            if (list == null) return NotFound();
            return View(new PartyListPage { List = list, Form = form });
        }

        /// <summary>Public view of a list once the election is open (drafts are not published).</summary>
        [AllowAnonymous]
        public async Task<IActionResult> Details(Guid id)
        {
            var list = await lists.GetAsync(id, HttpContext.RequestAborted);
            if (list == null || list.Election.Type != ElectionType.Parliamentary) return NotFound();
            var isManager = User.Identity?.IsAuthenticated == true && (await authz.AuthorizeAsync(User, Permissions.ManageElections)).Succeeded;
            if (list.Election.Status == ElectionStatus.Setup && !isManager) return NotFound();
            return View(list);
        }
    }
}
