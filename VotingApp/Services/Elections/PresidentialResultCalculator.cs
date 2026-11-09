using System;
using System.Collections.Generic;
using System.Linq;

namespace VotingApp.Services.Elections
{
    public enum PresidentialOutcome
    {
        /// <summary>No valid votes counted yet.</summary>
        NoVotes,
        /// <summary>A candidate has more than half of the valid votes.</summary>
        Elected,
        /// <summary>Nobody has a majority: the top two go to a second round.</summary>
        Runoff,
        /// <summary>A tie decides who wins or who qualifies for the runoff; needs resolution under the law.</summary>
        TieRequiresResolution,
    }

    public sealed record ContestantVotes(Guid ContestantId, long Votes);

    public sealed record PresidentialStanding(Guid ContestantId, long Votes, decimal PercentOfValid, int Rank);

    public sealed record PresidentialResult(
        PresidentialOutcome Outcome,
        long TotalValidVotes,
        long MajorityThreshold,
        IReadOnlyList<PresidentialStanding> Standings,
        Guid? WinnerId,
        IReadOnlyList<Guid> RunoffContestantIds,
        IReadOnlyList<Guid> TiedContestantIds);

    /// <summary>
    /// Two-round presidential system: a candidate is elected with more than half of the valid votes
    /// (blank and invalid ballots are not valid votes). Otherwise the two most voted candidates
    /// contest a second round, which is won by simple plurality.
    /// </summary>
    public class PresidentialResultCalculator
    {
        public PresidentialResult Calculate(IEnumerable<ContestantVotes> votes, bool isRunoffRound = false)
        {
            if (votes == null) throw new ArgumentNullException(nameof(votes));
            var list = votes.ToList();
            if (list.Any(v => v.Votes < 0)) throw new ArgumentException("Vote counts cannot be negative.", nameof(votes));
            if (list.Select(v => v.ContestantId).Distinct().Count() != list.Count)
                throw new ArgumentException("Each contestant may appear only once.", nameof(votes));

            long totalValid = list.Sum(v => v.Votes);
            // "More than half": the smallest integer strictly greater than totalValid / 2.
            long majority = totalValid / 2 + 1;

            var ordered = list.OrderByDescending(v => v.Votes).ToList();
            var standings = ordered
                .Select(v => new PresidentialStanding(
                    v.ContestantId,
                    v.Votes,
                    totalValid == 0 ? 0m : Math.Round(v.Votes * 100m / totalValid, 2),
                    // Competition ranking: tied candidates share a rank.
                    ordered.Count(o => o.Votes > v.Votes) + 1))
                .ToList();

            var none = Array.Empty<Guid>();
            if (totalValid == 0)
            {
                return new PresidentialResult(PresidentialOutcome.NoVotes, 0, majority, standings, null, none, none);
            }

            var leader = ordered[0];
            var tiedForFirst = ordered.Where(v => v.Votes == leader.Votes).Select(v => v.ContestantId).ToList();

            if (isRunoffRound)
            {
                // Second round: plurality decides.
                return tiedForFirst.Count > 1
                    ? new PresidentialResult(PresidentialOutcome.TieRequiresResolution, totalValid, majority, standings, null, none, tiedForFirst)
                    : new PresidentialResult(PresidentialOutcome.Elected, totalValid, majority, standings, leader.ContestantId, none, none);
            }

            if (leader.Votes >= majority)
            {
                return new PresidentialResult(PresidentialOutcome.Elected, totalValid, majority, standings, leader.ContestantId, none, none);
            }

            if (ordered.Count < 2)
            {
                // A sole candidate without a majority of valid votes cannot happen (100% > 50%), kept for safety.
                return new PresidentialResult(PresidentialOutcome.TieRequiresResolution, totalValid, majority, standings, null, none, tiedForFirst);
            }

            // Who reaches the runoff? The two best placed, unless a tie makes that ambiguous.
            var secondVotes = ordered[1].Votes;
            var contendersForTwoPlaces = ordered.Where(v => v.Votes >= secondVotes).ToList();
            if (contendersForTwoPlaces.Count > 2)
            {
                var tiedAtCutoff = ordered.Where(v => v.Votes == secondVotes).Select(v => v.ContestantId).ToList();
                return new PresidentialResult(PresidentialOutcome.TieRequiresResolution, totalValid, majority, standings, null, none, tiedAtCutoff);
            }

            var runoff = contendersForTwoPlaces.Select(v => v.ContestantId).ToList();
            return new PresidentialResult(PresidentialOutcome.Runoff, totalValid, majority, standings, null, runoff, none);
        }
    }
}
