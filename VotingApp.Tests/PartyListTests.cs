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
        private static async Task<(Fixture F, Guid ElectionId, Guid PartyA, Guid PartyB)> ParliamentaryAsync()
        {
            var f = new Fixture();
            f.User.IsAdmin = true;
            var election = (await f.Admin.CreateAsync(new CreateElectionInput("Parliament 2028", ElectionType.Parliamentary, new DateTime(2028, 5, 1), 6, 4m))).Value;
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
    }
}
