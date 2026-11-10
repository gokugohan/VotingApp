using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VotingApp.Application.Elections;
using VotingApp.Application.Geography;
using VotingApp.Models;

namespace VotingApp.Controllers
{
    public class HomeController : BaseController
    {
        private readonly AreaService areas;
        private readonly PublicResultsService publicResults;
        private readonly ElectionResultsService results;
        private readonly PartyListService lists;

        public HomeController(AreaService areas, PublicResultsService publicResults, ElectionResultsService results, PartyListService lists)
        {
            this.lists = lists;
            this.areas = areas;
            this.publicResults = publicResults;
            this.results = results;
        }

        /// <summary>Results of a round (default: the most recent round in counting or later).</summary>
        [AllowAnonymous]
        public async Task<IActionResult> Index(Guid? roundId, Guid? map)
        {
            var rounds = await publicResults.ListPublishedRoundsAsync();
            var selected = rounds.FirstOrDefault(r => r.RoundId == roundId) ?? rounds.FirstOrDefault();
            if (roundId.HasValue && selected?.RoundId != roundId) return NotFound();
            if (selected == null) return View((RoundResultsPage)null);

            var page = await ResultsController.BuildPageAsync(results, publicResults, lists, selected.RoundId, map,
                Url.Action(nameof(Index)));
            page.Rounds = rounds;
            return View(page);
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
