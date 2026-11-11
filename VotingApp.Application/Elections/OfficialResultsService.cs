using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VotingApp.Application.Abstractions;
using VotingApp.Domain.Elections;

namespace VotingApp.Application.Elections
{
    /// <summary>Participation figures over the counted sheets of a scope (the whole country or one municipality).</summary>
    public sealed record OfficialTotals(
        int PollingStations,
        int SheetsCounted,
        long RegisteredVoters,
        long BallotsUsed,
        long ValidVotes,
        long BlankVotes,
        long InvalidVotes,
        long BallotsUsedWhereRegistered)
    {
        /// <summary>
        /// Ballots used as a share of registered voters, over counted stations that report registered
        /// voters (the field is optional on the sheet). Null when no counted station reports it.
        /// </summary>
        public decimal? TurnoutPercent => RegisteredVoters == 0 ? null : Math.Round(BallotsUsedWhereRegistered * 100m / RegisteredVoters, 2);
        public int StationsMissing => PollingStations - SheetsCounted;
    }

    public sealed record OfficialContestantRow(
        Guid ContestantId,
        int BallotPosition,
        string Name,
        string Acronym,
        long Votes,
        decimal PercentOfValid,
        bool? MeetsThreshold,
        int? Seats);

    /// <summary>One line of the breakdown: a municipality (national record) or a polling station (municipal record).</summary>
    public sealed record OfficialBreakdownRow(
        string Name,
        string Detail,
        int PollingStations,
        int SheetsCounted,
        ResultSheetStatus? Status,
        long RegisteredVoters,
        long BallotsUsed,
        long BlankVotes,
        long InvalidVotes,
        IReadOnlyDictionary<Guid, long> Votes)
    {
        public long ValidVotes => Votes.Values.Sum();
    }

    /// <summary>
    /// The printable results record of a round: what a tabulation body signs and publishes. It carries a
    /// fingerprint of the open-data CSV, so anyone can check that the paper and the published data match.
    /// </summary>
    public sealed record OfficialResultsDocument(
        RoundSummary Summary,
        DateTime VotingDate,
        string MunicipalityId,
        string MunicipalityName,
        OfficialTotals Totals,
        IReadOnlyList<OfficialContestantRow> Contestants,
        IReadOnlyList<OfficialBreakdownRow> Breakdown,
        IReadOnlyList<PartyElected> Elected,
        string DataFingerprint,
        DateTime GeneratedAtUtc)
    {
        public bool IsNational => MunicipalityId == null;
        /// <summary>Only a certified round is final; anything earlier is provisional and can change.</summary>
        public bool IsFinal => Summary.RoundStatus == ElectionStatus.Certified;
    }

    public class OfficialResultsService
    {
        private readonly IAppDbContext db;
        private readonly ElectionResultsService results;
        private readonly PublicResultsService publicResults;
        private readonly PartyListService lists;

        public OfficialResultsService(IAppDbContext db, ElectionResultsService results, PublicResultsService publicResults, PartyListService lists)
        {
            this.db = db;
            this.results = results;
            this.publicResults = publicResults;
            this.lists = lists;
        }

        /// <summary>SHA-256 (lower-case hex) of the CSV file exactly as it is downloaded.</summary>
        public static string Fingerprint(byte[] file) => Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();

