using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VotingApp.Application.Candidates;
using VotingApp.Application.Common;
using VotingApp.Application.Elections;
using VotingApp.Application.Geography;
using VotingApp.Application.Security;
using VotingApp.Domain.Elections;
using VotingApp.Infrastructure.Identity;
using VotingApp.Models;

namespace VotingApp.Controllers
{
    /// <summary>Election set-up (managers) and user municipality scopes (admins).</summary>
    public class ElectionsController : BaseController
    {
        private readonly ElectionAdminService elections;
        private readonly CandidateService candidates;
        private readonly UserScopeService scopes;
        private readonly AreaService areas;
        private readonly UserManager<AppUser> users;
        private readonly PartyListService lists;

        public ElectionsController(ElectionAdminService elections, CandidateService candidates, UserScopeService scopes,
            AreaService areas, UserManager<AppUser> users, PartyListService lists)
        {
            this.lists = lists;
            this.elections = elections;
            this.candidates = candidates;
            this.scopes = scopes;
            this.areas = areas;
            this.users = users;
        }

        /// <summary>Public list of elections with links to their results.</summary>
        [AllowAnonymous]
        public async Task<IActionResult> Index() => View(await elections.ListAsync(HttpContext.RequestAborted));

        [Authorize(Policy = Permissions.ManageElections)]
        public IActionResult Create() => View(new CreateElectionModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageElections)]
        public async Task<IActionResult> Create(CreateElectionModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var result = await elections.CreateAsync(
                new CreateElectionInput(model.Name, model.Type, model.ElectionDate, model.TotalSeats, model.ThresholdPercent),
                HttpContext.RequestAborted);
            if (!result.Succeeded)
            {
                AddErrors(result);
                return View(model);
            }
            Flash("Election created. Add the contestants, then open it.");
            return RedirectToAction(nameof(Details), new { id = result.Value });
        }

        [Authorize(Policy = Permissions.ManageElections)]
        public async Task<IActionResult> Details(Guid id)
        {
            var election = await elections.GetAsync(id, HttpContext.RequestAborted);
            if (election == null) return NotFound();
            ViewData["Candidates"] = new SelectList(await candidates.ListAsync(HttpContext.RequestAborted), "Id", "Name");
            ViewData["ListStatuses"] = (await lists.GetStatusesAsync(id, HttpContext.RequestAborted)).ToDictionary(s => s.ContestantId);
            return View(election);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageElections)]
        public async Task<IActionResult> AddContestant(AddContestantModel model)
        {
            if (!ModelState.IsValid)
            {
                Flash(string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)), error: true);
                return RedirectToAction(nameof(Details), new { id = model.ElectionId });
            }
            var result = await elections.AddContestantAsync(model.ElectionId,
                new AddContestantInput(model.Type, model.Name, model.Acronym, model.CandidateId), HttpContext.RequestAborted);
            Flash(result.Succeeded ? $"Added {model.Name}." : result.Error, error: !result.Succeeded);
            return RedirectToAction(nameof(Details), new { id = model.ElectionId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageElections)]
        public async Task<IActionResult> RemoveContestant(Guid electionId, Guid contestantId)
        {
            var result = await elections.RemoveContestantAsync(contestantId, HttpContext.RequestAborted);
            Flash(result.Succeeded ? "Contestant removed." : result.Error, error: !result.Succeeded);
            return RedirectToAction(nameof(Details), new { id = electionId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageElections)]
        public async Task<IActionResult> SetStatus(Guid id, ElectionStatus status)
        {
            var result = await elections.SetStatusAsync(id, status, HttpContext.RequestAborted);
            Flash(result.Succeeded ? $"Status set to {status}." : result.Error, error: !result.Succeeded);
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageElections)]
        public async Task<IActionResult> CreateRunoff(Guid id, DateTime votingDate)
        {
            var result = await elections.CreateRunoffAsync(id, votingDate, HttpContext.RequestAborted);
            Flash(result.Succeeded ? "Runoff round created with the two qualified candidates." : result.Error, error: !result.Succeeded);
            return RedirectToAction(nameof(Details), new { id });
        }

        // ---- User municipality scopes ----

        [Authorize(Roles = Permissions.AdminRole)]
        public async Task<IActionResult> Scopes()
        {
            var assignments = await scopes.GetAllAssignmentsAsync(HttpContext.RequestAborted);
            var rows = (await users.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync(HttpContext.RequestAborted))
                .Select(u => new UserScopeRow
                {
                    User = u,
                    MunicipalityIds = assignments.TryGetValue(u.Id, out var ids) ? ids : new List<string>(),
                })
                .ToList();
            ViewData["Municipalities"] = await areas.GetMunicipalitiesAsync(HttpContext.RequestAborted);
            return View(rows);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Permissions.AdminRole)]
        public async Task<IActionResult> Scopes(string userId, List<string> municipalityIds)
        {
            if (await users.FindByIdAsync(userId) == null) return NotFound();
            await scopes.SetAssignmentsAsync(userId, municipalityIds, HttpContext.RequestAborted);
            Flash("Assignments saved.");
            return RedirectToAction(nameof(Scopes));
        }
    }
}
