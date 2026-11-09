using System;
using VotingApp.Services.Elections;

namespace VotingApp.Tests
{
    public class PresidentialResultCalculatorTests
    {
        private readonly PresidentialResultCalculator calculator = new();

        private static readonly Guid A = Guid.NewGuid();
        private static readonly Guid B = Guid.NewGuid();
        private static readonly Guid C = Guid.NewGuid();
        private static readonly Guid D = Guid.NewGuid();

        [Fact]
        public void Candidate_with_more_than_half_of_valid_votes_is_elected_in_first_round()
        {
            var r = calculator.Calculate(new[]
            {
                new ContestantVotes(A, 501), new ContestantVotes(B, 300), new ContestantVotes(C, 199),
            });

            Assert.Equal(PresidentialOutcome.Elected, r.Outcome);
            Assert.Equal(A, r.WinnerId);
            Assert.Equal(1000, r.TotalValidVotes);
            Assert.Equal(501, r.MajorityThreshold);
        }

        [Fact]
        public void Exactly_half_is_not_a_majority()
        {
            var r = calculator.Calculate(new[]
            {
                new ContestantVotes(A, 500), new ContestantVotes(B, 300), new ContestantVotes(C, 200),
            });

            Assert.Equal(PresidentialOutcome.Runoff, r.Outcome);
            Assert.Null(r.WinnerId);
            Assert.Equal(new[] { A, B }, r.RunoffContestantIds);
        }

        [Fact]
        public void Top_two_go_to_runoff()
        {
            var r = calculator.Calculate(new[]
            {
                new ContestantVotes(C, 150), new ContestantVotes(A, 460),
                new ContestantVotes(D, 20), new ContestantVotes(B, 370),
            });

            Assert.Equal(PresidentialOutcome.Runoff, r.Outcome);
            Assert.Equal(new[] { A, B }, r.RunoffContestantIds);
            Assert.Equal(46.0m, r.Standings[0].PercentOfValid);
        }

        [Fact]
        public void Tie_for_second_place_needs_resolution()
        {
            var r = calculator.Calculate(new[]
            {
                new ContestantVotes(A, 400), new ContestantVotes(B, 300), new ContestantVotes(C, 300),
            });

            Assert.Equal(PresidentialOutcome.TieRequiresResolution, r.Outcome);
            Assert.Equal(2, r.TiedContestantIds.Count);
            Assert.Contains(B, r.TiedContestantIds);
            Assert.Contains(C, r.TiedContestantIds);
        }

        [Fact]
        public void Two_tied_leaders_both_reach_runoff()
        {
            var r = calculator.Calculate(new[]
            {
                new ContestantVotes(A, 400), new ContestantVotes(B, 400), new ContestantVotes(C, 200),
            });

            Assert.Equal(PresidentialOutcome.Runoff, r.Outcome);
            Assert.Equal(2, r.RunoffContestantIds.Count);
            Assert.Equal(1, r.Standings[0].Rank);
            Assert.Equal(1, r.Standings[1].Rank);
            Assert.Equal(3, r.Standings[2].Rank);
        }

        [Fact]
        public void Runoff_round_is_won_by_plurality()
        {
            var r = calculator.Calculate(new[] { new ContestantVotes(A, 510), new ContestantVotes(B, 490) }, isRunoffRound: true);
            Assert.Equal(PresidentialOutcome.Elected, r.Outcome);
            Assert.Equal(A, r.WinnerId);
        }

        [Fact]
        public void Tied_runoff_needs_resolution()
        {
            var r = calculator.Calculate(new[] { new ContestantVotes(A, 500), new ContestantVotes(B, 500) }, isRunoffRound: true);
            Assert.Equal(PresidentialOutcome.TieRequiresResolution, r.Outcome);
        }

        [Fact]
        public void No_votes_yet()
        {
            var r = calculator.Calculate(new[] { new ContestantVotes(A, 0), new ContestantVotes(B, 0) });
            Assert.Equal(PresidentialOutcome.NoVotes, r.Outcome);
        }
    }
}
