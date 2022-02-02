using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VotingApp.Data;
using VotingApp.Data.Interface;
using VotingApp.Models;
using VotingApp.SignalRHub;

namespace VotingApp.Controllers
{
    public class VotingsController : BaseController
    {
        public VotingsController(IRepositoryWrapper repository, IHubContext<VotingHub> hubContext,
            IMapper mapper,
            ILogger<BaseController> logger) : base(repository, hubContext, mapper, logger)
        {
        }



        // GET: VotingCandidates
        public async Task<IActionResult> Index()
        {

            //return View(await this.repository.Votes.GetAllWithEntitiesAsync());

            return View(await this.repository.Candidates.GetAllAsync());
        }


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
        public async Task<IActionResult> SaveVote(SaveCandidateVote model)
        {
            try
            {
                if (!this.repository.Votes.IsVoteAlreadyExist(model.CandidateId, model.PollingStationAreaId))
                {

                    var vote = this.mapper.Map<Vote>(model);
                    vote.Id = Guid.NewGuid();
                    this.repository.Votes.Add(vote);
                    await this.repository.SaveAsync();
                    return Json(new { success = true, message = "Vote added!" });
                }
                else
                {
                    var vote = await this.repository.Votes.GetAsync(m => m.CandidateId.Equals(model.CandidateId) & m.PollingStationAreaId.Equals(model.PollingStationAreaId));
                    vote.Total = model.Total;
                    this.repository.Votes.Update(vote);
                    await this.repository.SaveAsync();
                    return Json(new { success = true, message = "Vote Updated!" });
                }

            }
            catch (Exception ex)
            {
                this.logger.LogInformation(ex.Message);
            }

            return Json(new { success = false });
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

        public async Task<IActionResult> ListVotingInMunicipality(Guid? id)
        {
            return Json((await this.repository.Votes.ListVotingInMunicipalityByCandidateId(id)).ToList());
        }


        public IActionResult GetTotalVoteOfCandidates()
        {
            return Json(this.repository.Votes.TotalVoteOfCandidate());
        }


        public async Task<IActionResult> GetCandidateVotesWithinMunicipality(string id)
        {
            //throw new NotImplementedException();
            return Json((await this.repository.Votes.GetCandidateVotesWithinMunicipality(id)));
        }


        public async Task<IActionResult> GetCandidateMunicipalitVotes()
        {
            return Json(await this.repository.Votes.GetCandidateMunicipalitVotes());
        }


    }
}
