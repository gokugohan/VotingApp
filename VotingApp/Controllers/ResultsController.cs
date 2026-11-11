using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VotingApp.Application.Elections;
using VotingApp.Application.Geography;
using VotingApp.Domain.Elections;
using VotingApp.Models;

namespace VotingApp.Controllers
{
    /// <summary>
    /// Public provisional results of a round. Only approved and locked sheets are counted, so
    /// figures still under entry or verification are never published.
    /// </summary>
    [AllowAnonymous]
    public class ResultsController : BaseController
    {
        private readonly ElectionResultsService results;
        private readonly PublicResultsService publicResults;
        private readonly AreaService areas;
        private readonly PartyListService lists;
        private readonly ResultsComparisonService comparisons;

        public ResultsController(ElectionResultsService results, PublicResultsService publicResults, AreaService areas, PartyListService lists,
            ResultsComparisonService comparisons)
        {
            this.comparisons = comparisons;
            this.results = results;
            this.publicResults = publicResults;
            this.areas = areas;
            this.lists = lists;
        }

        public async Task<IActionResult> Round(Guid id, Guid? map)
        {
            var page = await BuildPageAsync(results, publicResults, lists, comparisons, id, map, Url.Action(nameof(Round), new { id }));
            return page == null ? NotFound() : View(page);
        }

        /// <summary>Shared by the home page: everything the results partial needs for one round.</summary>
        internal static async Task<RoundResultsPage> BuildPageAsync(ElectionResultsService results, PublicResultsService publicResults,
            PartyListService lists, ResultsComparisonService comparisons, Guid roundId, Guid? mapContestantId, string pagePath)
        {
            var summary = await results.GetSummaryAsync(roundId, ResultSheetStatus.Approved);
            if (summary == null) return null;
            var elected = summary.Parliamentary == null || summary.SheetsCounted == 0 ? null
                : await lists.GetElectedForRoundAsync(roundId, summary.Contestants.Select(c => (c.ContestantId, c.Name, c.Seats ?? 0)).ToList());
            return new RoundResultsPage
            {
                Summary = summary,
                Breakdown = await publicResults.GetBreakdownAsync(roundId),
                Signature = await publicResults.GetSignatureAsync(roundId),
                MapContestantId = summary.Contestants.Any(c => c.ContestantId == mapContestantId) ? mapContestantId : null,
                PagePath = pagePath,
                Elected = elected,
                Comparison = summary.SheetsCounted == 0 ? null : await comparisons.GetAsync(roundId),
            };
        }

        /// <summary>Drill-down: every polling station in one municipality.</summary>
        public async Task<IActionResult> Municipality(Guid roundId, string id)
        {
            var ct = HttpContext.RequestAborted;
            var summary = await results.GetSummaryAsync(roundId, ResultSheetStatus.Approved, ct);
            var municipality = string.IsNullOrEmpty(id) ? null : await areas.GetAsync(id, ct);
            if (summary == null || municipality == null || municipality.Level != Domain.Entities.AreaHierarchy.Municipality) return NotFound();

            var breakdown = await publicResults.GetBreakdownAsync(roundId, ct);
            ViewData["Signature"] = await publicResults.GetSignatureAsync(roundId, ct);
            return View(new MunicipalityResultsPage
            {
                Summary = summary,
                Ballot = breakdown.Ballot,
                Municipality = breakdown.Municipalities.First(m => m.MunicipalityId == id),
                Stations = await publicResults.GetStationsAsync(roundId, id, ct),
            });
        }

        /// <summary>Live-refresh fingerprint polled by results pages (a few bytes).</summary>
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Signature(Guid id) =>
            Content(await publicResults.GetSignatureAsync(id, HttpContext.RequestAborted), "text/plain");

        /// <summary>Open data: the national summary as JSON (cached for a few seconds).</summary>
        [ResponseCache(Duration = 10, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> Summary(Guid id)
        {
            var summary = await results.GetSummaryAsync(id, ResultSheetStatus.Approved, HttpContext.RequestAborted);
            return summary == null ? NotFound() : Json(summary);
        }

        /// <summary>Open data: per-municipality results as JSON.</summary>
        [ResponseCache(Duration = 10, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> Municipalities(Guid id)
        {
            var breakdown = await publicResults.GetBreakdownAsync(id, HttpContext.RequestAborted);
            return breakdown == null ? NotFound() : Json(breakdown);
        }

        /// <summary>Open data: one row per counted polling station sheet.</summary>
        public async Task<IActionResult> Csv(Guid id)
        {
            var summary = await results.GetSummaryAsync(id, ResultSheetStatus.Approved, HttpContext.RequestAborted);
            if (summary == null) return NotFound();
            var bytes = await publicResults.ExportCountedSheetsFileAsync(id, HttpContext.RequestAborted);
            var name = $"results-{Slug(summary.ElectionName)}-round{summary.RoundNumber}-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv";
            return File(bytes, "text/csv; charset=utf-8", name);
        }

        /// <summary>
        /// The printable results record of a round, national or for one municipality, to be signed by the
        /// tabulation body. Provisional until the round is certified.
        /// </summary>
        public async Task<IActionResult> Official(Guid id, string municipality,
            [Microsoft.AspNetCore.Mvc.FromServices] OfficialResultsService official)
        {
            var document = await official.GetAsync(id, string.IsNullOrEmpty(municipality) ? null : municipality, HttpContext.RequestAborted);
            return document == null ? NotFound() : View(document);
        }

        private static string Slug(string value) =>
            new string((value ?? "election").ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
    }
}
