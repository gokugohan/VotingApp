using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VotingApp.Application.Common;
using VotingApp.Application.Elections;
using VotingApp.Domain.Elections;

namespace VotingApp.Tests
{
    public class PartyListTests
    {
        private static async Task<(Fixture F, Guid ElectionId, Guid PartyA, Guid PartyB)> ParliamentaryAsync(DateTime? date = null)
        {
            var f = new Fixture();
            f.User.IsAdmin = true;
            var election = (await f.Admin.CreateAsync(new CreateElectionInput("Parliament 2028", ElectionType.Parliamentary, date ?? new DateTime(2028, 5, 1), 6, 4m))).Value;
            var a = (await f.Admin.AddContestantAsync(election, new AddContestantInput(ContestantType.Party, "Party A", "PA", null))).Value;
            var b = (await f.Admin.AddContestantAsync(election, new AddContestantInput(ContestantType.Party, "Party B", "PB", null))).Value;
            return (f, election, a, b);
        }

        private static List<ListEntry> People(string genders) =>
            genders.Select((g, i) => new ListEntry($"Person {i + 1}", g == 'F' ? Gender.Female : Gender.Male)).ToList();

        [Fact]
        public void Parse_accepts_common_formats_and_reports_line_numbers()
        {
            var (entries, errors) = PartyListService.Parse("Maria da Costa, F\r\n\nJoão Pereira; m\nAna\tFemale\nNo gender here\nBad, X\n , F");

            Assert.Equal(new[] { "Maria da Costa", "João Pereira", "Ana" }, entries.Select(e => e.FullName));
            Assert.Equal(new[] { Gender.Female, Gender.Male, Gender.Female }, entries.Select(e => e.Gender));
            Assert.Equal(3, errors.Count);
            Assert.StartsWith("Line 5:", errors[0]);
            Assert.StartsWith("Line 6:", errors[1]);
            Assert.StartsWith("Line 7:", errors[2]);
        }

        [Fact]
        public void Format_round_trips_with_parse()
        {
            var people = new[]
            {
                new PartyListCandidate { Position = 2, FullName = "B", Gender = Gender.Male },
                new PartyListCandidate { Position = 1, FullName = "A", Gender = Gender.Female },
            };
            var (entries, errors) = PartyListService.Parse(PartyListService.Format(people));
            Assert.Empty(errors);
            Assert.Equal(new[] { "A", "B" }, entries.Select(e => e.FullName));
        }

        [Fact]
        public async Task Parliamentary_election_cannot_open_until_every_list_meets_the_rules()
        {
            var (f, election, a, b) = await ParliamentaryAsync();
            using var _ = f;

            var noLists = await f.Admin.SetStatusAsync(election, ElectionStatus.Voting);
            Assert.Equal(FailureKind.Validation, noLists.Failure);
            Assert.Contains("Party A", noLists.Error);

            // Party A meets the rule; Party B has no woman in positions 1-3.
            await f.Lists.SaveAsync(a, People("FMMMFM"), People("F"));
            var draft = await f.Lists.SaveAsync(b, People("MMMFMM"), new List<ListEntry>());
            Assert.True(draft.Succeeded); // drafts can be saved...
            Assert.Contains(draft.Value, i => i.Code == "GENDER_QUOTA");
            var blocked = await f.Admin.SetStatusAsync(election, ElectionStatus.Voting);
            Assert.Equal(FailureKind.Validation, blocked.Failure); // ...but block opening
            Assert.Contains("Party B", blocked.Error);
            Assert.DoesNotContain("Party A (", blocked.Error);

            await f.Lists.SaveAsync(b, People("MFMMMF"), new List<ListEntry>());
            Assert.True((await f.Admin.SetStatusAsync(election, ElectionStatus.Voting)).Succeeded);

            // Lists are fixed once voting starts.
            var late = await f.Lists.SaveAsync(a, People("F"), new List<ListEntry>());
            Assert.Equal(FailureKind.Conflict, late.Failure);
        }

        [Fact]
        public async Task Saving_replaces_the_list_and_renumbers_positions()
        {
            var (f, _, a, _) = await ParliamentaryAsync();
            using var __ = f;
            await f.Lists.SaveAsync(a, People("FMMFMM"), People("FM"));
            await f.Lists.SaveAsync(a, People("MFM"), People("F"));

            var list = await f.Lists.GetAsync(a);
            Assert.Equal(new[] { 1, 2, 3 }, list.Effective.Select(c => c.Position));
            Assert.Equal(new[] { 1 }, list.Substitutes.Select(c => c.Position));
            Assert.Equal(4, await f.Db.PartyListCandidates.CountAsync(c => c.ContestantId == a));
            Assert.Contains(list.Issues, i => i.Code == "LIST_SHORT"); // 3 candidates for 6 seats: warning, not error
            Assert.True(list.IsValid);
        }

        [Fact]
        public async Task Elected_members_come_from_the_top_of_the_effective_list()
        {
            var (f, _, a, b) = await ParliamentaryAsync();
            using var __ = f;
            await f.Lists.SaveAsync(a, People("FMMFMM"), People("FF"));
            await f.Lists.SaveAsync(b, People("MF"), new List<ListEntry>());

            var elected = await f.Lists.GetElectedAsync(new[] { (a, "Party A", 3), (b, "Party B", 3) });

            var pa = elected.Single(e => e.ContestantId == a);
            Assert.Equal(new[] { 1, 2, 3 }, pa.Members.Select(m => m.Position));
            Assert.Equal(0, pa.Unfilled);
            var pb = elected.Single(e => e.ContestantId == b);
            Assert.Equal(2, pb.Members.Count);
            Assert.Equal(1, pb.Unfilled); // won 3 seats with only 2 names on the list
        }

