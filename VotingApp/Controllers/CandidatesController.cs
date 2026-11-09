using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
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
using VotingApp.ViewModels;

using static VotingApp.Helpers.Helper;

namespace VotingApp.Controllers
{
    public class CandidatesController : BaseController
    {
        private readonly IWebHostEnvironment webHostEnvironment;
        public CandidatesController(IRepositoryWrapper repository,
            IHubContext<VotingHub> hubContext, ILogger<BaseController> logger,
            IMapper mapper,
            IWebHostEnvironment webHostEnvironment) : base(repository, hubContext,mapper, logger)
        {
            this.webHostEnvironment = webHostEnvironment;
        }


        // GET: Candidates
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            return View(await repository.Candidates.GetAllAsync());
        }

        // GET: Candidates/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var candidate = await repository.Candidates.GetAsync(id);
            if (candidate == null)
            {
                return NotFound();
            }

            return View(candidate);
        }

        // GET: Candidates/Create
        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_CANDIDATE)]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Candidates/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_CANDIDATE)]
        public async Task<IActionResult> Create(CreateCandidateModel model)
        {
            ValidateImage(model.Image);
            if (ModelState.IsValid)
            {
                var candidate = new Candidate
                {
                    Id=Guid.NewGuid(),
                    Name = model.Name,
                    Description = model.Description,
                    ShortBiography = model.ShortBiography,
                    Avatar = UploadImage(model.Image)
                };
                repository.Candidates.Add(candidate);
                await repository.SaveAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // GET: Candidates/Edit/5
        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_CANDIDATE)]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var model = this.mapper.Map<EditCandidateModel>(await repository.Candidates.GetAsync(id));
            
            return View(model);
        }

        // POST: Candidates/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_CANDIDATE)]
        public async Task<IActionResult> Edit(Guid id, EditCandidateModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            ValidateImage(model.Image);
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            if (ModelState.IsValid)
            {
                try
                {
                    //var candidate = this.mapper.Map<Candidate>(model);
                    var candidate = await this.repository.Candidates.GetAsync(id);

                    if (model.Image != null)
                    {
                        model.Avatar = UploadImage(model.Image);
                        candidate.Avatar = model.Avatar;
                    }

                    candidate.Description = model.Description;
                    candidate.ShortBiography = model.ShortBiography;
                    repository.Candidates.Update(candidate);
                    await repository.SaveAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CandidateExists(model.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
               
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Candidates/Delete/5
        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_CANDIDATE)]
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var candidate = await repository.Candidates
                .GetAsync(m => m.Id == id);

            
            if (candidate == null)
            {
                return NotFound();
            }

            var model = new DeleteCandidateViewModel();
            model.Id = candidate.Id;
            model.Candidate = candidate;

            // Check if candidates has vote
            if(await repository.Candidates.hasAnyVoting(id.Value))
            {
                ViewData["TotalCandidateVote"] = await this.repository.Candidates.GetTotalVoting(id.Value);
                model.VoteList = await this.repository.Candidates.getTotalVotingWithAreas(id.Value);
            }

            return View(model);
        }

        // POST: Candidates/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permission.PERMISSION_ACCESS_ADD_CANDIDATE)]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var entity = await this.repository.Candidates.GetAsync(id);
            if (entity == null)
            {
                return NotFound();
            }

            this.repository.Candidates.Delete(entity);
            if (await this.repository.SaveAsync())
            {
                return RedirectToAction(nameof(Index));
            }

            // Most likely still referenced (e.g. has votes); SaveAsync logged the details.
            TempData["Error"] = "Could not delete: the record is still in use.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        private bool CandidateExists(Guid id)
        {
            return repository.Candidates.Any(e => e.Id == id);
        }

        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private const long MaxImageBytes = 2 * 1024 * 1024;

        /// <summary>Adds a model error if the upload is not a small raster image. SVG is rejected (it can carry script).</summary>
        private void ValidateImage(IFormFile formFile)
        {
            if (formFile == null) return;

            var extension = Path.GetExtension(formFile.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !AllowedImageExtensions.Contains(extension) ||
                formFile.ContentType == null || !formFile.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Image", "Only JPG, PNG, GIF or WEBP images are allowed.");
            }
            else if (formFile.Length == 0 || formFile.Length > MaxImageBytes)
            {
                ModelState.AddModelError("Image", "The image must be smaller than 2 MB.");
            }
        }

        /// <summary>Saves an already validated image under a server-generated name.</summary>
        private string UploadImage(IFormFile formFile)
        {
            if (formFile == null) return null;

            string uploadsFolderPath = Path.Combine(this.webHostEnvironment.WebRootPath, "images", "candidates");
            Directory.CreateDirectory(uploadsFolderPath);

            // Never reuse the client's file name: it can contain path segments or misleading extensions.
            var uniqueFileName = Guid.NewGuid().ToString("N") + Path.GetExtension(formFile.FileName).ToLowerInvariant();
            using (var fileStream = new FileStream(Path.Combine(uploadsFolderPath, uniqueFileName), FileMode.CreateNew))
            {
                formFile.CopyTo(fileStream);
            }
            return uniqueFileName;
        }

        //Ajax
        [AllowAnonymous]
        public async Task<IActionResult> GetCandidadesAjax()
        {
            return Json(await this.repository.Candidates.GetAllAsync());
        }
    }
}
