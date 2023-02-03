using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using VotingApp.Data;
using VotingApp.Data.Interface;
using VotingApp.Helpers;
using VotingApp.Models;
using VotingApp.SignalRHub;

namespace VotingApp.Controllers
{
    public class HomeController : BaseController
    {
        private AppDbContext Context;
        public HomeController(IRepositoryWrapper repository, IHubContext<VotingHub> hubContext,
            IMapper mapper,
            AppDbContext context,
            ILogger<BaseController> logger) : base(repository, hubContext,mapper, logger)
        {
            this.Context = context;
        }

        
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var municipalities = await this.repository.Areas.GetAllAsync(m=>m.Level==Helper.MUNICIPALITY,m=>m.OrderBy(m=>m.Name));
            var candidates = await this.repository.Candidates.GetAllAsync(m=>m.OrderBy(m=>m.Name));

            //ViewData["candidates"] = candidates;
            var totalVotesOfCandidate = this.repository.Votes.TotalVoteOfCandidate();

            ViewData["TotalVote"] = this.repository.Votes.GetTotalVote();
            ViewData["candidates"] = candidates;

            //var votings = this.Context.Votes
            //    .Include(m => m.Candidate)
            //    .Include(m => m.PollingStationArea).ThenInclude(m => m.Area)
            //    .Include(m => m.PollingStationArea).ThenInclude(m => m.PollingStation)
            //    .ToList();
            //var mstring = municipalities.Select(m => m.Id).ToList();
            //var cVotings = votings.Select(m => m.Candidate.Name).Distinct().ToList();


            return View(municipalities);
        }

        [AllowAnonymous]
        public async Task<IActionResult> GetMunicipalities()
        {
            var municipalities = await this.repository.Areas.GetAllAsync(m => m.Level == Helper.MUNICIPALITY);
            return Json(municipalities);
        }
        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }
        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }



    }
}
