using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VotingApp.Application.Candidates;
using VotingApp.Application.Geography;
using VotingApp.Application.Results;
using VotingApp.Models;

namespace VotingApp.Controllers
{
    public class HomeController : BaseController
    {
        // Dot as thousands separator, no decimals (e.g. 1.234.567).
        private static readonly NumberFormatInfo VoteNumberFormat = new() { NumberDecimalDigits = 0, NumberGroupSeparator = "." };

        private readonly AreaService areas;
        private readonly CandidateService candidates;
        private readonly ResultsQueryService results;

        public HomeController(AreaService areas, CandidateService candidates, ResultsQueryService results)
        {
            this.areas = areas;
            this.candidates = candidates;
            this.results = results;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            ViewData["TotalVote"] = (await results.GetTotalVotesAsync(HttpContext.RequestAborted)).ToString("N", VoteNumberFormat);
            ViewData["candidates"] = await candidates.ListAsync(HttpContext.RequestAborted);
            return View(await areas.GetMunicipalitiesAsync(HttpContext.RequestAborted));
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
