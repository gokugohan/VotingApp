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
using VotingApp.Models;
using VotingApp.SignalRHub;

namespace VotingApp.Controllers
{
    public class AreasController : BaseController
    {
        public AreasController(IRepositoryWrapper repository, IHubContext<VotingHub> hubContext, IMapper mapper, ILogger<BaseController> logger) : base(repository, hubContext, mapper,logger)
        {
        }


        // GET: Areas
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var municipalities = await this.repository.Areas.GetAllAsync(m => m.Level.Equals(2));
            var administrative_posts = await this.repository.Areas.GetAllAsync(m => m.Level.Equals(3));
            var villages = await this.repository.Areas.GetAllAsync(m => m.Level.Equals(4));

            var model = new IndexAreaModel
            {
                Municipalities = municipalities,
                AdministrativePosts = administrative_posts,
                Villages = villages
            };

            return View(model);
        }

        [AllowAnonymous]
        public async Task<IActionResult> GetMunicipalities()
        {
            var municipalities = await this.repository.Areas.GetAllAsync(m => m.Level.Equals(2));
            return Json(new { data = municipalities });
        }
        [AllowAnonymous]
        public async Task<IActionResult> GetArea(int level, string parentId)
        {
            var areas = await this.repository.Areas.GetAllAsync(m => m.Level.Equals(level) & m.ParentId.Equals(parentId));
            return Json(new { data = areas });
        }


        // GET: Areas/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var area = await repository.Areas.GetAsync(id);
            if (area == null)
            {
                return NotFound();
            }


            return View(area);
        }

        //// GET: Areas/Create
        //public IActionResult Create()
        //{

        //    ViewData["AreaId"] = new SelectList(this.repository.Areas.GetAll(), "Id", "Name");
        //    return View();
        //}

        //// POST: Areas/Create
        //// To protect from overposting attacks, enable the specific properties you want to bind to, for 
        //// more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Create([Bind("Id,Name,ParentId,Level")] Area area)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        await repository.Areas.AddAsync(area);
        //        await repository.SaveAsync();
        //        return RedirectToAction(nameof(Index));
        //    }
        //    ViewData["AreaId"] = new SelectList(this.repository.Areas.GetAll(), "Id", "Name");
        //    return View(area);
        //}

        //// GET: Areas/Edit/5
        //public async Task<IActionResult> Edit(string id)
        //{
        //    if (id == null)
        //    {
        //        return NotFound();
        //    }

        //    var area = await repository.Areas.GetAsync(id);
        //    if (area == null)
        //    {
        //        return NotFound();
        //    }
        //    ViewData["AreaId"] = new SelectList(this.repository.Areas.GetAll(), "Id", "Name");
        //    return View(area);
        //}

        //// POST: Areas/Edit/5
        //// To protect from overposting attacks, enable the specific properties you want to bind to, for 
        //// more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Edit(string id, [Bind("Id,Name,ParentId,Level")] Area area)
        //{
        //    if (id != area.Id)
        //    {
        //        return NotFound();
        //    }

        //    if (ModelState.IsValid)
        //    {
        //        try
        //        {
        //            repository.Areas.Update(area);
        //            await repository.SaveAsync();
        //        }
        //        catch (DbUpdateConcurrencyException)
        //        {
        //            if (!AreaExists(area.Id))
        //            {
        //                return NotFound();
        //            }
        //            else
        //            {
        //                throw;
        //            }
        //        }
        //        return RedirectToAction(nameof(Index));
        //    }
        //    return View(area);
        //}

        //// GET: Areas/Delete/5
        //public async Task<IActionResult> Delete(string id)
        //{
        //    if (id == null)
        //    {
        //        return NotFound();
        //    }

        //    var area = await repository.Areas.GetAsync(id);
        //    if (area == null)
        //    {
        //        return NotFound();
        //    }

        //    return View(area);
        //}

        //// POST: Areas/Delete/5
        //[HttpPost, ActionName("Delete")]
        //[ValidateAntiForgeryToken]
        //public IActionResult DeleteConfirmed(string id)
        //{
        //    try
        //    {
        //        this.repository.Areas.Delete(m=>m.Id.Equals(id));
        //        return RedirectToAction(nameof(Index));
        //    }
        //    catch (Exception ex)
        //    {
        //        this.logger.LogError(ex.Message);
        //    }

        //    return RedirectToAction(nameof(Delete), new { id = id });
        //}

        //private bool AreaExists(string id)
        //{
        //    return repository.Areas.Any(e => e.Id == id);
        //}
    }
}
