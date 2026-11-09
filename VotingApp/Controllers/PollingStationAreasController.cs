using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using VotingApp.Application.Common;
using VotingApp.Application.Geography;
using VotingApp.Application.PollingStations;
using VotingApp.Application.Security;
using VotingApp.Domain.Entities;
using VotingApp.Models;

namespace VotingApp.Controllers
{
    /// <summary>Polling stations placed in villages (the unit results are recorded against).</summary>
    public class PollingStationAreasController : BaseController
    {
        private readonly PollingStationService stations;
        private readonly AreaService areas;

        public PollingStationAreasController(PollingStationService stations, AreaService areas)
        {
            this.stations = stations;
            this.areas = areas;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index(string municipalityId)
        {
            var ct = HttpContext.RequestAborted;
            var municipalities = await areas.GetMunicipalitiesAsync(ct);
            var locations = await stations.ListLocationsAsync(ct);
            if (!string.IsNullOrEmpty(municipalityId))
            {
                locations = locations.Where(l => l.AreaId != null && l.AreaId.StartsWith(municipalityId, StringComparison.Ordinal)).ToList();
            }
            ViewData["PollingStations"] = locations.OrderBy(l => l.AreaId).ThenBy(l => l.PollingStation?.Name).ToList();
            ViewData["MunicipalityId"] = municipalityId;
            ViewData["Municipalities"] = new SelectList(municipalities, "Id", "Name");
            return View(municipalities);
        }

        [AllowAnonymous]
        public async Task<IActionResult> GetVotingAreasOfMunicipality(string id) =>
            Json(await stations.ListLocationsInMunicipalityAsync(id, HttpContext.RequestAborted));

        [AllowAnonymous]
        public async Task<IActionResult> GetPostAdministratives(string id) =>
            Json(await areas.GetChildrenAsync(AreaHierarchy.AdministrativePost, id, HttpContext.RequestAborted));

        [AllowAnonymous]
        public async Task<IActionResult> GetVillages(string id) =>
            Json(await areas.GetChildrenAsync(AreaHierarchy.Village, id, HttpContext.RequestAborted));

        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();
            var location = await stations.GetLocationAsync(id.Value, HttpContext.RequestAborted);
            return location == null ? NotFound() : View(location);
        }

        [Authorize(Policy = Permissions.ManagePollingStations)]
        public async Task<IActionResult> Create()
        {
            await LoadMunicipalitiesAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManagePollingStations)]
        public async Task<IActionResult> Create([Bind("AreaId,StationName")] CreatePollingStationArea model)
        {
            if (ModelState.IsValid)
            {
                var result = await stations.CreateLocationAsync(model.AreaId, model.StationName, HttpContext.RequestAborted);
                if (result.Succeeded) return RedirectToAction(nameof(Index));
                AddErrors(result);
            }
            await LoadMunicipalitiesAsync();
            return View(model);
        }

        [Authorize(Policy = Permissions.ManagePollingStations)]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();
            var location = await stations.GetLocationWithParentsAsync(id.Value, HttpContext.RequestAborted);
            if (location == null) return NotFound();

            await LoadMunicipalitiesAsync(location.Municipality?.Id);
            return View(ToEditModel(location.Station));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManagePollingStations)]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,AreaId,StationName")] EditPollingStationArea model)
        {
            if (id != model.Id) return NotFound();
            if (ModelState.IsValid)
            {
                var result = await stations.UpdateLocationAsync(id, model.AreaId, model.StationName, HttpContext.RequestAborted);
                if (result.Succeeded) return RedirectToAction(nameof(Index));
                if (result.Failure == FailureKind.NotFound) return NotFound();
                AddErrors(result);
            }

            var location = await stations.GetLocationWithParentsAsync(id, HttpContext.RequestAborted);
            if (location == null) return NotFound();
            await LoadMunicipalitiesAsync(location.Municipality?.Id);
            var edit = ToEditModel(location.Station);
            edit.StationName = model.StationName;
            return View(edit);
        }

        [Authorize(Policy = Permissions.ManagePollingStations)]
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();
            var location = await stations.GetLocationAsync(id.Value, HttpContext.RequestAborted);
            return location == null ? NotFound() : View(location);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManagePollingStations)]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var result = await stations.DeleteLocationAsync(id, HttpContext.RequestAborted);
            if (result.Succeeded) return RedirectToAction(nameof(Index));
            if (result.Failure == FailureKind.NotFound) return NotFound();
            Flash(result.Error, error: true);
            return RedirectToAction(nameof(Delete), new { id });
        }

        private async Task LoadMunicipalitiesAsync(string selected = null) =>
            ViewData["Municipalities"] = new SelectList(await areas.GetMunicipalitiesAsync(HttpContext.RequestAborted), "Id", "Name", selected);

        private static EditPollingStationArea ToEditModel(PollingStationArea station) => new()
        {
            Id = station.Id,
            PollingStationId = station.PollingStationId,
            PollingStation = station.PollingStation,
            AreaId = station.AreaId,
            Area = station.Area,
            StationName = station.PollingStation?.Name,
        };
    }
}
