using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VotingApp.Application.Abstractions;
using VotingApp.Domain.Entities;

namespace VotingApp.Application.Geography
{
    /// <summary>Keys used by the Highcharts Timor-Leste map for each municipality code.</summary>
    public static class MapKeys
    {
        private static readonly IReadOnlyDictionary<string, string> Keys = new Dictionary<string, string>
        {
            ["TLS001"] = "tl-an", ["TLS002"] = "tl-al", ["TLS003"] = "tl-bc", ["TLS004"] = "tl-bb",
            ["TLS005"] = "tl-cl", ["TLS006"] = "tl-dl", ["TLS007"] = "tl-er", ["TLS008"] = "tl-lq",
            ["TLS009"] = "tl-bt", ["TLS010"] = "tl-mf", ["TLS011"] = "tl-mt", ["TLS012"] = "tl-am",
            ["TLS013"] = "tl-vq",
        };

        /// <summary>The map key for a municipality, or null if the map has no shape for it.</summary>
        public static string ForMunicipality(string municipalityId) =>
            municipalityId != null && Keys.TryGetValue(municipalityId, out var key) ? key : null;
    }

    public class AreaService
    {
        private readonly IAppDbContext db;

        public AreaService(IAppDbContext db) => this.db = db;

        public Task<List<Area>> GetByLevelAsync(int level, CancellationToken ct = default) =>
            db.Areas.AsNoTracking().Where(a => a.Level == level).OrderBy(a => a.Name).ToListAsync(ct);

        public Task<List<Area>> GetMunicipalitiesAsync(CancellationToken ct = default) =>
            GetByLevelAsync(AreaHierarchy.Municipality, ct);

        public Task<List<Area>> GetChildrenAsync(int level, string parentId, CancellationToken ct = default) =>
            db.Areas.AsNoTracking().Where(a => a.Level == level && a.ParentId == parentId).OrderBy(a => a.Name).ToListAsync(ct);

        public Task<Area> GetAsync(string id, CancellationToken ct = default) =>
            db.Areas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
    }
}
