using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VotingApp.Data;
using VotingApp.Data.Interface;
using VotingApp.Helpers;
using VotingApp.Models;
using VotingApp.SignalRHub;

using static VotingApp.Helpers.Helper;

namespace VotingApp.Controllers
{
    public class PollingStationAreasController : BaseController
    {
        public PollingStationAreasController(IRepositoryWrapper repository, IHubContext<VotingHub> hubContext,
            IMapper mapper, 
            ILogger<BaseController> logger) : base(repository, hubContext,mapper, logger)
        {
        }


        // GET: VotingAreas
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var votingAreas = repository.PollingStationAreas.GetAllWithAreaPollingStation();

            //var listOfArea = new List<IndexVotinAreaModel>();
            //foreach(var item in applicationDbContext)
            //{
            //    var village = await repository.Areas.FindAsync(item.AreaId);
            //    var post_administrative = await repository.Areas.FindAsync(village.ParentId);
            //    var municipality = await repository.Areas.FindAsync(post_administrative.ParentId);

            //    listOfArea.Add(new IndexVotinAreaModel
            //    {
            //        Id = item.Id,
            //        Description = item.Description,
            //        Municipality = municipality,
            //        PosAdministrative = post_administrative,
            //        Village = village
            //    });
            //}

            var municipalities = await this.repository.Areas.GetAllAsync(m => m.Level == Helper.MUNICIPALITY);
            ViewData["PollingStations"] = await this.repository.PollingStationAreas.GetAllWithAreaPollingStationAsync();
            ViewData["Municipalities"] = new SelectList(repository.Areas.GetAll(m => m.Level == 2), "Id", "Name");
            return View(municipalities);
        }

        [AllowAnonymous]
        public IActionResult GetVotingAreasOfMunicipality(string id)
        {

            return Json(this.repository.PollingStationAreas.GetPollingStationWithinMunicipality(id));

        }

        // GET: VotingAreas/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pollingStationArea = await repository.PollingStationAreas.GetWithAreaAsync(id);
            if (pollingStationArea == null)
            {
                return NotFound();
            }

            return View(pollingStationArea);
        }

        [AllowAnonymous]

        public async Task<IActionResult> GetPostAdministratives(string id)
        {
            var post_administratives = await this.repository.Areas.GetAllAsync(m => m.Level == Helper.ADMINISTRATIVE_POST && m.ParentId.Equals(id));
            return Json(post_administratives);
        }

        [AllowAnonymous]
        public async Task<IActionResult> GetVillages(string id)
        {
            var villages = await this.repository.Areas.GetAllAsync(m => m.Level == Helper.VILLAGE && m.ParentId.Equals(id));
            return Json(villages);
        }

        // GET: VotingAreas/Create
        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_POLLING_STATION)]
        public IActionResult Create()
        {
            ViewData["Municipalities"] = new SelectList(repository.Areas.GetAll(m=>m.Level==2), "Id", "Name");
            return View();
        }


        // POST: VotingAreas/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_POLLING_STATION)]
        public async Task<IActionResult> Create([Bind("Id,AreaId,StationName")] CreatePollingStationArea model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var pollingStation = new PollingStation
                    {
                        Id = Guid.NewGuid(),
                        Name = model.StationName
                    };
                    var pollingStationArea = new PollingStationArea
                    {
                        Id = Guid.NewGuid(),
                        PollingStation = pollingStation,
                        AreaId = model.AreaId
                    };

                    this.repository.PollingStationAreas.Add(pollingStationArea);
                    await repository.SaveAsync();
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
               
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: VotingAreas/Edit/5
        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_POLLING_STATION)]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pollingStationArea = await repository.PollingStationAreas.GetWithAreaAsync(id);
            var postAdmin = await repository.Areas.GetAsync(pollingStationArea.Area.ParentId);
            var municipality = await repository.Areas.GetAsync(postAdmin.ParentId);
            if (pollingStationArea == null)
            {
                return NotFound();
            }
            ViewData["postAdmin"] = postAdmin;
            ViewData["municipality"] = municipality;
            ViewData["Municipalities"] = new SelectList(repository.Areas.GetAll(m => m.Level == Helper.MUNICIPALITY), "Id", "Name");

            return View(pollingStationArea);
        }

        // POST: VotingAreas/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_POLLING_STATION)]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,AreaId,StationName")] PollingStationArea votingArea)
        {
            if (id != votingArea.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    repository.PollingStationAreas.Update(votingArea);
                    await repository.SaveAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!VotingAreaExists(votingArea.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["Municipalities"] = new SelectList(repository.Areas.GetAll(m => m.Level == Helper.MUNICIPALITY), "Id", "Name");
            return View(votingArea);
        }

        // GET: VotingAreas/Delete/5
        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_POLLING_STATION)]
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pollingStationArea = await repository.PollingStationAreas.GetAsync(id);
            if (pollingStationArea == null)
            {
                return NotFound();
            }

            return View(pollingStationArea);
        }

        // POST: VotingAreas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_POLLING_STATION)]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var entity = await this.repository.PollingStationAreas.GetAsync(id);
            if (entity == null)
            {
                return NotFound();
            }

            this.repository.PollingStationAreas.Delete(entity);
            if (await this.repository.SaveAsync())
            {
                return RedirectToAction(nameof(Index));
            }

            // Most likely still referenced (e.g. has votes); SaveAsync logged the details.
            TempData["Error"] = "Could not delete: the record is still in use.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        private bool VotingAreaExists(Guid id)
        {
            return repository.PollingStationAreas.Any(e => e.Id == id);
        }
    }
}
