using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VotingApp.Application.Geography;
using VotingApp.Domain.Entities;
using VotingApp.Models;

namespace VotingApp.Controllers
{
    public class AreasController : BaseController
    {
        private readonly AreaService areas;

        public AreasController(AreaService areas) => this.areas = areas;

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var ct = HttpContext.RequestAborted;
            return View(new IndexAreaModel
            {
                Municipalities = await areas.GetByLevelAsync(AreaHierarchy.Municipality, ct),
                AdministrativePosts = await areas.GetByLevelAsync(AreaHierarchy.AdministrativePost, ct),
                Villages = await areas.GetByLevelAsync(AreaHierarchy.Village, ct),
            });
        }

        [AllowAnonymous]
        public async Task<IActionResult> GetMunicipalities() =>
            Json(new { data = await areas.GetMunicipalitiesAsync(HttpContext.RequestAborted) });

        [AllowAnonymous]
        public async Task<IActionResult> GetArea(int level, string parentId) =>
            Json(new { data = await areas.GetChildrenAsync(level, parentId, HttpContext.RequestAborted) });

        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var area = await areas.GetAsync(id, HttpContext.RequestAborted);
            return area == null ? NotFound() : View(area);
        }
    }
}
