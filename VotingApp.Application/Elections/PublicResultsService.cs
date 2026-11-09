using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VotingApp.Application.Geography;
using VotingApp.Application.Abstractions;
using VotingApp.Domain.Elections;
using VotingApp.Domain.Entities;

namespace VotingApp.Application.Elections
{
    public sealed record BallotContestant(Guid Id, string Name, string Acronym, int BallotPosition);

    public sealed record MunicipalityResult(
        string MunicipalityId,
        string Name,
        string MapKey,
        int PollingStations,
        int SheetsCounted,
        long ValidVotes,
        IReadOnlyDictionary<Guid, long> Votes)
    {
        public decimal ReportingPercent => PollingStations == 0 ? 0 : Math.Round(SheetsCounted * 100m / PollingStations, 1);

        /// <summary>The contestant with the most votes, or null if none or tied.</summary>
        public Guid? LeaderId
        {
            get
            {
                var top = Votes.Where(v => v.Value > 0).OrderByDescending(v => v.Value).Take(2).ToList();
                if (top.Count == 0 || (top.Count == 2 && top[0].Value == top[1].Value)) return null;
                return top[0].Key;
            }
        }
    }

    public sealed record RoundBreakdown(Guid RoundId, IReadOnlyList<BallotContestant> Ballot, IReadOnlyList<MunicipalityResult> Municipalities);

    public sealed record StationResult(
        Guid PollingStationAreaId,
        string AdministrativePost,
        string Village,
        string Station,
        ResultSheetStatus? Status,
        int? RegisteredVoters,
        int? BallotsUsed,
        int BlankVotes,
        int InvalidVotes,
        IReadOnlyDictionary<Guid, int> Votes,
        string ScanPath)
    {
        public bool IsCounted => Status >= ResultSheetStatus.Approved;
    }

    /// <summary>
    /// Public, read-only views of a round below the national level: per municipality, per polling
    /// station, and a station-level CSV for open data. Only approved and locked sheets are published;
    /// stations without a counted sheet are listed as not yet reported.
    /// </summary>
    public class PublicResultsService
    {
        private const int CodeLength = AreaHierarchy.MunicipalityCodeLength;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(15);
        private readonly IAppDbContext db;
        private readonly IMemoryCache cache;

        public PublicResultsService(IAppDbContext db, IMemoryCache cache)
        {
            this.db = db;
            this.cache = cache;
        }

        public static string BreakdownKey(Guid roundId) => $"round-breakdown:{roundId}";

        public async Task<IReadOnlyList<BallotContestant>> GetBallotAsync(Guid roundId, CancellationToken ct = default) =>
            await db.RoundContestants.AsNoTracking()
                .Where(rc => rc.ElectionRoundId == roundId)
                .OrderBy(rc => rc.BallotPosition)
                .Select(rc => new BallotContestant(rc.ContestantId, rc.Contestant.Name, rc.Contestant.Acronym, rc.BallotPosition))
                .ToListAsync(ct);

