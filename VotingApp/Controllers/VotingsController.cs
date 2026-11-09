using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using VotingApp.Application.Candidates;
using VotingApp.Application.PollingStations;
using VotingApp.Application.Results;
using VotingApp.Application.Security;
using VotingApp.Application.Votes;
using VotingApp.Models;
using VotingApp.SignalRHub;

namespace VotingApp.Controllers
{
    /// <summary>
    /// Legacy quick vote entry (Votes table) and its read-only JSON endpoints. Superseded by the
    /// result-sheet workflow; entry is disabled unless Features:LegacyVoteEntry is true, so results are
    /// not recorded in two places. The dashboard now reads result sheets (DashboardController).
    /// </summary>
    public class VotingsController : BaseController
    {
        public const string FeatureFlag = "Features:LegacyVoteEntry";
        private static readonly string[] EntryActions = { nameof(Index), nameof(Candidate), nameof(SaveVote), nameof(Details) };
        private readonly VoteEntryService votes;
        private readonly ResultsQueryService results;
        private readonly CandidateService candidates;
        private readonly PollingStationService stations;
        private readonly IHubContext<VotingHub> hub;
        private readonly bool entryEnabled;

        public VotingsController(VoteEntryService votes, ResultsQueryService results, CandidateService candidates,
            PollingStationService stations, IHubContext<VotingHub> hub, IConfiguration configuration)
        {
            this.entryEnabled = configuration.GetValue<bool>(FeatureFlag);
            this.votes = votes;
            this.results = results;
            this.candidates = candidates;
            this.stations = stations;
            this.hub = hub;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var action = (string)context.RouteData.Values["action"];
            if (!entryEnabled && EntryActions.Contains(action, StringComparer.OrdinalIgnoreCase))
            {
                if (HttpMethods.IsPost(context.HttpContext.Request.Method))
                {
                    context.Result = StatusCode(StatusCodes.Status410Gone,
                        new { success = false, message = "Quick vote entry is disabled. Use result sheets." });
                }
                else
                {
                    Flash("Quick vote entry has been replaced by polling station result sheets (enter, verify, approve).");
                    context.Result = RedirectToAction(nameof(ElectionsController.Index), "Elections");
                }
                return;
            }
            base.OnActionExecuting(context);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var options = await stations.ListLocationOptionsAsync(HttpContext.RequestAborted);
            ViewData["PollingStationAreaId"] = new SelectList(options, nameof(PollingStationOption.Id), nameof(PollingStationOption.Name));
            var selected = options.FirstOrDefault();
            ViewData["SelectedPollingStationArea"] = selected?.Id.ToString() ?? string.Empty;
            ViewData["SelectedPollingStationAreaName"] = selected?.Name;
            return View(await candidates.ListAsync(HttpContext.RequestAborted));
        }

        [Authorize(Policy = Permissions.EnterVotes)]
        public async Task<IActionResult> Candidate(Guid? id)
        {
            if (id == null) return NotFound();
            var options = await stations.ListLocationOptionsAsync(HttpContext.RequestAborted);
            ViewData["PollingStationAreaId"] = new SelectList(options, nameof(PollingStationOption.Id), nameof(PollingStationOption.Name));
            ViewData["Id"] = id;
            return View(await candidates.ListAsync(HttpContext.RequestAborted));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.EnterVotes)]
        public async Task<IActionResult> SaveVote(SaveCandidateVote model)
        {
            if (model == null) return BadRequest(new { success = false, message = "Invalid request." });
            var result = await votes.SaveAsync(model.CandidateId, model.PollingStationAreaId, model.Total, HttpContext.RequestAborted);
            if (!result.Succeeded) return JsonFailure(result);

            var message = result.Value == VoteSaveKind.Added ? "Vote added!" : "Vote updated!";
            await hub.Clients.All.SendAsync(VotingHub.VotingDataEvent, message, HttpContext.RequestAborted);
            return Json(new { success = true, message });
        }

        [Authorize(Policy = Permissions.EnterVotes)]
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();
            var vote = await votes.GetWithDetailsAsync(id.Value, HttpContext.RequestAborted);
            return vote == null ? NotFound() : View(vote);
        }

        // ---- Public dashboard data ----

        [AllowAnonymous]
        public async Task<IActionResult> ListVotingInMunicipality(Guid? id) =>
            Json((await results.GetTotalsByMunicipalityAsync(id, HttpContext.RequestAborted)).ToList());

        [AllowAnonymous]
        public async Task<IActionResult> GetTotalVoteOfCandidates() =>
            Json(await results.GetTotalsByCandidateAsync(HttpContext.RequestAborted));

        [AllowAnonymous]
        public async Task<IActionResult> GetCandidateVotesWithinMunicipality(string id) =>
            Json(await results.GetCandidateTotalsInMunicipalityAsync(id, HttpContext.RequestAborted));

        [AllowAnonymous]
        public async Task<IActionResult> GetCandidateMunicipalitVotes() =>
            Json(await results.GetCandidateSeriesByMunicipalityAsync(HttpContext.RequestAborted));

        [AllowAnonymous]
        public async Task<IActionResult> getCandidatesByMunicipality(string id) =>
            Json(await results.GetVotesInMunicipalityAsync(id, HttpContext.RequestAborted));

        [AllowAnonymous]
        public async Task<IActionResult> GetTotalVotingByPollingStationId(Guid id) =>
            Json(await votes.ListForPollingStationAsync(id, HttpContext.RequestAborted));
    }
}
