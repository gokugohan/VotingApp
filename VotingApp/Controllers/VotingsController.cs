using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using VotingApp.Application.Candidates;
using VotingApp.Application.PollingStations;
using VotingApp.Application.Results;
using VotingApp.Application.Security;
using VotingApp.Application.Votes;
using VotingApp.Models;
using VotingApp.SignalRHub;

namespace VotingApp.Controllers
{
    /// <summary>Quick vote entry (legacy Votes table) and the public dashboard's JSON endpoints.</summary>
    public class VotingsController : BaseController
    {
        private readonly VoteEntryService votes;
        private readonly ResultsQueryService results;
        private readonly CandidateService candidates;
        private readonly PollingStationService stations;
        private readonly IHubContext<VotingHub> hub;

        public VotingsController(VoteEntryService votes, ResultsQueryService results, CandidateService candidates,
            PollingStationService stations, IHubContext<VotingHub> hub)
        {
            this.votes = votes;
            this.results = results;
            this.candidates = candidates;
            this.stations = stations;
            this.hub = hub;
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
