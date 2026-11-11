using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VotingApp.Application.Elections;
using VotingApp.Domain.Elections;

namespace VotingApp.Tests
{
    public class OfflineEntryTests
    {
        private static OfflineEntryService Service(Fixture f) => new(f.Db, f.User, f.Sheets, f.Public);

        private static OfflineSubmission Entry(Guid id, Guid round, Guid[] ids, int a, int b, int c) =>
            new(id, SubmissionKind.Entry, round, Fixture.StationAileu, DateTime.UtcNow, Fixture.Sheet(ids, a, b, c));

        private static OfflineSubmission Verify(Guid id, Guid round, Guid[] ids, int a, int b, int c) =>
            new(id, SubmissionKind.Verification, round, Fixture.StationAileu, DateTime.UtcNow, Fixture.Sheet(ids, a, b, c));

        [Fact]
        public async Task Resending_an_entry_returns_the_first_outcome_without_applying_it_again()
        {
            using var f = new Fixture();
            f.User.IsAdmin = true;
            var (round, ids) = await f.PresidentialInCountingAsync();
            var svc = Service(f);
            f.User.UserId = "clerk-1";
            var id = Guid.NewGuid();

            var first = await svc.SubmitAsync(Entry(id, round, ids, 300, 200, 100));
            var again = await svc.SubmitAsync(Entry(id, round, ids, 300, 200, 100));

            Assert.Equal(OfflineEntryService.Saved, first.Outcome);
            Assert.False(first.Replayed);
            Assert.Equal(OfflineEntryService.Saved, again.Outcome);
            Assert.True(again.Replayed);
            Assert.Equal(1, await f.Db.SheetSubmissions.CountAsync());
        }

        [Fact]
        public async Task A_mismatched_verification_sent_twice_counts_once()
        {
            using var f = new Fixture();
            f.User.IsAdmin = true;
            var (round, ids) = await f.PresidentialInCountingAsync();
            var svc = Service(f);
            f.User.UserId = "clerk-1";
            await svc.SubmitAsync(Entry(Guid.NewGuid(), round, ids, 300, 200, 100));

            f.User.UserId = "clerk-2";
            var id = Guid.NewGuid();
            var typo = Verify(id, round, ids, 300, 190, 110);
            var first = await svc.SubmitAsync(typo);
            var again = await svc.SubmitAsync(typo);

            Assert.Equal(OfflineEntryService.Mismatch, first.Outcome);
            Assert.Contains("B", first.Message);
            Assert.True(again.Replayed);
            Assert.Equal(1, (await f.Db.ResultSheets.SingleAsync()).VerificationMismatches);

            var good = await svc.SubmitAsync(Verify(Guid.NewGuid(), round, ids, 300, 200, 100));
            Assert.Equal(OfflineEntryService.Verified, good.Outcome);
        }

        [Fact]
        public async Task Online_rules_still_apply_and_rejections_can_be_retried()
        {
            using var f = new Fixture();
            f.User.IsAdmin = true;
            var (round, ids) = await f.PresidentialInCountingAsync();
            var svc = Service(f);

            // Verification before any first entry.
            f.User.UserId = "clerk-2";
            Assert.Equal(OfflineEntryService.Rejected, (await svc.SubmitAsync(Verify(Guid.NewGuid(), round, ids, 1, 1, 1))).Outcome);

            // Same person cannot verify their own entry.
            f.User.UserId = "clerk-1";
            await svc.SubmitAsync(Entry(Guid.NewGuid(), round, ids, 300, 200, 100));
            var own = Guid.NewGuid();
            var self = await svc.SubmitAsync(Verify(own, round, ids, 300, 200, 100));
            Assert.Equal(OfflineEntryService.Rejected, self.Outcome);

            // A rejected id is not recorded, so the same queued sheet can be sent again later.
            Assert.False(await f.Db.SheetSubmissions.AnyAsync(s => s.Id == own));
            f.User.UserId = "clerk-2";
            Assert.Equal(OfflineEntryService.Verified, (await svc.SubmitAsync(Verify(own, round, ids, 300, 200, 100))).Outcome);
        }

        [Fact]
        public async Task A_submission_id_cannot_be_replayed_by_another_account()
        {
            using var f = new Fixture();
            f.User.IsAdmin = true;
            var (round, ids) = await f.PresidentialInCountingAsync();
            var svc = Service(f);
            var id = Guid.NewGuid();
            f.User.UserId = "clerk-1";
            await svc.SubmitAsync(Entry(id, round, ids, 300, 200, 100));

            f.User.UserId = "someone-else";
            var replay = await svc.SubmitAsync(Entry(id, round, ids, 1, 1, 1));

            Assert.Equal(OfflineEntryService.Rejected, replay.Outcome);
            Assert.Contains("another account", replay.Message);
        }

        [Fact]
        public async Task The_pack_lists_the_ballot_and_stations_still_needing_entry_in_scope()
        {
            using var f = new Fixture();
            f.User.IsAdmin = true;
            var (round, ids) = await f.PresidentialInCountingAsync();
            await f.Scopes.SetAssignmentsAsync("aileu-clerk", new[] { "TLS001" });
            var svc = Service(f);

            f.User.IsAdmin = false;
            f.User.UserId = "aileu-clerk";
            await svc.SubmitAsync(Entry(Guid.NewGuid(), round, ids, 3, 2, 1));
            var pack = await svc.GetPackAsync(round);

            Assert.True(pack.EntryOpen);
            Assert.Equal(3, pack.Ballot.Count);
            var station = Assert.Single(pack.Stations); // Ainaro is outside the clerk's scope
            Assert.Equal(Fixture.StationAileu, station.Id);
            Assert.Equal("entered", station.Stage);
        }
    }
}
