using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VotingApp.Application.Common;
using VotingApp.Application.Elections;
using VotingApp.Application.Security;
using VotingApp.Domain.Elections;

namespace VotingApp.Controllers
{
    public class PartyFormModel
    {
        public Guid? Id { get; set; }
        [Required, StringLength(200)] public string Name { get; set; }
        [StringLength(30)] public string Acronym { get; set; }
        public PartyKind Kind { get; set; } = PartyKind.Party;
        [StringLength(2000)] public string Notes { get; set; }
    }

    /// <summary>Lasting party and coalition records: public history, managed by election managers.</summary>
    public class PartiesController : BaseController
    {
        private readonly PartyService parties;

        public PartiesController(PartyService parties) => this.parties = parties;

        [AllowAnonymous]
        public async Task<IActionResult> Index() => View(await parties.ListAsync(HttpContext.RequestAborted));

        [AllowAnonymous]
        public async Task<IActionResult> Details(Guid id)
        {
            var party = await parties.GetAsync(id, HttpContext.RequestAborted);
            if (party == null) return NotFound();
            ViewData["History"] = await parties.GetHistoryAsync(id, HttpContext.RequestAborted);
            return View(party);
        }

        [Authorize(Policy = Permissions.ManageElections)]
        public IActionResult Create() => View("Form", new PartyFormModel());

        [Authorize(Policy = Permissions.ManageElections)]
        public async Task<IActionResult> Edit(Guid id)
        {
            var p = await parties.GetAsync(id, HttpContext.RequestAborted);
            if (p == null) return NotFound();
            return View("Form", new PartyFormModel { Id = p.Id, Name = p.Name, Acronym = p.Acronym, Kind = p.Kind, Notes = p.Notes });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageElections)]
        public async Task<IActionResult> Save(PartyFormModel model)
        {
            if (!ModelState.IsValid) return View("Form", model);
            var input = new PartyInput(model.Name, model.Acronym, model.Kind, model.Notes);
            OperationResult result;
            Guid id;
            if (model.Id.HasValue)
            {
                result = await parties.UpdateAsync(model.Id.Value, input, HttpContext.RequestAborted);
                id = model.Id.Value;
            }
            else
            {
                var created = await parties.CreateAsync(input, HttpContext.RequestAborted);
                result = created;
                id = created.Value;
            }
            if (!result.Succeeded)
            {
                if (result.Failure == FailureKind.NotFound) return NotFound();
                AddErrors(result);
                return View("Form", model);
            }
            Flash("Party saved.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageElections)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await parties.DeleteAsync(id, HttpContext.RequestAborted);
            if (result.Failure == FailureKind.NotFound) return NotFound();
            Flash(result.Succeeded ? "Party deleted." : result.Error, error: !result.Succeeded);
            return result.Succeeded ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Details), new { id });
        }
    }
}