        /// <summary>
        /// The national record (<paramref name="municipalityId"/> null) or a municipal one. Null when the
        /// round or the municipality does not exist. Only approved and locked sheets are counted.
        /// </summary>
        public async Task<OfficialResultsDocument> GetAsync(Guid roundId, string municipalityId = null, CancellationToken ct = default)
        {
            var summary = await results.GetSummaryAsync(roundId, ResultSheetStatus.Approved, ct);
            var breakdown = await publicResults.GetBreakdownAsync(roundId, ct);
            if (summary == null || breakdown == null) return null;

            var municipality = municipalityId == null ? null : breakdown.Municipalities.FirstOrDefault(m => m.MunicipalityId == municipalityId);
            if (municipalityId != null && municipality == null) return null;

            var votingDate = await db.ElectionRounds.AsNoTracking().Where(r => r.Id == roundId).Select(r => r.VotingDate).FirstAsync(ct);
            var ballot = breakdown.Ballot;

            // Station figures for the scope (the national record reads every municipality).
            var scope = municipality == null ? breakdown.Municipalities : new[] { municipality };
            var stationsByMunicipality = new List<(MunicipalityResult Municipality, IReadOnlyList<StationResult> Stations)>();
            foreach (var m in scope)
            {
                stationsByMunicipality.Add((m, await publicResults.GetStationsAsync(roundId, m.MunicipalityId, ct)));
            }
            var allStations = stationsByMunicipality.SelectMany(x => x.Stations).ToList();
            var counted = allStations.Where(s => s.IsCounted).ToList();

            var votes = ballot.ToDictionary(c => c.Id, c => counted.Sum(s => s.Votes.TryGetValue(c.Id, out var v) ? (long)v : 0));
            var totals = new OfficialTotals(
                allStations.Count,
                counted.Count,
                counted.Sum(s => (long)(s.RegisteredVoters ?? 0)),
                counted.Sum(s => (long)(s.BallotsUsed ?? 0)),
                votes.Values.Sum(),
                counted.Sum(s => (long)s.BlankVotes),
                counted.Sum(s => (long)s.InvalidVotes),
                counted.Where(s => s.RegisteredVoters > 0).Sum(s => (long)(s.BallotsUsed ?? 0)));

            // Seats and the threshold are national; a municipal record shows votes only.
            var national = summary.Contestants.ToDictionary(c => c.ContestantId);
            var allocation = summary.Parliamentary?.Parties.ToDictionary(p => p.ContestantId);
            var contestants = ballot
                .Select(c => new OfficialContestantRow(
                    c.Id, c.BallotPosition, c.Name, c.Acronym, votes[c.Id],
                    totals.ValidVotes == 0 ? 0 : Math.Round(votes[c.Id] * 100m / totals.ValidVotes, 2),
                    municipality == null && allocation != null && allocation.TryGetValue(c.Id, out var a) ? a.MeetsThreshold : null,
                    municipality == null && national.TryGetValue(c.Id, out var n) ? n.Seats : null))
                .OrderByDescending(c => c.Votes).ThenBy(c => c.BallotPosition)
                .ToList();

            var rows = municipality == null
                ? stationsByMunicipality.Select(x => Row(x.Municipality.Name, null, x.Stations, null)).ToList()
                : stationsByMunicipality[0].Stations.Select(s => Row(s.Station, s.AdministrativePost + " · " + s.Village, new[] { s }, s.Status)).ToList();

            IReadOnlyList<PartyElected> elected = null;
            if (municipality == null && summary.Parliamentary != null && summary.SheetsCounted > 0)
            {
                elected = await lists.GetElectedForRoundAsync(roundId,
                    summary.Contestants.Select(c => (c.ContestantId, c.Name, c.Seats ?? 0)).ToList(), ct);
            }

            var csv = await publicResults.ExportCountedSheetsFileAsync(roundId, ct);
            return new OfficialResultsDocument(summary, votingDate, municipality?.MunicipalityId, municipality?.Name, totals, contestants, rows,
                elected, Fingerprint(csv), DateTime.UtcNow);

            OfficialBreakdownRow Row(string name, string detail, IEnumerable<StationResult> stations, ResultSheetStatus? status)
            {
                var list = stations.ToList();
                var done = list.Where(s => s.IsCounted).ToList();
                return new OfficialBreakdownRow(name, detail, list.Count, done.Count, status,
                    done.Sum(s => (long)(s.RegisteredVoters ?? 0)),
                    done.Sum(s => (long)(s.BallotsUsed ?? 0)),
                    done.Sum(s => (long)s.BlankVotes),
                    done.Sum(s => (long)s.InvalidVotes),
                    ballot.ToDictionary(c => c.Id, c => done.Sum(s => s.Votes.TryGetValue(c.Id, out var v) ? (long)v : 0)));
            }
        }
    }
}
