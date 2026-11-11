using System;
using System.Collections.Generic;
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
        private readonly ElectionResultsService results;

        public PartyListsController(PartyListService lists, IAuthorizationService authz, ElectionResultsService results)
        {
            this.lists = lists;
            this.authz = authz;
            this.results = results;
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
            foreach (var e in effectiveErrors) ModelState.AddModelError("Form.Effective", T["Effective candidates"] + ": " + T.Message(e));
            foreach (var e in substituteErrors) ModelState.AddModelError("Form.Substitutes", T["Substitutes"] + ": " + T.Message(e));

            if (ModelState.IsValid)
            {
                var result = await lists.SaveAsync(form.ContestantId, effective, substitutes, HttpContext.RequestAborted);
                if (result.Succeeded)
                {
                    var errors = result.Value.Where(i => i.Severity == Domain.Elections.Rules.IssueSeverity.Error).ToList();
                    Flash(errors.Count == 0
                        ? $"List saved: {effective.Count} effective candidates and {substitutes.Count} substitutes."
                        : T["List saved as a draft, but it does not meet the rules yet:"] + " " + string.Join(" ", errors.Select(i => T.Message(i.Message))),
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

        /// <summary>Seat holders after the election; record or undo a stand-down.</summary>
        [Authorize(Policy = Permissions.ManageElections)]
        public async Task<IActionResult> Seats(Guid id)
        {
            var page = await BuildSeatsPageAsync(id, null);
            return page == null ? NotFound() : View(page);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageElections)]
        public async Task<IActionResult> StepDown([Bind(Prefix = "Form")] StepDownForm form)
        {
            if (ModelState.IsValid)
            {
                var result = await lists.StepDownAsync(new StepDownInput(form.CandidateId.Value, form.Date.Value, form.Reason.Value, form.Note),
                    HttpContext.RequestAborted);
                if (result.Succeeded)
                {
                    Flash("Stepping down recorded. The seat passes to the next person on the list.");
                    return RedirectToAction(nameof(Seats), new { id = form.ContestantId });
                }
                if (result.Failure == FailureKind.NotFound) return NotFound();
                AddErrors(result);
            }
            var page = await BuildSeatsPageAsync(form.ContestantId, form);
            return page == null ? NotFound() : View(nameof(Seats), page);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageElections)]
        public async Task<IActionResult> Reinstate(Guid contestantId, Guid candidateId)
        {
            var result = await lists.ReinstateAsync(candidateId, HttpContext.RequestAborted);
            if (result.Failure == FailureKind.NotFound) return NotFound();
            Flash(result.Succeeded ? "Reinstated: the seat returns to them." : string.Join(" ", result.Errors.Select(T.Message)), error: !result.Succeeded);
            return RedirectToAction(nameof(Seats), new { id = contestantId });
        }

        private async Task<PartySeatsPage> BuildSeatsPageAsync(Guid contestantId, StepDownForm form)
        {
            var ct = HttpContext.RequestAborted;
            var list = await lists.GetAsync(contestantId, ct);
            if (list == null || list.Election.Type != ElectionType.Parliamentary) return null;
            var round = list.Election.Rounds.OrderBy(r => r.Number).First();
            var summary = await results.GetSummaryAsync(round.Id, ResultSheetStatus.Approved, ct);
            var seats = summary == null || summary.SheetsCounted == 0 ? null
                : summary.Contestants.FirstOrDefault(c => c.ContestantId == contestantId)?.Seats;
            var holders = seats is int n && n > 0
                ? (await lists.GetElectedAsync(new[] { (contestantId, list.Contestant.Name, n) }, ct)).Single().Members
                : new List<ElectedMember>();
            return new PartySeatsPage
            {
                List = list,
                Seats = seats,
                Holders = holders,
                CanRecord = list.Election.Status is ElectionStatus.Provisional or ElectionStatus.Certified,
                Form = form ?? new StepDownForm { ContestantId = contestantId, Date = DateTime.UtcNow.Date },
            };
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
