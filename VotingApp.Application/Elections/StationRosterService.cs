using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VotingApp.Application.Abstractions;
using VotingApp.Domain.Elections;
using VotingApp.Domain.Entities;

namespace VotingApp.Application.Elections
{
    public sealed record RosterStation(
        Guid PollingStationAreaId,
        string StationName,
        string AreaId,
        string VillageName,
        string AdministrativePostId,
        string AdministrativePostName,
        string MunicipalityId,
        string MunicipalityName);

    /// <summary>The polling stations that belong to an election.</summary>
    public sealed class StationRoster
    {
        public StationRoster(IReadOnlyList<RosterStation> stations, DateTime? frozenAtUtc)
        {
            Stations = stations;
            FrozenAtUtc = frozenAtUtc;
            ById = stations.ToDictionary(s => s.PollingStationAreaId);
        }

        public IReadOnlyList<RosterStation> Stations { get; }
        public IReadOnlyDictionary<Guid, RosterStation> ById { get; }

        /// <summary>Set once the election has opened; null means the live list (election still in set-up).</summary>
        public DateTime? FrozenAtUtc { get; }
        public bool IsFrozen => FrozenAtUtc.HasValue;

        public RosterStation Find(Guid pollingStationAreaId) => ById.TryGetValue(pollingStationAreaId, out var s) ? s : null;
    }

    /// <summary>
    /// Which polling stations belong to an election: the copy frozen when it opened, or the live
    /// list while it is still being set up. Everything about a round's results reads stations here.
    /// </summary>
    public class StationRosterService
    {
        private readonly IAppDbContext db;
        private readonly RoundResultsCache cache;

        public StationRosterService(IAppDbContext db, RoundResultsCache cache)
        {
            this.db = db;
            this.cache = cache;
        }

        public async Task<StationRoster> GetForRoundAsync(Guid roundId, CancellationToken ct = default)
        {
            var electionId = await db.ElectionRounds.Where(r => r.Id == roundId).Select(r => (Guid?)r.ElectionId).FirstOrDefaultAsync(ct);
            if (electionId == null) return new StationRoster(Array.Empty<RosterStation>(), null);
            return await cache.GetOrBuildAsync(roundId, "roster", () => GetForElectionAsync(electionId.Value, CancellationToken.None));
        }

        public async Task<StationRoster> GetForElectionAsync(Guid electionId, CancellationToken ct = default)
        {
            var frozen = await db.ElectionStations.AsNoTracking().Where(s => s.ElectionId == electionId).ToListAsync(ct);
            if (frozen.Count > 0)
            {
                return new StationRoster(
                    frozen.Select(s => new RosterStation(s.PollingStationAreaId, s.StationName, s.AreaId, s.VillageName,
                            s.AdministrativePostId, s.AdministrativePostName, s.MunicipalityId, s.MunicipalityName))
                        .OrderBy(s => s.MunicipalityName).ThenBy(s => s.VillageName).ThenBy(s => s.StationName).ToList(),
                    frozen.Min(s => s.FrozenAtUtc));
            }
            return new StationRoster(await BuildLiveAsync(ct), null);
        }

        /// <summary>
        /// Copies the current station list into the election (once). Called when the election opens;
        /// the caller saves the changes.
        /// </summary>
        public async Task<int> FreezeAsync(Guid electionId, CancellationToken ct = default)
        {
            if (await db.ElectionStations.AnyAsync(s => s.ElectionId == electionId, ct)) return 0;
            var now = DateTime.UtcNow;
            var live = await BuildLiveAsync(ct);
            db.ElectionStations.AddRange(live.Select(s => new ElectionStation
            {
                Id = Guid.NewGuid(),
                ElectionId = electionId,
                PollingStationAreaId = s.PollingStationAreaId,
                StationName = s.StationName,
                AreaId = s.AreaId,
                VillageName = s.VillageName,
                AdministrativePostId = s.AdministrativePostId,
                AdministrativePostName = s.AdministrativePostName,
                MunicipalityId = s.MunicipalityId,
                MunicipalityName = s.MunicipalityName,
                FrozenAtUtc = now,
            }));
            return live.Count;
        }

        /// <summary>Elections that still depend on this station (open and not yet certified).</summary>
        public Task<List<string>> OpenElectionsUsingAsync(Guid pollingStationAreaId, CancellationToken ct = default) =>
            db.ElectionStations.AsNoTracking()
                .Where(s => s.PollingStationAreaId == pollingStationAreaId && s.Election.Status < ElectionStatus.Certified)
                .Select(s => s.Election.Name)
                .Distinct()
                .ToListAsync(ct);

        private async Task<List<RosterStation>> BuildLiveAsync(CancellationToken ct)
        {
            var stations = await db.PollingStationAreas.AsNoTracking()
                .Select(p => new { p.Id, Station = p.PollingStation.Name, p.AreaId, Village = p.Area.Name, PostId = p.Area.ParentId })
                .ToListAsync(ct);
            var areas = await db.Areas.AsNoTracking()
                .Where(a => a.Level == AreaHierarchy.Municipality || a.Level == AreaHierarchy.AdministrativePost)
                .ToDictionaryAsync(a => a.Id, a => a.Name, ct);
            string Name(string id) => id != null && areas.TryGetValue(id, out var n) ? n : null;

            return stations
                .Where(s => s.AreaId != null && s.AreaId.Length >= AreaHierarchy.MunicipalityCodeLength)
                .Select(s =>
                {
                    var municipalityId = AreaHierarchy.MunicipalityIdOf(s.AreaId);
                    return new RosterStation(s.Id, s.Station, s.AreaId, s.Village, s.PostId, Name(s.PostId), municipalityId, Name(municipalityId) ?? municipalityId);
                })
                .OrderBy(s => s.MunicipalityName).ThenBy(s => s.VillageName).ThenBy(s => s.StationName)
                .ToList();
        }
    }
}