        private static List<PartyListCandidate> ListOf(int effective, int substitutes) =>
            Enumerable.Range(1, effective).Select(i => new PartyListCandidate { Id = Guid.NewGuid(), Position = i, FullName = "E" + i, Gender = Gender.Female })
                .Concat(Enumerable.Range(1, substitutes).Select(i => new PartyListCandidate { Id = Guid.NewGuid(), Position = i, FullName = "S" + i, Gender = Gender.Male, IsSubstitute = true }))
                .ToList();

        private static void StepDown(List<PartyListCandidate> list, string name, int day = 1) =>
            list.Single(c => c.FullName == name).SteppedDownOn = new DateTime(2028, 7, day);

        [Fact]
        public void A_vacated_seat_goes_to_the_next_unelected_candidate_then_to_substitutes()
        {
            var list = ListOf(5, 2);
            StepDown(list, "E2");
            var holders = PartyListService.SeatHolders(list, 3);
            Assert.Equal(new[] { "E1", "E4", "E3" }, holders.Select(h => h.FullName));
            Assert.Equal("E2", holders[1].Replaces);
            Assert.Null(holders[0].Replaces);

            // The next in line also stepped down: skip them.
            StepDown(list, "E4", 2);
            Assert.Equal(new[] { "E1", "E5", "E3" }, PartyListService.SeatHolders(list, 3).Select(h => h.FullName));

            // Effective list used up: substitutes take over, in order. E5 keeps the seat they took first.
            StepDown(list, "E1", 3);
            holders = PartyListService.SeatHolders(list, 3);
            Assert.Equal(new[] { "S1", "E5", "E3" }, holders.Select(h => h.FullName));
            Assert.True(holders[0].IsSubstitute);
            Assert.Equal("E1", holders[0].Replaces);
        }

        [Fact]
        public void Seats_stay_empty_when_nobody_is_left_on_the_list()
        {
            var list = ListOf(2, 1);
            StepDown(list, "E1");
            StepDown(list, "S1");
            var holders = PartyListService.SeatHolders(list, 2);
            Assert.Equal(new[] { "E2" }, holders.Select(h => h.FullName));
        }

        [Fact]
        public async Task Stepping_down_is_recorded_after_results_and_can_be_undone()
        {
            var (f, election, a, b) = await ParliamentaryAsync(new DateTime(2023, 5, 21));
            using var __ = f;
            await f.Lists.SaveAsync(a, People("FMMFMM"), People("FM"));
            await f.Lists.SaveAsync(b, People("MFMMMF"), new List<ListEntry>());
            var second = (await f.Lists.GetAsync(a)).Effective[1].Id;
            var date = new DateTime(2023, 7, 1);

            var tooEarly = await f.Lists.StepDownAsync(new StepDownInput(second, date, StepDownReason.JoinedGovernment, null));
            Assert.Equal(FailureKind.Conflict, tooEarly.Failure);

            var opened = await f.Admin.SetStatusAsync(election, ElectionStatus.Provisional);
            Assert.True(opened.Succeeded, opened.Error);
            var beforeElection = await f.Lists.StepDownAsync(new StepDownInput(second, new DateTime(2023, 4, 1), StepDownReason.Resigned, null));
            Assert.Equal(FailureKind.Validation, beforeElection.Failure);
            var future = await f.Lists.StepDownAsync(new StepDownInput(second, DateTime.UtcNow.Date.AddDays(10), StepDownReason.Resigned, null));
            Assert.Equal(FailureKind.Validation, future.Failure);

            var ok = await f.Lists.StepDownAsync(new StepDownInput(second, date, StepDownReason.JoinedGovernment, "  Minister of Finance  "));
            Assert.True(ok.Succeeded);
            var stored = await f.Db.PartyListCandidates.SingleAsync(c => c.Id == second);
            Assert.Equal(StepDownReason.JoinedGovernment, stored.StepDownReason);
            Assert.Equal("Minister of Finance", stored.StepDownNote);
            Assert.Equal(FailureKind.Conflict, (await f.Lists.StepDownAsync(new StepDownInput(second, date, StepDownReason.Resigned, null))).Failure);

            var elected = (await f.Lists.GetElectedAsync(new[] { (a, "Party A", 3) })).Single();
            Assert.Equal(new[] { "Person 1", "Person 4", "Person 3" }, elected.Members.Select(m => m.FullName));
            Assert.Equal("Person 2", elected.Members[1].Replaces);

            Assert.True((await f.Lists.ReinstateAsync(second)).Succeeded);
            elected = (await f.Lists.GetElectedAsync(new[] { (a, "Party A", 3) })).Single();
            Assert.Equal(new[] { "Person 1", "Person 2", "Person 3" }, elected.Members.Select(m => m.FullName));
            Assert.Equal(FailureKind.Conflict, (await f.Lists.ReinstateAsync(second)).Failure);
        }
    }
}
