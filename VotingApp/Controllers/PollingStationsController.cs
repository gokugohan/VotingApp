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
using VotingApp.SignalRHub;

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
        public IActionResult Create()
        {
            return View();
        }

        // POST: PollingStations/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
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
        public IActionResult DeleteConfirmed(Guid id)
        {
            try
            {
                this.repository.PollingStations.Delete(m => m.Id.Equals(id));
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex.Message);
            }

            return RedirectToAction(nameof(Delete), new { id = id });
        }

        private bool PollingStationExists(Guid id)
        {
            return repository.PollingStations.Any(e => e.Id == id);
        }
    }
}
