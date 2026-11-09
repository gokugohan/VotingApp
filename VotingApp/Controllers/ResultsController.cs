using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VotingApp.Application.Elections;
using VotingApp.Domain.Elections;

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

        public ResultsController(ElectionResultsService results) => this.results = results;

        public async Task<IActionResult> Round(Guid id)
        {
            var summary = await results.GetSummaryAsync(id, ResultSheetStatus.Approved, HttpContext.RequestAborted);
            return summary == null ? NotFound() : View(summary);
        }

        /// <summary>Open data: the same summary as JSON (cached for a few seconds).</summary>
        [ResponseCache(Duration = 10, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> Summary(Guid id)
        {
            var summary = await results.GetSummaryAsync(id, ResultSheetStatus.Approved, HttpContext.RequestAborted);
            return summary == null ? NotFound() : Json(summary);
        }
    }
}
