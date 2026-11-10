using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VotingApp.Application.Abstractions;
using VotingApp.Domain.Elections;

namespace VotingApp.Application.Elections
{
    public sealed record PreviousFigure(decimal PercentOfValid, int? Seats);

    /// <summary>The previous election of the same type, matched contestant by contestant.</summary>
    public sealed record ResultsComparison(Guid PreviousRoundId, string PreviousElectionName, DateTime PreviousDate,
        IReadOnlyDictionary<Guid, PreviousFigure> ByContestant)
    {
        public PreviousFigure For(Guid contestantId) => ByContestant.TryGetValue(contestantId, out var f) ? f : null;
    }

    /// <summary>
    /// Compares a round with the same round of the previous election of the same type: parties are
    /// matched through their lasting party record, presidential candidates through their profile.
    /// Contestants without such a link, or new in this election, have no comparison.
    /// </summary>
    public class ResultsComparisonService
    {
        private readonly IAppDbContext db;
        private readonly ElectionResultsService results;
        private readonly RoundResultsCache cache;

        public ResultsComparisonService(IAppDbContext db, ElectionResultsService results, RoundResultsCache cache)
        {
            this.db = db;
            this.results = results;
            this.cache = cache;
        }

        public Task<ResultsComparison> GetAsync(Guid roundId, CancellationToken ct = default) =>
            cache.GetOrBuildAsync(roundId, "comparison", () => BuildAsync(roundId, CancellationToken.None));

        private async Task<ResultsComparison> BuildAsync(Guid roundId, CancellationToken ct)
        {
            var round = await db.ElectionRounds.AsNoTracking().Include(r => r.Election).FirstOrDefaultAsync(r => r.Id == roundId, ct);
            if (round == null) return null;

            var previousRound = await db.ElectionRounds.AsNoTracking()
                .Where(r => r.Election.Type == round.Election.Type
                    && r.Election.ElectionDate < round.Election.ElectionDate
                    && r.Number == round.Number
                    && r.Status >= ElectionStatus.Counting)
                .OrderByDescending(r => r.Election.ElectionDate)
                .Select(r => new { r.Id, r.Election.Name, r.Election.ElectionDate })
                .FirstOrDefaultAsync(ct);
            if (previousRound == null) return null;

            var previous = await results.GetSummaryAsync(previousRound.Id, ResultSheetStatus.Approved, ct);
            if (previous == null || previous.SheetsCounted == 0) return null;

            var parliamentary = round.Election.Type == ElectionType.Parliamentary;
            async Task<Dictionary<Guid, Guid>> KeysAsync(Guid id) =>
                (await db.RoundContestants.AsNoTracking().Where(rc => rc.ElectionRoundId == id)
                    .Select(rc => new { rc.ContestantId, Key = parliamentary ? rc.Contestant.PartyId : rc.Contestant.CandidateId })
                    .ToListAsync(ct))
                .Where(x => x.Key.HasValue)
                .ToDictionary(x => x.ContestantId, x => x.Key.Value);

            var currentKeys = await KeysAsync(roundId);
            var previousByKey = (await KeysAsync(previousRound.Id))
                .Join(previous.Contestants, k => k.Key, c => c.ContestantId, (k, c) => (Key: k.Value, Figure: new PreviousFigure(c.PercentOfValid, c.Seats)))
                .GroupBy(x => x.Key)
                .ToDictionary(g => g.Key, g => g.First().Figure);

            var matched = currentKeys
                .Where(k => previousByKey.ContainsKey(k.Value))
                .ToDictionary(k => k.Key, k => previousByKey[k.Value]);

            return new ResultsComparison(previousRound.Id, previousRound.Name, previousRound.ElectionDate, matched);
        }
    }
}
