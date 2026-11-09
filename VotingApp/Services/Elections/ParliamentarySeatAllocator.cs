using System;
using System.Collections.Generic;
using System.Linq;

namespace VotingApp.Services.Elections
{
    public sealed record PartySeatAllocation(
        Guid ContestantId,
        long Votes,
        decimal PercentOfValid,
        bool MeetsThreshold,
        int Seats);

    public sealed record ParliamentaryResult(
        int TotalSeats,
        decimal ThresholdPercent,
        long TotalValidVotes,
        IReadOnlyList<PartySeatAllocation> Parties,
        // Party that won the last seat allocated, if any.
        Guid? LastSeatWinnerId,
        // Party with the next highest quotient: the first to gain a seat if votes shift.
        Guid? NextInLineId,
        // True when the last seat was decided between equal quotients (tie-break applied).
        bool LastSeatDecidedByTie);

    /// <summary>
    /// Proportional representation with the D'Hondt highest-averages method and a legal threshold
    /// on the share of valid votes. Defaults follow the Timor-Leste National Parliament:
    /// 65 seats, one national constituency, 4% threshold. Check these against the law in force
    /// for each election and configure them on the Election record.
    /// </summary>
    public class ParliamentarySeatAllocator
    {
        public const int DefaultSeats = 65;
        public const decimal DefaultThresholdPercent = 4m;

        /// <param name="votes">Valid votes per party/coalition.</param>
        /// <param name="totalValidVotes">
        /// Valid votes used for the threshold. Defaults to the sum of <paramref name="votes"/>;
        /// pass it explicitly if some valid votes are not in the list.
        /// </param>
        /// <remarks>
        /// Ties for the last seat go to the list with fewer total votes (the Portuguese-tradition rule);
        /// if those are also equal, the result is flagged and the order is arbitrary but deterministic.
        /// Confirm the tie-break rule in the applicable electoral law.
        /// </remarks>
        public ParliamentaryResult Allocate(
            IEnumerable<ContestantVotes> votes,
            int seats = DefaultSeats,
            decimal thresholdPercent = DefaultThresholdPercent,
            long? totalValidVotes = null)
        {
            if (votes == null) throw new ArgumentNullException(nameof(votes));
            if (seats <= 0) throw new ArgumentOutOfRangeException(nameof(seats));
            if (thresholdPercent < 0 || thresholdPercent >= 100) throw new ArgumentOutOfRangeException(nameof(thresholdPercent));

            var list = votes.ToList();
            if (list.Any(v => v.Votes < 0)) throw new ArgumentException("Vote counts cannot be negative.", nameof(votes));
            if (list.Select(v => v.ContestantId).Distinct().Count() != list.Count)
                throw new ArgumentException("Each contestant may appear only once.", nameof(votes));

            long totalValid = totalValidVotes ?? list.Sum(v => v.Votes);
            if (totalValid < list.Sum(v => v.Votes))
                throw new ArgumentException("Total valid votes cannot be less than the sum of party votes.", nameof(totalValidVotes));

            // votes / totalValid >= threshold / 100, compared in decimal to avoid rounding at the boundary.
            bool Meets(long v) => totalValid > 0 && v > 0 && v * 100m >= thresholdPercent * totalValid;

            var eligible = list.Where(v => Meets(v.Votes)).ToList();
            var seatCount = list.ToDictionary(v => v.ContestantId, _ => 0);

            Guid? lastWinner = null;
            bool lastByTie = false;

            if (eligible.Count > 0)
            {
                for (int seat = 1; seat <= seats; seat++)
                {
                    var best = eligible
                        .OrderByDescending(p => p, new QuotientComparer(seatCount))
                        .ThenBy(p => p.Votes)            // tie: fewer total votes wins the seat
                        .ThenBy(p => p.ContestantId)     // fully equal: deterministic order, flagged below
                        .ToList();

                    var winner = best[0];
                    if (seat == seats)
                    {
                        lastWinner = winner.ContestantId;
                        lastByTie = best.Count > 1 && QuotientComparer.Compare(winner, best[1], seatCount) == 0;
                    }
                    seatCount[winner.ContestantId]++;
                }
            }

            Guid? nextInLine = eligible.Count == 0 ? null : eligible
                .OrderByDescending(p => p, new QuotientComparer(seatCount))
                .ThenBy(p => p.Votes)
                .ThenBy(p => p.ContestantId)
                .First().ContestantId;

            var parties = list
                .OrderByDescending(v => v.Votes)
                .Select(v => new PartySeatAllocation(
                    v.ContestantId,
                    v.Votes,
                    totalValid == 0 ? 0m : Math.Round(v.Votes * 100m / totalValid, 2),
                    Meets(v.Votes),
                    seatCount[v.ContestantId]))
                .ToList();

            return new ParliamentaryResult(seats, thresholdPercent, totalValid, parties, lastWinner, nextInLine, lastByTie);
        }

        /// <summary>Compares D'Hondt quotients votes/(seats+1) exactly, by cross-multiplication.</summary>
        private sealed class QuotientComparer : IComparer<ContestantVotes>
        {
            private readonly IReadOnlyDictionary<Guid, int> seats;
            public QuotientComparer(IReadOnlyDictionary<Guid, int> seats) => this.seats = seats;

            public int Compare(ContestantVotes x, ContestantVotes y) => Compare(x, y, seats);

            public static int Compare(ContestantVotes x, ContestantVotes y, IReadOnlyDictionary<Guid, int> seats)
            {
                // x.Votes / (sx + 1) vs y.Votes / (sy + 1)  <=>  x.Votes * (sy + 1) vs y.Votes * (sx + 1)
                decimal left = (decimal)x.Votes * (seats[y.ContestantId] + 1);
                decimal right = (decimal)y.Votes * (seats[x.ContestantId] + 1);
                return left.CompareTo(right);
            }
        }
    }
}
