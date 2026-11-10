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
    public class PartyRegistryTests
    {
        /// <summary>
        /// A parliamentary election with the given parties (name, votes at the Aileu station), opened,
        /// counted and approved. Returns the round id and contestant ids by name.
        /// </summary>
        private static async Task<(Guid RoundId, Dictionary<string, Guid> Contestants)> ParliamentaryResultAsync(
            Fixture f, string name, DateTime date, params (string Party, int Votes)[] parties)
        {
            f.User.IsAdmin = true;
            f.User.UserId = "manager";
            var election = (await f.Admin.CreateAsync(new CreateElectionInput(name, ElectionType.Parliamentary, date, 6, 4m))).Value;
            var ids = new Dictionary<string, Guid>();
            foreach (var p in parties)
            {
                ids[p.Party] = (await f.Admin.AddContestantAsync(election, new AddContestantInput(ContestantType.Party, p.Party, null, null))).Value;
                await f.Lists.SaveAsync(ids[p.Party], Enumerable.Range(1, 6).Select(i => new ListEntry($"{p.Party} {i}", i % 3 == 1 ? Gender.Female : Gender.Male)).ToList(), new List<ListEntry>());
            }
            Assert.True((await f.Admin.SetStatusAsync(election, ElectionStatus.Counting)).Succeeded);
            var round = (await f.Db.ElectionRounds.SingleAsync(r => r.ElectionId == election)).Id;

            var total = parties.Sum(p => p.Votes);
            SheetFiguresInput Sheet() => new(null, total + 10, total, 0, 10, 0, 0, parties.ToDictionary(p => ids[p.Party], p => p.Votes));
            f.User.UserId = "clerk-1";
            await f.Sheets.SubmitEntryAsync(round, Fixture.StationAileu, Sheet());
            var sheet = (await f.Db.ResultSheets.SingleAsync(s => s.ElectionRoundId == round)).Id;
            f.User.UserId = "clerk-2";
            await f.Sheets.SubmitVerificationAsync(sheet, Sheet());
            f.User.UserId = "supervisor";
            Assert.True((await f.Sheets.ApproveAsync(sheet)).Succeeded);
            return (round, ids);
        }

        [Fact]
        public async Task Adding_a_party_by_name_reuses_the_existing_record_ignoring_case()
        {
            using var f = new Fixture();
            var (_, first) = await ParliamentaryResultAsync(f, "Parliament 2023", new DateTime(2023, 5, 21), ("CNRT", 300), ("Fretilin", 200));
            var (_, second) = await ParliamentaryResultAsync(f, "Parliament 2028", new DateTime(2028, 5, 21), ("cnrt ", 250), ("New Party", 150));

            var a = await f.Db.Contestants.SingleAsync(c => c.Id == first["CNRT"]);
            var b = await f.Db.Contestants.SingleAsync(c => c.Id == second["cnrt "]);
            Assert.NotNull(a.PartyId);
            Assert.Equal(a.PartyId, b.PartyId);
            Assert.Equal(3, await f.Db.Parties.CountAsync()); // CNRT, Fretilin, New Party
        }

        [Fact]
        public async Task Results_are_compared_with_the_previous_election_by_party()
        {
            using var f = new Fixture();
            await ParliamentaryResultAsync(f, "Parliament 2023", new DateTime(2023, 5, 21), ("CNRT", 300), ("Fretilin", 200));
            var (round, ids) = await ParliamentaryResultAsync(f, "Parliament 2028", new DateTime(2028, 5, 21), ("CNRT", 250), ("Fretilin", 250), ("New Party", 100));

            var comparison = await f.Comparisons.GetAsync(round);

            Assert.Equal("Parliament 2023", comparison.PreviousElectionName);
            Assert.Equal(60.00m, comparison.For(ids["CNRT"]).PercentOfValid);      // 300 of 500 in 2023
            Assert.Equal(40.00m, comparison.For(ids["Fretilin"]).PercentOfValid);
            Assert.Null(comparison.For(ids["New Party"]));                        // first election
            Assert.NotNull(comparison.For(ids["CNRT"]).Seats);
        }

        [Fact]
        public async Task The_first_election_of_a_type_has_no_comparison()
        {
            using var f = new Fixture();
            var (round, _) = await ParliamentaryResultAsync(f, "Parliament 2023", new DateTime(2023, 5, 21), ("CNRT", 300), ("Fretilin", 200));
            Assert.Null(await f.Comparisons.GetAsync(round));
        }

        [Fact]
        public async Task Relinking_a_misspelled_party_merges_its_history()
        {
            using var f = new Fixture();
            await ParliamentaryResultAsync(f, "Parliament 2023", new DateTime(2023, 5, 21), ("Fretilin", 200), ("CNRT", 300));
            var (round, ids) = await ParliamentaryResultAsync(f, "Parliament 2028", new DateTime(2028, 5, 21), ("FRETILIN Party", 250), ("CNRT", 250));
            var fretilin = (await f.Db.Parties.SingleAsync(p => p.Name == "Fretilin")).Id;
            Assert.Null((await f.Comparisons.GetAsync(round)).For(ids["FRETILIN Party"]));

            Assert.True((await f.Parties.LinkContestantAsync(ids["FRETILIN Party"], fretilin)).Succeeded);

            Assert.NotNull((await f.Comparisons.GetAsync(round)).For(ids["FRETILIN Party"])); // cache invalidated by the link
            var history = await f.Parties.GetHistoryAsync(fretilin);
            Assert.Equal(new[] { "Parliament 2028", "Parliament 2023" }, history.Select(h => h.ElectionName));
            Assert.Equal(new[] { "FRETILIN Party", "Fretilin" }, history.Select(h => h.NameOnBallot));
            Assert.Equal(50.00m, history[0].PercentOfValid);
        }

        [Fact]
        public async Task Party_records_must_be_unique_and_are_kept_once_used()
        {
            using var f = new Fixture();
            await ParliamentaryResultAsync(f, "Parliament 2023", new DateTime(2023, 5, 21), ("CNRT", 300), ("Fretilin", 200));
            var dup = await f.Parties.CreateAsync(new PartyInput(" cnrt", null, PartyKind.Party, null));
            Assert.Equal(FailureKind.Validation, dup.Failure);

            var cnrt = (await f.Db.Parties.SingleAsync(p => p.Name == "CNRT")).Id;
            Assert.Equal(FailureKind.Conflict, (await f.Parties.DeleteAsync(cnrt)).Failure);

            var unused = (await f.Parties.CreateAsync(new PartyInput("Never Stood", "NS", PartyKind.Party, null))).Value;
            Assert.True((await f.Parties.DeleteAsync(unused)).Succeeded);
        }
    }
}
