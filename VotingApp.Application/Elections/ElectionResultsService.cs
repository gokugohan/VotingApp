using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VotingApp.Application.Abstractions;
using VotingApp.Domain.Elections;
using VotingApp.Domain.Elections.Rules;

namespace VotingApp.Application.Elections
{
    public sealed record ContestantTotal(Guid ContestantId, string Name, string Acronym, long Votes, decimal PercentOfValid, int? Seats);

    public sealed record RoundSummary(
        Guid RoundId,
        Guid ElectionId,
        string ElectionName,
        ElectionType Type,
        int RoundNumber,
        ElectionStatus RoundStatus,
        ResultSheetStatus MinimumSheetStatus,
        int PollingStations,
        int SheetsCounted,
        IReadOnlyDictionary<string, int> SheetsByStatus,
        long ValidVotes,
        long BlankVotes,
        long InvalidVotes,
        IReadOnlyList<ContestantTotal> Contestants,
        PresidentialResult Presidential,
        ParliamentaryResult Parliamentary,
        DateTime GeneratedAtUtc)
    {
        public decimal ReportingPercent => PollingStations == 0 ? 0 : Math.Round(SheetsCounted * 100m / PollingStations, 1);
    }

    /// <summary>
    /// Aggregates result sheets of a round and applies the election's rules. Results are cached
    /// briefly so a busy public dashboard does not re-aggregate on every request; workflow changes
    /// that affect counted sheets invalidate the cache.
    /// </summary>
    public class ElectionResultsService
    {
        private readonly IAppDbContext db;
        private readonly RoundResultsCache cache;
        private readonly PresidentialResultCalculator presidential;
        private readonly ParliamentarySeatAllocator parliamentary;
        private readonly StationRosterService rosters;

        public ElectionResultsService(IAppDbContext db, RoundResultsCache cache,
            PresidentialResultCalculator presidential, ParliamentarySeatAllocator parliamentary, StationRosterService rosters)
        {
            this.rosters = rosters;
            this.db = db;
            this.cache = cache;
            this.presidential = presidential;
            this.parliamentary = parliamentary;
        }

        /// <summary>Marks everything cached for the round stale (summary, breakdowns, station lists, CSV).</summary>
        public void Invalidate(Guid roundId) => cache.Invalidate(roundId);

        /// <summary>
        /// Summary counting sheets at or beyond <paramref name="minimumStatus"/>
        /// (Approved = provisional official count; Entered = everything received so far).
        /// </summary>
        public async Task<RoundSummary> GetSummaryAsync(Guid roundId, ResultSheetStatus minimumStatus = ResultSheetStatus.Approved, CancellationToken ct = default)
        {
            // Built with CancellationToken.None: the result is shared with other waiting requests.
            return await cache.GetOrBuildAsync(roundId, "summary:" + minimumStatus,
                () => BuildAsync(roundId, minimumStatus, CancellationToken.None));
        }

        private async Task<RoundSummary> BuildAsync(Guid roundId, ResultSheetStatus minimumStatus, CancellationToken ct)
        {
            var round = await db.ElectionRounds.AsNoTracking()
                .Include(r => r.Election)
                .Include(r => r.Contestants).ThenInclude(rc => rc.Contestant)
                .FirstOrDefaultAsync(r => r.Id == roundId, ct);
            if (round == null) return null;

            var byStatus = await db.ResultSheets.AsNoTracking()
                .Where(s => s.ElectionRoundId == roundId)
                .GroupBy(s => s.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var counted = db.ResultSheets.AsNoTracking().Where(s => s.ElectionRoundId == roundId && s.Status >= minimumStatus);
            var sheetsCounted = await counted.CountAsync(ct);
            var blank = await counted.SumAsync(s => (long)s.BlankVotes, ct);
            var invalid = await counted.SumAsync(s => (long)s.InvalidVotes, ct);

            var votes = await db.ResultSheetEntries.AsNoTracking()
                .Where(e => e.ResultSheet.ElectionRoundId == roundId && e.ResultSheet.Status >= minimumStatus)
                .GroupBy(e => e.ContestantId)
                .Select(g => new { ContestantId = g.Key, Votes = g.Sum(e => (long)e.Votes) })
                .ToDictionaryAsync(x => x.ContestantId, x => x.Votes, ct);

            var ballot = round.Contestants.OrderBy(rc => rc.BallotPosition).Select(rc => rc.Contestant).ToList();
            var contestantVotes = ballot.Select(c => new ContestantVotes(c.Id, votes.TryGetValue(c.Id, out var v) ? v : 0)).ToList();
            long valid = contestantVotes.Sum(v => v.Votes);

            PresidentialResult presidentialResult = null;
            ParliamentaryResult parliamentaryResult = null;
            if (round.Election.Type == ElectionType.Presidential)
            {
                presidentialResult = presidential.Calculate(contestantVotes, isRunoffRound: round.Number > 1);
            }
            else
            {
                parliamentaryResult = parliamentary.Allocate(contestantVotes,
                    round.Election.TotalSeats ?? ParliamentarySeatAllocator.DefaultSeats,
                    round.Election.ThresholdPercent ?? ParliamentarySeatAllocator.DefaultThresholdPercent);
            }

            var seats = parliamentaryResult?.Parties.ToDictionary(p => p.ContestantId, p => p.Seats);
            var contestants = ballot
                .Select(c =>
                {
                    var v = votes.TryGetValue(c.Id, out var n) ? n : 0;
                    return new ContestantTotal(c.Id, c.Name, c.Acronym, v,
                        valid == 0 ? 0 : Math.Round(v * 100m / valid, 2),
                        seats != null && seats.TryGetValue(c.Id, out var s) ? s : (int?)null);
                })
                .OrderByDescending(c => c.Votes).ThenBy(c => c.Name)
                .ToList();

            return new RoundSummary(
                round.Id, round.ElectionId, round.Election.Name, round.Election.Type, round.Number, round.Status, minimumStatus,
                (await rosters.GetForRoundAsync(roundId, ct)).Stations.Count,
                sheetsCounted,
                byStatus.ToDictionary(x => x.Status.ToString(), x => x.Count),
                valid, blank, invalid,
                contestants, presidentialResult, parliamentaryResult, DateTime.UtcNow);
        }
    }
}