        /// <summary>Counted votes and reporting progress per municipality (cached briefly).</summary>
        public async Task<RoundBreakdown> GetBreakdownAsync(Guid roundId, CancellationToken ct = default)
        {
            if (cache.TryGetValue(BreakdownKey(roundId), out RoundBreakdown cached)) return cached;
            if (!await db.ElectionRounds.AnyAsync(r => r.Id == roundId, ct)) return null;

            var ballot = await GetBallotAsync(roundId, ct);
            var municipalities = await db.Areas.AsNoTracking()
                .Where(a => a.Level == AreaHierarchy.Municipality)
                .OrderBy(a => a.Name)
                .Select(a => new { a.Id, a.Name })
                .ToListAsync(ct);

            var stations = await db.PollingStationAreas
                .GroupBy(p => p.AreaId.Substring(0, CodeLength))
                .Select(g => new { MunicipalityId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.MunicipalityId, x => x.Count, ct);

            var counted = await db.ResultSheets
                .Where(s => s.ElectionRoundId == roundId && s.Status >= ResultSheetStatus.Approved)
                .GroupBy(s => s.PollingStationArea.AreaId.Substring(0, CodeLength))
                .Select(g => new { MunicipalityId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.MunicipalityId, x => x.Count, ct);

            var votes = await db.ResultSheetEntries
                .Where(e => e.ResultSheet.ElectionRoundId == roundId && e.ResultSheet.Status >= ResultSheetStatus.Approved)
                .GroupBy(e => new { MunicipalityId = e.ResultSheet.PollingStationArea.AreaId.Substring(0, CodeLength), e.ContestantId })
                .Select(g => new { g.Key.MunicipalityId, g.Key.ContestantId, Votes = g.Sum(e => (long)e.Votes) })
                .ToListAsync(ct);
            var votesByMunicipality = votes.ToLookup(v => v.MunicipalityId);

            var rows = municipalities.Select(m =>
            {
                var perContestant = ballot.ToDictionary(c => c.Id,
                    c => votesByMunicipality[m.Id].Where(v => v.ContestantId == c.Id).Sum(v => v.Votes));
                return new MunicipalityResult(m.Id, m.Name, MapKeys.ForMunicipality(m.Id),
                    stations.TryGetValue(m.Id, out var s) ? s : 0,
                    counted.TryGetValue(m.Id, out var c) ? c : 0,
                    perContestant.Values.Sum(),
                    perContestant);
            }).ToList();

            var breakdown = new RoundBreakdown(roundId, ballot, rows);
            cache.Set(BreakdownKey(roundId), breakdown, CacheDuration);
            return breakdown;
        }

        /// <summary>
        /// Every polling station in a municipality with its published figures. Sheets that are not yet
        /// approved show only their status, never their figures.
        /// </summary>
        public async Task<List<StationResult>> GetStationsAsync(Guid roundId, string municipalityId, CancellationToken ct = default)
        {
            if (municipalityId?.Length != CodeLength) return new List<StationResult>();
            var stations = await db.PollingStationAreas.AsNoTracking()
                .Where(p => p.AreaId.StartsWith(municipalityId))
                .Select(p => new { p.Id, Village = p.Area.Name, PostId = p.Area.ParentId, Station = p.PollingStation.Name })
                .ToListAsync(ct);
            // Prefix filters rather than list.Contains (which EF Core 8 sends as OPENJSON, needing SQL compat level 130+).
            var posts = await db.Areas.AsNoTracking()
                .Where(a => a.Level == AreaHierarchy.AdministrativePost && a.Id.StartsWith(municipalityId))
                .ToDictionaryAsync(a => a.Id, a => a.Name, ct);
            var sheets = await db.ResultSheets.AsNoTracking()
                .Where(s => s.ElectionRoundId == roundId && s.PollingStationArea.AreaId.StartsWith(municipalityId))
                .Include(s => s.Entries)
                .ToDictionaryAsync(s => s.PollingStationAreaId, ct);

            return stations.Select(st =>
            {
                sheets.TryGetValue(st.Id, out var sheet);
                var published = sheet != null && sheet.Status >= ResultSheetStatus.Approved;
                return new StationResult(st.Id,
                    st.PostId != null && posts.TryGetValue(st.PostId, out var post) ? post : null,
                    st.Village, st.Station, sheet?.Status,
                    published ? sheet.RegisteredVoters : null,
                    published ? sheet.BallotsUsed : null,
                    published ? sheet.BlankVotes : 0,
                    published ? sheet.InvalidVotes : 0,
                    published ? sheet.Entries.ToDictionary(e => e.ContestantId, e => e.Votes) : new Dictionary<Guid, int>(),
                    published ? sheet.ScanPath : null);
            })
            .OrderBy(s => s.AdministrativePost).ThenBy(s => s.Village).ThenBy(s => s.Station)
            .ToList();
        }

        /// <summary>Open data: one CSV row per counted (approved or locked) sheet, one column per contestant.</summary>
        public async Task<string> ExportCountedSheetsCsvAsync(Guid roundId, CancellationToken ct = default)
        {
            var ballot = await GetBallotAsync(roundId, ct);
            var sheets = await db.ResultSheets.AsNoTracking()
                .Where(s => s.ElectionRoundId == roundId && s.Status >= ResultSheetStatus.Approved)
                .Include(s => s.Entries)
                .Include(s => s.PollingStationArea).ThenInclude(p => p.Area)
                .Include(s => s.PollingStationArea).ThenInclude(p => p.PollingStation)
                .ToListAsync(ct);
            var areaNames = await db.Areas.AsNoTracking()
                .Where(a => a.Level == AreaHierarchy.Municipality || a.Level == AreaHierarchy.AdministrativePost)
                .ToDictionaryAsync(a => a.Id, a => a.Name, ct);

            var csv = new StringBuilder();
            var header = new List<string>
            {
                "municipality_id", "municipality", "administrative_post", "village", "polling_station", "status",
                "registered_voters", "ballots_received", "ballots_used", "ballots_spoiled", "ballots_unused",
                "blank_votes", "invalid_votes",
            };
            header.AddRange(ballot.Select(c => c.Name));
            csv.AppendLine(string.Join(",", header.Select(Escape)));

            foreach (var s in sheets
                .OrderBy(s => s.PollingStationArea.AreaId)
                .ThenBy(s => s.PollingStationArea.PollingStation?.Name))
            {
                var areaId = s.PollingStationArea.AreaId;
                var municipalityId = areaId?.Length >= CodeLength ? AreaHierarchy.MunicipalityIdOf(areaId) : null;
                var postId = s.PollingStationArea.Area?.ParentId;
                var votes = s.Entries.ToDictionary(e => e.ContestantId, e => e.Votes);
                var row = new List<string>
                {
                    municipalityId,
                    municipalityId != null && areaNames.TryGetValue(municipalityId, out var m) ? m : null,
                    postId != null && areaNames.TryGetValue(postId, out var p) ? p : null,
                    s.PollingStationArea.Area?.Name,
                    s.PollingStationArea.PollingStation?.Name,
                    s.Status.ToString(),
                    N(s.RegisteredVoters), N(s.BallotsReceived), N(s.BallotsUsed), N(s.BallotsSpoiled), N(s.BallotsUnused),
                    N(s.BlankVotes), N(s.InvalidVotes),
                };
                row.AddRange(ballot.Select(c => votes.TryGetValue(c.Id, out var v) ? N(v) : "0"));
                csv.AppendLine(string.Join(",", row.Select(Escape)));
            }
            return csv.ToString();
        }

        private static string N(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

        /// <summary>RFC 4180 quoting, plus a guard against spreadsheet formula injection.</summary>
        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if ("=+-@".Contains(value[0]) && !int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
            {
                value = "'" + value;
            }
            return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }
    }
}
