using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VotingApp.Application.Elections;
using VotingApp.Domain.Elections;

namespace VotingApp.Tests
{
    public class OfficialResultsTests
    {
        /// <summary>Aileu sheet approved (A=300, B=200, C=100); Ainaro sheet only entered.</summary>
        private static async Task<(Fixture F, Guid Round, Guid[] Ids, OfficialResultsService Svc)> ArrangeAsync()
        {
            var f = new Fixture();
            f.User.IsAdmin = true;
            var (round, ids) = await f.PresidentialInCountingAsync();
            f.User.UserId = "clerk-1";
            await f.Sheets.SubmitEntryAsync(round, Fixture.StationAileu, Fixture.Sheet(ids, 300, 200, 100));
            await f.Sheets.SubmitEntryAsync(round, Fixture.StationAinaro, Fixture.Sheet(ids, 1, 2, 3));
            var aileu = (await f.Db.ResultSheets.SingleAsync(s => s.PollingStationAreaId == Fixture.StationAileu)).Id;
            f.User.UserId = "clerk-2";
            await f.Sheets.SubmitVerificationAsync(aileu, Fixture.Sheet(ids, 300, 200, 100));
            f.User.UserId = "supervisor";
            Assert.True((await f.Sheets.ApproveAsync(aileu)).Succeeded);
            return (f, round, ids, new OfficialResultsService(f.Db, f.Results, f.Public, f.Lists));
        }

        [Fact]
        public async Task National_record_counts_only_approved_sheets_and_ranks_contestants()
        {
            var (f, round, ids, svc) = await ArrangeAsync();
            using var _ = f;
            var doc = await svc.GetAsync(round);

            Assert.True(doc.IsNational);
            Assert.False(doc.IsFinal);
            Assert.Equal(new DateTime(2027, 3, 20), doc.VotingDate.Date);
            var t = doc.Totals;
            Assert.Equal((2, 1, 1), (t.PollingStations, t.SheetsCounted, t.StationsMissing));
            Assert.Equal((1000L, 610L, 600L, 6L, 4L), (t.RegisteredVoters, t.BallotsUsed, t.ValidVotes, t.BlankVotes, t.InvalidVotes));
            Assert.Equal(61.00m, t.TurnoutPercent);
            Assert.Null((t with { RegisteredVoters = 0 }).TurnoutPercent);

            Assert.Equal(new[] { ids[0], ids[1], ids[2] }, doc.Contestants.Select(c => c.ContestantId));
            Assert.Equal(new[] { 50.00m, 33.33m, 16.67m }, doc.Contestants.Select(c => c.PercentOfValid));
            Assert.All(doc.Contestants, c => Assert.Null(c.Seats));

            Assert.Equal(new[] { "Aileu", "Ainaro" }, doc.Breakdown.Select(r => r.Name));
            Assert.Equal(600, doc.Breakdown[0].ValidVotes);
            Assert.Equal(0, doc.Breakdown[1].SheetsCounted);
            Assert.Equal(0, doc.Breakdown[1].ValidVotes); // the entered Ainaro figures stay out
        }

        [Fact]
        public async Task Fingerprint_matches_the_downloaded_csv()
        {
            var (f, round, _, svc) = await ArrangeAsync();
            using var _ = f;
            var doc = await svc.GetAsync(round);
            var file = await f.Public.ExportCountedSheetsFileAsync(round);

            Assert.Equal(System.Security.Cryptography.SHA256.HashData(file).Select(b => b.ToString("x2")).Aggregate(string.Concat), doc.DataFingerprint);
            Assert.Equal(64, doc.DataFingerprint.Length);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, file.Take(3)); // the BOM is part of what is hashed
        }

        [Fact]
        public async Task Municipal_record_lists_each_station_with_its_status()
        {
            var (f, round, _, svc) = await ArrangeAsync();
            using var _ = f;
            var doc = await svc.GetAsync(round, "TLS002");

            Assert.False(doc.IsNational);
            Assert.Equal("Ainaro", doc.MunicipalityName);
            var row = Assert.Single(doc.Breakdown);
            Assert.Equal("EB Horai", row.Name);
            Assert.Equal("Maubisse · Horai-Quic", row.Detail);
            Assert.Equal(ResultSheetStatus.Entered, row.Status);
            Assert.Equal((1, 0, 0L), (doc.Totals.PollingStations, doc.Totals.SheetsCounted, doc.Totals.ValidVotes));
            Assert.All(doc.Contestants, c => Assert.Equal(0m, c.PercentOfValid));
        }

        [Fact]
        public async Task Unknown_round_or_municipality_has_no_record()
        {
            var (f, round, _, svc) = await ArrangeAsync();
            using var _ = f;
            Assert.Null(await svc.GetAsync(Guid.NewGuid()));
            Assert.Null(await svc.GetAsync(round, "TLS999"));
        }
    }
}
