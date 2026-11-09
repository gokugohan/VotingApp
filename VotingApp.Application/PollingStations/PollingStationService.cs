using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VotingApp.Application.Abstractions;
using VotingApp.Application.Common;
using VotingApp.Domain.Entities;

namespace VotingApp.Application.PollingStations
{
    public sealed record PollingStationOption(Guid Id, string Name);

    /// <summary>A polling station placed in a village, with its administrative parents.</summary>
    public sealed record PollingStationLocation(PollingStationArea Station, Area AdministrativePost, Area Municipality);

    /// <summary>
    /// Polling stations (<see cref="PollingStation"/>) and their placement in a village
    /// (<see cref="PollingStationArea"/>, the unit results are recorded against).
    /// </summary>
    public class PollingStationService
    {
        private readonly IAppDbContext db;
        private readonly ILogger<PollingStationService> logger;

        public PollingStationService(IAppDbContext db, ILogger<PollingStationService> logger)
        {
            this.db = db;
            this.logger = logger;
        }

        // ---- Polling stations ----

        public Task<List<PollingStation>> ListStationsAsync(CancellationToken ct = default) =>
            db.PollingStations.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);

        public Task<PollingStation> GetStationAsync(Guid id, CancellationToken ct = default) =>
            db.PollingStations.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

        public async Task<OperationResult<Guid>> CreateStationAsync(string name, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                return OperationResult<Guid>.Fail(FailureKind.Validation, "Name is required.");
            var station = new PollingStation { Id = Guid.NewGuid(), Name = name.Trim() };
            db.PollingStations.Add(station);
            await db.SaveChangesAsync(ct);
            return OperationResult<Guid>.Success(station.Id);
        }

        public async Task<OperationResult> RenameStationAsync(Guid id, string name, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                return OperationResult.Fail(FailureKind.Validation, "Name is required.");
            var station = await db.PollingStations.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (station == null) return OperationResult.NotFound("Polling station not found.");
            station.Name = name.Trim();
            await db.SaveChangesAsync(ct);
            return OperationResult.Success();
        }

        public async Task<OperationResult> DeleteStationAsync(Guid id, CancellationToken ct = default)
        {
            var station = await db.PollingStations.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (station == null) return OperationResult.NotFound("Polling station not found.");
            if (await db.PollingStationAreas.AnyAsync(p => p.PollingStationId == id, ct))
                return OperationResult.Fail(FailureKind.Conflict, "The polling station is placed in a village; remove that first.");
            db.PollingStations.Remove(station);
            return await TrySaveAsync("polling station", id, ct);
        }

        // ---- Polling stations placed in villages ----

        public Task<List<PollingStationArea>> ListLocationsAsync(CancellationToken ct = default) =>
            db.PollingStationAreas.AsNoTracking()
                .Include(p => p.Area).Include(p => p.PollingStation)
                .ToListAsync(ct);

        /// <summary>Options for vote-entry dropdowns: "Village - Station", sorted by name.</summary>
        public async Task<List<PollingStationOption>> ListLocationOptionsAsync(CancellationToken ct = default)
        {
            var rows = await db.PollingStationAreas.AsNoTracking()
                .Select(p => new { p.Id, Area = p.Area.Name, Station = p.PollingStation.Name })
                .ToListAsync(ct);
            return rows.Select(r => new PollingStationOption(r.Id, $"{r.Area} - {r.Station}"))
                .OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public Task<PollingStationArea> GetLocationAsync(Guid id, CancellationToken ct = default) =>
            db.PollingStationAreas.AsNoTracking()
                .Include(p => p.Area).Include(p => p.PollingStation)
                .FirstOrDefaultAsync(p => p.Id == id, ct);

        public async Task<PollingStationLocation> GetLocationWithParentsAsync(Guid id, CancellationToken ct = default)
        {
            var station = await GetLocationAsync(id, ct);
            if (station == null) return null;
            var post = station.Area == null ? null
                : await db.Areas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == station.Area.ParentId, ct);
            var municipality = post == null ? null
                : await db.Areas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == post.ParentId, ct);
            return new PollingStationLocation(station, post, municipality);
        }

        /// <summary>
        /// Villages in a municipality that host a polling station, named "Village | Station"
        /// (shape expected by the existing municipality picker).
        /// </summary>
        public async Task<List<Area>> ListLocationsInMunicipalityAsync(string municipalityId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(municipalityId)) return new List<Area>();
            var prefix = municipalityId;
            var rows = await db.PollingStationAreas.AsNoTracking()
                .Where(p => p.Area.ParentId.StartsWith(prefix))
                .Select(p => new { p.Area.Id, Area = p.Area.Name, p.Area.Level, Station = p.PollingStation.Name })
                .ToListAsync(ct);
            return rows.Select(r => new Area { Id = r.Id, Name = $"{r.Area} | {r.Station}", Level = r.Level }).ToList();
        }

        /// <summary>Creates a polling station and places it in a village in one step.</summary>
        public async Task<OperationResult<Guid>> CreateLocationAsync(string villageId, string stationName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(stationName))
                return OperationResult<Guid>.Fail(FailureKind.Validation, "Polling station name is required.");
            if (!await db.Areas.AnyAsync(a => a.Id == villageId, ct))
                return OperationResult<Guid>.Fail(FailureKind.Validation, "Select a valid village.");

            var location = new PollingStationArea
            {
                Id = Guid.NewGuid(),
                AreaId = villageId,
                PollingStation = new PollingStation { Id = Guid.NewGuid(), Name = stationName.Trim() },
            };
            db.PollingStationAreas.Add(location);
            await db.SaveChangesAsync(ct);
            return OperationResult<Guid>.Success(location.Id);
        }

        /// <summary>Moves a polling station to another village and/or renames it.</summary>
        public async Task<OperationResult> UpdateLocationAsync(Guid id, string villageId, string stationName, CancellationToken ct = default)
        {
            var location = await db.PollingStationAreas.Include(p => p.PollingStation).FirstOrDefaultAsync(p => p.Id == id, ct);
            if (location == null) return OperationResult.NotFound("Polling station not found.");
            if (!string.IsNullOrEmpty(villageId) && villageId != location.AreaId)
            {
                if (!await db.Areas.AnyAsync(a => a.Id == villageId, ct))
                    return OperationResult.Fail(FailureKind.Validation, "Select a valid village.");
                location.AreaId = villageId;
            }
            if (!string.IsNullOrWhiteSpace(stationName))
            {
                location.PollingStation.Name = stationName.Trim();
            }
            await db.SaveChangesAsync(ct);
            return OperationResult.Success();
        }

        public async Task<OperationResult> DeleteLocationAsync(Guid id, CancellationToken ct = default)
        {
            var location = await db.PollingStationAreas.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (location == null) return OperationResult.NotFound("Polling station not found.");
            if (await db.Votes.AnyAsync(v => v.PollingStationAreaId == id, ct) ||
                await db.ResultSheets.AnyAsync(s => s.PollingStationAreaId == id, ct))
            {
                return OperationResult.Fail(FailureKind.Conflict, "Results are recorded for this polling station; it cannot be deleted.");
            }
            db.PollingStationAreas.Remove(location);
            return await TrySaveAsync("polling station location", id, ct);
        }

        private async Task<OperationResult> TrySaveAsync(string what, Guid id, CancellationToken ct)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                return OperationResult.Success();
            }
            catch (DbUpdateException ex)
            {
                logger.LogWarning(ex, "Deleting {What} {Id} failed", what, id);
                return OperationResult.Fail(FailureKind.Conflict, $"The {what} is still in use and cannot be deleted.");
            }
        }
    }
}
