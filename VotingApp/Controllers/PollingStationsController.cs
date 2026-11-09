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
using VotingApp.SignalRHub;

using static VotingApp.Helpers.Helper;

namespace VotingApp.Controllers
{
    public class PollingStationsController : BaseController
    {
        public PollingStationsController(Data.Interface.IRepositoryWrapper repository, IHubContext<VotingHub> hubContext,
            IMapper mapper, 
            ILogger<BaseController> logger) : base(repository, hubContext, mapper,logger)
        {
        }


        // GET: PollingStations
        public async Task<IActionResult> Index()
        {
            return View(await repository.PollingStations.GetAllAsync());
        }

        // GET: PollingStations/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pollingStation = await repository.PollingStations.GetAsync(id);
            if (pollingStation == null)
            {
                return NotFound();
            }

            return View(pollingStation);
        }

        // GET: PollingStations/Create
        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_POLLING_STATION)]
        public IActionResult Create()
        {
            return View();
        }

        // POST: PollingStations/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_POLLING_STATION)]
        public async Task<IActionResult> Create([Bind("Id,Name")] PollingStation pollingStation)
        {
            if (ModelState.IsValid)
            {
                pollingStation.Id = Guid.NewGuid();
                repository.PollingStations.Add(pollingStation);
                await repository.SaveAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(pollingStation);
        }

        // GET: PollingStations/Edit/5
        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_POLLING_STATION)]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pollingStation = await repository.PollingStations.GetAsync(id);
            if (pollingStation == null)
            {
                return NotFound();
            }
            return View(pollingStation);
        }

        // POST: PollingStations/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_POLLING_STATION)]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,Name")] PollingStation pollingStation)
        {
            if (id != pollingStation.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    repository.PollingStations.Update(pollingStation);
                    await repository.SaveAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PollingStationExists(pollingStation.Id))
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
            return View(pollingStation);
        }

        // GET: PollingStations/Delete/5
        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_POLLING_STATION)]
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pollingStation = await repository.PollingStations.GetAsync(id);
            if (pollingStation == null)
            {
                return NotFound();
            }

            return View(pollingStation);
        }

        // POST: PollingStations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_POLLING_STATION)]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var entity = await this.repository.PollingStations.GetAsync(id);
            if (entity == null)
            {
                return NotFound();
            }

            this.repository.PollingStations.Delete(entity);
            if (await this.repository.SaveAsync())
            {
                return RedirectToAction(nameof(Index));
            }

            // Most likely still referenced (e.g. has votes); SaveAsync logged the details.
            TempData["Error"] = "Could not delete: the record is still in use.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        private bool PollingStationExists(Guid id)
        {
            return repository.PollingStations.Any(e => e.Id == id);
        }
    }
}
