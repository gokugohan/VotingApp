using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VotingApp.Data;
using VotingApp.Data.Interface;
using VotingApp.Models;
using VotingApp.SignalRHub;
using static VotingApp.Helpers.Helper;

namespace VotingApp.Controllers
{
    public class VotingsController : BaseController
    {
        public VotingsController(IRepositoryWrapper repository, IHubContext<VotingHub> hubContext,
            IMapper mapper,
            ILogger<BaseController> logger) : base(repository, hubContext, mapper, logger)
        {

        }



        [AllowAnonymous]
        // GET: VotingCandidates
        public async Task<IActionResult> Index()
        {

            //return View(await this.repository.Votes.GetAllWithEntitiesAsync());

            var pollingStationAreas = await this.repository.PollingStationAreas.GetAllWithAreaPollingStationAsync();

            var psas = pollingStationAreas.Select(m => new VotingCandidateAreaDropdownModel
            {
                Id = m.Id,
                VotingAreaName = $"{m.Area.Name} - {m.PollingStation.Name}"
            }).OrderBy(m => m.VotingAreaName).ToList();

            ViewData["PollingStationAreaId"] = new SelectList(psas, "Id", "VotingAreaName");

            var SelectedPollingStationArea = psas.FirstOrDefault();
            if (SelectedPollingStationArea != null)
            {
                ViewData["SelectedPollingStationArea"] = SelectedPollingStationArea.Id;
                ViewData["SelectedPollingStationAreaName"] = SelectedPollingStationArea.VotingAreaName;
             
            }
            else
            {
                ViewData["SelectedPollingStationArea"] = string.Empty;
            }
            
            var candidates = await this.repository.Candidates.GetAllAsync();
            return View(candidates);
        }


        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_VOTING)]
        public async Task<IActionResult> Candidate(Guid? id)
        {
            if (id.HasValue)
            {
                var pollingStationAreas = await this.repository.PollingStationAreas.GetAllWithAreaPollingStationAsync();

                var psas = pollingStationAreas.Select(m => new VotingCandidateAreaDropdownModel
                {
                    Id = m.Id,
                    VotingAreaName = $"{m.Area.Name} - {m.PollingStation.Name}"
                }).OrderBy(m => m.VotingAreaName).ToList();

                ViewData["PollingStationAreaId"] = new SelectList(psas, "Id", "VotingAreaName");
                ViewData["Id"] = id;
                return View(await this.repository.Candidates.GetAllAsync());
            }

            return NotFound();

        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_VOTING)]
        public async Task<IActionResult> SaveVote(SaveCandidateVote model)
        {
            if (model == null || model.CandidateId == Guid.Empty || model.PollingStationAreaId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Candidate and polling station are required." });
            }
            if (model.Total < 0)
            {
                return BadRequest(new { success = false, message = "Total votes cannot be negative." });
            }

            string message;
            var vote = await this.repository.Votes.GetAsync(m => m.CandidateId == model.CandidateId && m.PollingStationAreaId == model.PollingStationAreaId);
            if (vote == null)
            {
                vote = this.mapper.Map<Vote>(model);
                vote.Id = Guid.NewGuid();
                this.repository.Votes.Add(vote);
                message = "Vote added!";
            }
            else
            {
                vote.Total = model.Total;
                message = "Vote updated!";
            }

            // SaveAsync logs failures (incl. the unique-index violation when two clerks insert the same row at once).
            if (!await this.repository.SaveAsync())
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { success = false, message = "The vote could not be saved. Please try again." });
            }

            await this.hubContext.Clients.All.SendAsync(VotingHub.VotingDataEvent, message);
            return Json(new { success = true, message });
        }

        //public async Task<IActionResult> ListVotingsOfCandidates()
        //{
        //    var votesWithEntities = await this.repository.Votes.GetAllWithEntitiesAsync();

        //    var list = new List<string[]>();

        //    foreach (var vote in votesWithEntities)
        //    {
        //        list.Add(new string[]
        //        {
        //            vote.Candidate.Name, vote.Total.ToString()
        //        });
        //    }
        //    return Json(list);
        //}


        // GET: VotingCandidates/Details/5
        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_VOTING)]
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var voteCandidate = await repository.Votes.GetWithAllEntitiesAsync(id);
            if (voteCandidate == null)
            {
                return NotFound();
            }

            return View(voteCandidate);
        }
        [AllowAnonymous]
        public async Task<IActionResult> ListVotingInMunicipality(Guid? id)
        {
            return Json((await this.repository.Votes.ListVotingInMunicipalityByCandidateId(id)).ToList());
        }


        [AllowAnonymous]
        public IActionResult GetTotalVoteOfCandidates()
        {
            return Json(this.repository.Votes.TotalVoteOfCandidate());
        }

        [AllowAnonymous]
        public async Task<IActionResult> GetCandidateVotesWithinMunicipality(string id)
        {
            //throw new NotImplementedException();
            return Json((await this.repository.Votes.GetCandidateVotesWithinMunicipality(id)));
        }

        [AllowAnonymous]
        public async Task<IActionResult> GetCandidateMunicipalitVotes()
        {
            return Json(await this.repository.Votes.GetCandidateMunicipalitVotes());
        }
        [AllowAnonymous]
        public async Task<IActionResult> getCandidatesByMunicipality(string id)
        {
            return Json(await this.repository.Votes.GetCandidateByMunicipality(id));
        }

        [AllowAnonymous]

        public async Task<IActionResult> GetTotalVotingByPollingStationId(Guid id)
        {
            var data = await this.repository.Votes.GetAllWithEntitiesAsync(m => m.PollingStationAreaId.Equals(id));
            return Json(data);
        }


    }
}
