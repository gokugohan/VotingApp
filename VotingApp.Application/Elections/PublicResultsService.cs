using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

    public sealed record PublishedRound(Guid RoundId, Guid ElectionId, string ElectionName, int RoundNumber, ElectionStatus Status, DateTime VotingDate)
    {
        public string Label => RoundNumber > 1 ? $"{ElectionName} (round {RoundNumber})" : ElectionName;
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
        private readonly IAppDbContext db;
        private readonly RoundResultsCache cache;
        private readonly StationRosterService rosters;

        public PublicResultsService(IAppDbContext db, RoundResultsCache cache, StationRosterService rosters)
        {
            this.db = db;
            this.cache = cache;
            this.rosters = rosters;
        }

        /// <summary>Cache scope for data that is not tied to one round (the list of published rounds).</summary>
        public static readonly Guid AllRounds = Guid.Empty;

        /// <summary>
        /// Rounds the public dashboard can show (counting or later), newest first. The first one is the
        /// default ("featured") round.
        /// </summary>
        public Task<IReadOnlyList<PublishedRound>> ListPublishedRoundsAsync(CancellationToken ct = default) =>
            cache.GetOrBuildAsync<IReadOnlyList<PublishedRound>>(AllRounds, "published-rounds", async () => await db.ElectionRounds.AsNoTracking()
                .Where(r => r.Status >= ElectionStatus.Counting)
                .OrderByDescending(r => r.VotingDate).ThenByDescending(r => r.Number)
                .Select(r => new PublishedRound(r.Id, r.ElectionId, r.Election.Name, r.Number, r.Status, r.VotingDate))
                .ToListAsync());

        public Task<IReadOnlyList<BallotContestant>> GetBallotAsync(Guid roundId, CancellationToken ct = default) =>
            cache.GetOrBuildAsync<IReadOnlyList<BallotContestant>>(roundId, "ballot", async () => await db.RoundContestants.AsNoTracking()
                .Where(rc => rc.ElectionRoundId == roundId)
                .OrderBy(rc => rc.BallotPosition)
                .Select(rc => new BallotContestant(rc.ContestantId, rc.Contestant.Name, rc.Contestant.Acronym, rc.BallotPosition))
                .ToListAsync());

        /// <summary>
        /// Short fingerprint of the round's published state (counted sheets, last approval/lock, round status).
        /// Live pages poll it and only re-fetch when it changes.
        /// </summary>
        public Task<string> GetSignatureAsync(Guid roundId, CancellationToken ct = default) =>
            cache.GetOrBuildAsync(roundId, "signature", async () =>
            {
                var counted = db.ResultSheets.Where(s => s.ElectionRoundId == roundId && s.Status >= ResultSheetStatus.Approved);
                var count = await counted.CountAsync();
                var approved = await counted.MaxAsync(s => (DateTime?)s.ApprovedAt);
                var locked = await counted.MaxAsync(s => (DateTime?)s.LockedAt);
                var status = await db.ElectionRounds.Where(r => r.Id == roundId).Select(r => (int?)r.Status).FirstOrDefaultAsync();
                return string.Join("-", count, approved?.Ticks ?? 0, locked?.Ticks ?? 0, status ?? -1);
            });

        /// <summary>Counted votes and reporting progress per municipality (cached briefly).</summary>
        public Task<RoundBreakdown> GetBreakdownAsync(Guid roundId, CancellationToken ct = default) =>
            cache.GetOrBuildAsync(roundId, "breakdown", () => BuildBreakdownAsync(roundId, CancellationToken.None));

        /// <summary>A round's sheets, read once per change and shared by the breakdown, station pages and CSV.</summary>
        private sealed record SheetSnapshot(
            ResultSheetStatus Status, int? RegisteredVoters, int? BallotsReceived, int? BallotsUsed, int? BallotsSpoiled,
            int? BallotsUnused, int BlankVotes, int InvalidVotes, string ScanPath, IReadOnlyDictionary<Guid, int> Votes)
        {
            public bool IsCounted => Status >= ResultSheetStatus.Approved;
        }

        private Task<IReadOnlyDictionary<Guid, SheetSnapshot>> GetSheetIndexAsync(Guid roundId) =>
            cache.GetOrBuildAsync<IReadOnlyDictionary<Guid, SheetSnapshot>>(roundId, "sheets", async () =>
            {
                var sheets = await db.ResultSheets.AsNoTracking()
                    .Where(s => s.ElectionRoundId == roundId)
                    .Include(s => s.Entries)
                    .ToListAsync();
                return sheets.ToDictionary(s => s.PollingStationAreaId, s => new SheetSnapshot(
                    s.Status, s.RegisteredVoters, s.BallotsReceived, s.BallotsUsed, s.BallotsSpoiled, s.BallotsUnused,
                    s.BlankVotes, s.InvalidVotes, s.ScanPath, s.Entries.ToDictionary(e => e.ContestantId, e => e.Votes)));
            });

        private async Task<RoundBreakdown> BuildBreakdownAsync(Guid roundId, CancellationToken ct)
        {
            if (!await db.ElectionRounds.AnyAsync(r => r.Id == roundId, ct)) return null;

            var ballot = await GetBallotAsync(roundId, ct);
            var roster = await rosters.GetForRoundAsync(roundId, ct);
            var sheets = await GetSheetIndexAsync(roundId);

            var rows = roster.Stations
                .GroupBy(st => new { st.MunicipalityId, st.MunicipalityName })
                .OrderBy(g => g.Key.MunicipalityName)
                .Select(g =>
                {
                    var counted = g.Select(st => sheets.TryGetValue(st.PollingStationAreaId, out var sh) ? sh : null)
                        .Where(sh => sh != null && sh.IsCounted).ToList();
                    var perContestant = ballot.ToDictionary(c => c.Id,
                        c => counted.Sum(sh => sh.Votes.TryGetValue(c.Id, out var v) ? (long)v : 0));
                    return new MunicipalityResult(g.Key.MunicipalityId, g.Key.MunicipalityName, MapKeys.ForMunicipality(g.Key.MunicipalityId),
                        g.Count(), counted.Count, perContestant.Values.Sum(), perContestant);
                })
                .ToList();

            return new RoundBreakdown(roundId, ballot, rows);
        }

        /// <summary>
        /// Every polling station of the election in a municipality with its published figures. Sheets that
        /// are not yet approved show only their status, never their figures.
        /// </summary>
        public async Task<IReadOnlyList<StationResult>> GetStationsAsync(Guid roundId, string municipalityId, CancellationToken ct = default)
        {
            if (municipalityId?.Length != CodeLength) return new List<StationResult>();
            return await cache.GetOrBuildAsync<IReadOnlyList<StationResult>>(roundId, "stations:" + municipalityId,
                async () => await BuildStationsAsync(roundId, municipalityId, CancellationToken.None));
        }

        private async Task<List<StationResult>> BuildStationsAsync(Guid roundId, string municipalityId, CancellationToken ct)
        {
            var roster = await rosters.GetForRoundAsync(roundId, ct);
            var sheets = await GetSheetIndexAsync(roundId);
            return roster.Stations
                .Where(st => st.MunicipalityId == municipalityId)
                .Select(st =>
                {
                    sheets.TryGetValue(st.PollingStationAreaId, out var sheet);
                    var published = sheet != null && sheet.IsCounted;
                    return new StationResult(st.PollingStationAreaId, st.AdministrativePostName, st.VillageName, st.StationName, sheet?.Status,
                        published ? sheet.RegisteredVoters : null,
                        published ? sheet.BallotsUsed : null,
                        published ? sheet.BlankVotes : 0,
                        published ? sheet.InvalidVotes : 0,
                        published ? sheet.Votes : new Dictionary<Guid, int>(),
                        published ? sheet.ScanPath : null);
                })
                .OrderBy(s => s.AdministrativePost).ThenBy(s => s.Village).ThenBy(s => s.Station)
                .ToList();
        }

        /// <summary>Open data: one CSV row per counted (approved or locked) sheet, one column per contestant.</summary>
        public Task<string> ExportCountedSheetsCsvAsync(Guid roundId, CancellationToken ct = default) =>
            cache.GetOrBuildAsync(roundId, "csv", () => BuildCsvAsync(roundId, CancellationToken.None));

        private async Task<string> BuildCsvAsync(Guid roundId, CancellationToken ct)
        {
            var ballot = await GetBallotAsync(roundId, ct);
            var roster = await rosters.GetForRoundAsync(roundId, ct);
            var sheets = await GetSheetIndexAsync(roundId);

            var csv = new StringBuilder();
            var header = new List<string>
            {
                "municipality_id", "municipality", "administrative_post", "village", "polling_station", "status",
                "registered_voters", "ballots_received", "ballots_used", "ballots_spoiled", "ballots_unused",
                "blank_votes", "invalid_votes",
            };
            header.AddRange(ballot.Select(c => c.Name));
            csv.AppendLine(string.Join(",", header.Select(Escape)));

            foreach (var st in roster.Stations.OrderBy(st => st.AreaId).ThenBy(st => st.StationName))
            {
                if (!sheets.TryGetValue(st.PollingStationAreaId, out var s) || !s.IsCounted) continue;
                var row = new List<string>
                {
                    st.MunicipalityId, st.MunicipalityName, st.AdministrativePostName, st.VillageName, st.StationName,
                    s.Status.ToString(),
                    N(s.RegisteredVoters), N(s.BallotsReceived), N(s.BallotsUsed), N(s.BallotsSpoiled), N(s.BallotsUnused),
                    N(s.BlankVotes), N(s.InvalidVotes),
                };
                row.AddRange(ballot.Select(c => s.Votes.TryGetValue(c.Id, out var v) ? N(v) : "0"));
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
