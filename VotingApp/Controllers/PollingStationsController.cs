using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VotingApp.Application.Common;
using VotingApp.Application.PollingStations;
using VotingApp.Application.Security;
using VotingApp.Domain.Entities;

namespace VotingApp.Controllers
{
    public class PollingStationsController : BaseController
    {
        private readonly PollingStationService stations;

        public PollingStationsController(PollingStationService stations) => this.stations = stations;

        public async Task<IActionResult> Index() => View(await stations.ListStationsAsync(HttpContext.RequestAborted));

        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();
            var station = await stations.GetStationAsync(id.Value, HttpContext.RequestAborted);
            return station == null ? NotFound() : View(station);
        }

        [Authorize(Policy = Permissions.ManagePollingStations)]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManagePollingStations)]
        public async Task<IActionResult> Create([Bind("Name")] PollingStation pollingStation)
        {
            if (!ModelState.IsValid) return View(pollingStation);
            var result = await stations.CreateStationAsync(pollingStation.Name, HttpContext.RequestAborted);
            if (result.Succeeded) return RedirectToAction(nameof(Index));
            AddErrors(result);
            return View(pollingStation);
        }

        [Authorize(Policy = Permissions.ManagePollingStations)]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();
            var station = await stations.GetStationAsync(id.Value, HttpContext.RequestAborted);
            return station == null ? NotFound() : View(station);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManagePollingStations)]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,Name")] PollingStation pollingStation)
        {
            if (id != pollingStation.Id) return NotFound();
            if (!ModelState.IsValid) return View(pollingStation);
            var result = await stations.RenameStationAsync(id, pollingStation.Name, HttpContext.RequestAborted);
            if (result.Succeeded) return RedirectToAction(nameof(Index));
            if (result.Failure == FailureKind.NotFound) return NotFound();
            AddErrors(result);
            return View(pollingStation);
        }

        [Authorize(Policy = Permissions.ManagePollingStations)]
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();
            var station = await stations.GetStationAsync(id.Value, HttpContext.RequestAborted);
            return station == null ? NotFound() : View(station);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManagePollingStations)]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var result = await stations.DeleteStationAsync(id, HttpContext.RequestAborted);
            if (result.Succeeded) return RedirectToAction(nameof(Index));
            if (result.Failure == FailureKind.NotFound) return NotFound();
            Flash(result.Error, error: true);
            return RedirectToAction(nameof(Delete), new { id });
        }
    }
}
