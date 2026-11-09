using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VotingApp.Application.Elections;
using VotingApp.Application.Geography;
using VotingApp.Domain.Elections;
using VotingApp.Models;

namespace VotingApp.Controllers
{
    public class HomeController : BaseController
    {
        // Dot as thousands separator, no decimals (e.g. 1.234.567). For display only.
        private static readonly NumberFormatInfo VoteNumberFormat = new() { NumberDecimalDigits = 0, NumberGroupSeparator = "." };

        private readonly AreaService areas;
        private readonly PublicResultsService publicResults;
        private readonly ElectionResultsService results;

        public HomeController(AreaService areas, PublicResultsService publicResults, ElectionResultsService results)
        {
            this.areas = areas;
            this.publicResults = publicResults;
            this.results = results;
        }

        /// <summary>Dashboard of approved results for a round (default: the most recent round in counting or later).</summary>
        [AllowAnonymous]
        public async Task<IActionResult> Index(Guid? roundId)
        {
            var ct = HttpContext.RequestAborted;
            var rounds = await publicResults.ListPublishedRoundsAsync(ct);
            var selected = rounds.FirstOrDefault(r => r.RoundId == roundId) ?? rounds.FirstOrDefault();
            if (roundId.HasValue && selected?.RoundId != roundId) return NotFound();

            ViewData["Rounds"] = rounds;
            ViewData["Round"] = selected;
            if (selected != null)
            {
                var summary = await results.GetSummaryAsync(selected.RoundId, ResultSheetStatus.Approved, ct);
                ViewData["Summary"] = summary;
                ViewData["TotalVote"] = summary.ValidVotes.ToString("N", VoteNumberFormat);
                ViewData["TotalVoteRaw"] = summary.ValidVotes.ToString(CultureInfo.InvariantCulture);
                ViewData["candidates"] = await publicResults.GetBallotAsync(selected.RoundId, ct);
            }
            else
            {
                ViewData["TotalVote"] = "0";
                ViewData["TotalVoteRaw"] = "0";
                ViewData["candidates"] = Array.Empty<BallotContestant>();
            }
            return View(await areas.GetMunicipalitiesAsync(ct));
        }

        [AllowAnonymous]
        public async Task<IActionResult> GetMunicipalities() =>
            Json(await areas.GetMunicipalitiesAsync(HttpContext.RequestAborted));

        [AllowAnonymous]
        public IActionResult Privacy() => View();

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
