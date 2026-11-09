using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VotingApp.Application.Elections;
using VotingApp.Domain.Elections;

namespace VotingApp.Controllers
{
    /// <summary>
    /// Data for the home dashboard (charts and map), computed from approved result sheets of a round.
    /// Response shapes match what the dashboard scripts were written against, so the charts are unchanged.
    /// Everything comes from the cached municipality breakdown: no extra database load per chart.
    /// </summary>
    [AllowAnonymous]
    [ResponseCache(Duration = 10, Location = ResponseCacheLocation.Any)]
    public class DashboardController : BaseController
    {
        private readonly PublicResultsService publicResults;
        private readonly ElectionResultsService results;

        public DashboardController(PublicResultsService publicResults, ElectionResultsService results)
        {
            this.publicResults = publicResults;
            this.results = results;
        }

        /// <summary>Contestants on the round's ballot: [{ id, name, acronym }].</summary>
        public async Task<IActionResult> Contestants(Guid id)
        {
            var ballot = await publicResults.GetBallotAsync(id, HttpContext.RequestAborted);
            return Json(ballot.Select(c => new { id = c.Id, name = c.Name, acronym = c.Acronym }));
        }

        /// <summary>Votes per contestant name: { "Name": votes }.</summary>
        public async Task<IActionResult> Totals(Guid id)
        {
            var summary = await results.GetSummaryAsync(id, ResultSheetStatus.Approved, HttpContext.RequestAborted);
            if (summary == null) return NotFound();
            return Json(summary.Contestants.ToDictionary(c => c.Name, c => c.Votes));
        }

        /// <summary>
        /// Votes per municipality (all contestants, or one): [{ key: municipality name, value: { areaId, areaKey, areaName, totalVote } | null }].
        /// </summary>
        public async Task<IActionResult> ByMunicipality(Guid id, Guid? contestantId)
        {
            var breakdown = await publicResults.GetBreakdownAsync(id, HttpContext.RequestAborted);
            if (breakdown == null) return NotFound();
            return Json(breakdown.Municipalities.Select(m =>
            {
                var total = contestantId.HasValue
                    ? (m.Votes.TryGetValue(contestantId.Value, out var v) ? v : 0)
                    : m.ValidVotes;
                return new
                {
                    key = m.Name,
                    value = total == 0 ? null : new { areaId = m.MunicipalityId, areaKey = m.MapKey, areaName = m.Name, totalVote = total },
                };
            }));
        }

        /// <summary>Chart series: per contestant, votes in each municipality (municipality-name order).</summary>
        public async Task<IActionResult> Series(Guid id)
        {
            var breakdown = await publicResults.GetBreakdownAsync(id, HttpContext.RequestAborted);
            if (breakdown == null) return NotFound();
            return Json(breakdown.Ballot.Select(c => new
            {
                candidate = new { id = c.Id, name = c.Name },
                votes = breakdown.Municipalities.Select(m => m.Votes.TryGetValue(c.Id, out var v) ? v : 0),
            }));
        }
    }
}
