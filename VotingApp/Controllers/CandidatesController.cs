using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
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
        public async Task<IActionResult> Index()
        {
            return View(await repository.Candidates.GetAllAsync());
        }

        // GET: Candidates/Details/5
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
        public IActionResult Create()
        {
            return View();
        }

        // POST: Candidates/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateCandidateModel model)
        {
            if (ModelState.IsValid)
            {
                var candidate = new Candidate
                {
                    Id=Guid.NewGuid(),
                    Name = model.Name,
                    Description = model.Description,
                    Avatar = UploadImage(model.Image)
                };
                repository.Candidates.Add(candidate);
                await repository.SaveAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // GET: Candidates/Edit/5
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
        public async Task<IActionResult> Edit(Guid id, EditCandidateModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    model.Avatar = UploadImage(model.Image);
                    var candidate = this.mapper.Map<Candidate>(model);
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

            return View(candidate);
        }

        // POST: Candidates/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            try
            {
                this.repository.Candidates.Delete(m => m.Id.Equals(id));
                await this.repository.SaveAsync();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        private bool CandidateExists(Guid id)
        {
            return repository.Candidates.Any(e => e.Id == id);
        }

        private string UploadImage(IFormFile formFile)
        {
            string uniqueFileName = null;

            if (formFile != null)
            {
                string uploadsFolderPath = Path.Combine(this.webHostEnvironment.WebRootPath, "images/candidates");

                if (!Directory.Exists(uploadsFolderPath))
                {
                    Directory.CreateDirectory(uploadsFolderPath);
                }
                uniqueFileName = Guid.NewGuid().ToString() + "_" + formFile.FileName;
                string filePath = Path.Combine(uploadsFolderPath, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    formFile.CopyTo(fileStream);
                }
            }
            return uniqueFileName;
        }



        //Ajax

        public async Task<IActionResult> GetCandidadesAjax()
        {
            return Json(await this.repository.Candidates.GetAllAsync());
        }
    }
}
