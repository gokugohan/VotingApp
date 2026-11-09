using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VotingApp.Application.Candidates;
using VotingApp.Application.Security;
using VotingApp.Infrastructure;
using VotingApp.Models;
using VotingApp.ViewModels;

namespace VotingApp.Controllers
{
    public class CandidatesController : BaseController
    {
        private readonly CandidateService candidates;
        private readonly ImageStorage images;

        public CandidatesController(CandidateService candidates, ImageStorage images)
        {
            this.candidates = candidates;
            this.images = images;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index() => View(await candidates.ListAsync(HttpContext.RequestAborted));

        [AllowAnonymous]
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();
            var candidate = await candidates.GetAsync(id.Value, HttpContext.RequestAborted);
            return candidate == null ? NotFound() : View(candidate);
        }

        [AllowAnonymous]
        public async Task<IActionResult> GetCandidadesAjax() => Json(await candidates.ListAsync(HttpContext.RequestAborted));

        [Authorize(Policy = Permissions.ManageCandidates)]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageCandidates)]
        public async Task<IActionResult> Create(CreateCandidateModel model)
        {
            var imageError = ImageStorage.Validate(model.Image);
            if (imageError != null) ModelState.AddModelError(nameof(model.Image), imageError);
            if (!ModelState.IsValid) return View(model);

            var avatar = await images.SaveCandidateImageAsync(model.Image, HttpContext.RequestAborted);
            var result = await candidates.CreateAsync(new CandidateInput(model.Name, model.Description, model.ShortBiography, avatar), HttpContext.RequestAborted);
            if (!result.Succeeded)
            {
                AddErrors(result);
                return View(model);
            }
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = Permissions.ManageCandidates)]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();
            var candidate = await candidates.GetAsync(id.Value, HttpContext.RequestAborted);
            if (candidate == null) return NotFound();
            return View(new EditCandidateModel
            {
                Id = candidate.Id,
                Name = candidate.Name,
                Description = candidate.Description,
                ShortBiography = candidate.ShortBiography,
                Avatar = candidate.Avatar,
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageCandidates)]
        public async Task<IActionResult> Edit(Guid id, EditCandidateModel model)
        {
            if (id != model.Id) return NotFound();
            var imageError = ImageStorage.Validate(model.Image);
            if (imageError != null) ModelState.AddModelError(nameof(model.Image), imageError);
            if (!ModelState.IsValid) return View(model);

            var avatar = await images.SaveCandidateImageAsync(model.Image, HttpContext.RequestAborted);
            var result = await candidates.UpdateAsync(id, new CandidateInput(model.Name, model.Description, model.ShortBiography, avatar), HttpContext.RequestAborted);
            if (!result.Succeeded)
            {
                if (result.Failure == Application.Common.FailureKind.NotFound) return NotFound();
                AddErrors(result);
                return View(model);
            }
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = Permissions.ManageCandidates)]
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();
            var candidate = await candidates.GetAsync(id.Value, HttpContext.RequestAborted);
            if (candidate == null) return NotFound();

            var summary = await candidates.GetVoteSummaryAsync(id.Value, HttpContext.RequestAborted);
            var model = new DeleteCandidateViewModel { Id = candidate.Id, Candidate = candidate };
            if (summary.Votes.Count > 0)
            {
                ViewData["TotalCandidateVote"] = summary.TotalVotes;
                model.VoteList = summary.Votes.ToList();
            }
            return View(model);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageCandidates)]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var result = await candidates.DeleteAsync(id, HttpContext.RequestAborted);
            if (result.Succeeded) return RedirectToAction(nameof(Index));
            if (result.Failure == Application.Common.FailureKind.NotFound) return NotFound();
            Flash(result.Error, error: true);
            return RedirectToAction(nameof(Delete), new { id });
        }
    }
}
