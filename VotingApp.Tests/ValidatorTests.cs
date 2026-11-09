using System;
using System.Collections.Generic;
using System.Linq;
using VotingApp.Domain.Elections;
using VotingApp.Domain.Elections.Rules;

namespace VotingApp.Tests
{
    public class ResultSheetValidatorTests
    {
        private readonly ResultSheetValidator validator = new();
        private static readonly Guid A = Guid.NewGuid();
        private static readonly Guid B = Guid.NewGuid();

        private static ResultSheetFigures ConsistentSheet() => new()
        {
            RegisteredVoters = 500,
            BallotsReceived = 520,
            BallotsUsed = 400,
            BallotsSpoiled = 5,
            BallotsUnused = 115,
            BlankVotes = 6,
            InvalidVotes = 4,
            ContestantVotes = new Dictionary<Guid, int> { [A] = 250, [B] = 140 },
        };

        private static IEnumerable<string> Codes(ResultSheetValidation v) => v.Issues.Select(i => i.Code);

        [Fact]
        public void Consistent_sheet_has_no_issues()
        {
            var v = validator.Validate(ConsistentSheet(), new[] { A, B });
            Assert.Empty(v.Issues);
        }

        [Fact]
        public void Votes_must_add_up_to_ballots_used()
        {
            var v = validator.Validate(ConsistentSheet() with { BallotsUsed = 401, BallotsUnused = 114 });
            Assert.True(v.HasErrors);
            Assert.Contains(ResultSheetValidator.VotesDoNotMatchBallotsUsed, Codes(v));
        }

        [Fact]
        public void Ballots_must_reconcile()
        {
            var v = validator.Validate(ConsistentSheet() with { BallotsUnused = 100 });
            Assert.Contains(ResultSheetValidator.BallotsDoNotReconcile, Codes(v));
        }

        [Fact]
        public void Missing_ballot_figures_are_errors()
        {
            var v = validator.Validate(ConsistentSheet() with { BallotsReceived = null, BallotsSpoiled = null });
            Assert.Equal(2, v.Issues.Count(i => i.Code == ResultSheetValidator.MissingFigure));
        }

        [Fact]
        public void Missing_contestant_on_ballot_is_an_error()
        {
            var sheet = ConsistentSheet() with { ContestantVotes = new Dictionary<Guid, int> { [A] = 390 } };
            var v = validator.Validate(sheet, new[] { A, B });
            Assert.Contains(ResultSheetValidator.MissingFigure, Codes(v));
        }

        [Fact]
        public void Turnout_above_registered_is_a_warning_not_an_error()
        {
            var v = validator.Validate(ConsistentSheet() with { RegisteredVoters = 350 });
            Assert.False(v.HasErrors);
            Assert.Contains(ResultSheetValidator.TurnoutAboveRegistered, Codes(v));
        }

        [Fact]
        public void Very_high_turnout_is_flagged()
        {
            var v = validator.Validate(ConsistentSheet() with { RegisteredVoters = 410 });
            Assert.Contains(ResultSheetValidator.VeryHighTurnout, Codes(v));
        }

        [Fact]
        public void One_contestant_with_every_valid_vote_is_flagged()
        {
            var sheet = ConsistentSheet() with { ContestantVotes = new Dictionary<Guid, int> { [A] = 390, [B] = 0 } };
            var v = validator.Validate(sheet);
            Assert.False(v.HasErrors);
            Assert.Contains(ResultSheetValidator.SingleContestantTakesAll, Codes(v));
        }

        [Fact]
        public void Negative_figures_are_errors()
        {
            var sheet = ConsistentSheet() with { ContestantVotes = new Dictionary<Guid, int> { [A] = 400, [B] = -10 } };
            Assert.Contains(ResultSheetValidator.NegativeFigure, Codes(validator.Validate(sheet)));
        }
    }

    public class PartyListValidatorTests
    {
        private readonly PartyListValidator validator = new();

        private static IEnumerable<ListPosition> List(params Gender[] genders) =>
            genders.Select((g, i) => new ListPosition(i + 1, g));

        [Fact]
        public void Woman_in_every_block_of_three_passes()
        {
            var issues = validator.Validate(List(Gender.Male, Gender.Female, Gender.Male, Gender.Male, Gender.Male, Gender.Female, Gender.Male));
            Assert.Empty(issues);
        }

        [Fact]
        public void Block_without_a_woman_fails()
        {
            var issues = validator.Validate(List(Gender.Female, Gender.Male, Gender.Male, Gender.Male, Gender.Male, Gender.Male));
            var issue = Assert.Single(issues);
            Assert.Equal("GENDER_QUOTA", issue.Code);
            Assert.Contains("4-6", issue.Message);
        }

        [Fact]
        public void Gaps_in_positions_fail()
        {
            var issues = validator.Validate(new[] { new ListPosition(1, Gender.Female), new ListPosition(3, Gender.Male) });
            Assert.Equal("LIST_POSITIONS", Assert.Single(issues).Code);
        }
    }
}
