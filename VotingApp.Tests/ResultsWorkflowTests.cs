using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using VotingApp.Application.Abstractions;
using VotingApp.Application.Common;
using VotingApp.Application.Elections;
using VotingApp.Application.Results;
using VotingApp.Domain.Elections;
using VotingApp.Domain.Elections.Rules;
using VotingApp.Domain.Entities;
using VotingApp.Infrastructure.Persistence;

namespace VotingApp.Tests
{
    internal sealed class FakeUser : ICurrentUser
    {
        public string UserId { get; set; } = "admin";
        public bool IsAuthenticated => UserId != null;
        public bool IsAdmin { get; set; }
    }

    /// <summary>Real services and AppDbContext over EF in-memory, with two municipalities and one station each.</summary>
    internal sealed class Fixture : IDisposable
    {
        public static readonly Guid StationAileu = Guid.Parse("10000000-0000-0000-0000-000000000001");
        public static readonly Guid StationAinaro = Guid.Parse("10000000-0000-0000-0000-000000000002");

        public FakeUser User { get; } = new();
        public AppDbContext Db { get; }
        public ElectionResultsService Results { get; }
        public ElectionAdminService Admin { get; }
        public ResultSheetService Sheets { get; }
        public UserScopeService Scopes { get; }

        public Fixture()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            Db = new AppDbContext(options, User);
            Results = new ElectionResultsService(Db, new MemoryCache(new MemoryCacheOptions()),
                new PresidentialResultCalculator(), new ParliamentarySeatAllocator());
            Admin = new ElectionAdminService(Db, Results);
            Scopes = new UserScopeService(Db, User);
            Sheets = new ResultSheetService(Db, User, Scopes, new ResultSheetValidator(), Results);

            Db.Areas.AddRange(
                new Area { Id = "TLS001", Name = "Aileu", Level = AreaHierarchy.Municipality },
                new Area { Id = "TLS00101", Name = "Aileu Vila", ParentId = "TLS001", Level = AreaHierarchy.AdministrativePost },
                new Area { Id = "TLS0010101", Name = "Seloi Malere", ParentId = "TLS00101", Level = AreaHierarchy.Village },
                new Area { Id = "TLS002", Name = "Ainaro", Level = AreaHierarchy.Municipality },
                new Area { Id = "TLS00201", Name = "Maubisse", ParentId = "TLS002", Level = AreaHierarchy.AdministrativePost },
                new Area { Id = "TLS0020101", Name = "Horai-Quic", ParentId = "TLS00201", Level = AreaHierarchy.Village });
            Db.PollingStationAreas.AddRange(
                new PollingStationArea { Id = StationAileu, AreaId = "TLS0010101", PollingStation = new PollingStation { Id = Guid.NewGuid(), Name = "EB Seloi" } },
                new PollingStationArea { Id = StationAinaro, AreaId = "TLS0020101", PollingStation = new PollingStation { Id = Guid.NewGuid(), Name = "EB Horai" } });
            Db.SaveChanges();
        }

        /// <summary>Presidential election with candidates A, B, C, opened for counting. Returns (roundId, contestant ids).</summary>
        public async Task<(Guid RoundId, Guid[] Ids)> PresidentialInCountingAsync()
        {
            var election = (await Admin.CreateAsync(new CreateElectionInput("Presidential 2027", ElectionType.Presidential, new DateTime(2027, 3, 20), null, null))).Value;
            var ids = new List<Guid>();
            foreach (var name in new[] { "A", "B", "C" })
            {
                ids.Add((await Admin.AddContestantAsync(election, new AddContestantInput(ContestantType.Candidate, name, null, null))).Value);
            }
            Assert.True((await Admin.SetStatusAsync(election, ElectionStatus.Counting)).Succeeded);
            var round = await Db.ElectionRounds.SingleAsync(r => r.ElectionId == election);
            return (round.Id, ids.ToArray());
        }

        /// <summary>A consistent sheet: votes add up to ballots used; received = used + spoiled + unused.</summary>
        public static SheetFiguresInput Sheet(Guid[] ids, int a, int b, int c) => new(
            RegisteredVoters: 1000, BallotsReceived: 1000, BallotsUsed: a + b + c + 10, BallotsSpoiled: 5,
            BallotsUnused: 1000 - (a + b + c + 10) - 5, BlankVotes: 6, InvalidVotes: 4,
            ContestantVotes: new Dictionary<Guid, int> { [ids[0]] = a, [ids[1]] = b, [ids[2]] = c });

        public void Dispose() => Db.Dispose();
    }

    public class ResultsWorkflowTests
    {
        [Fact]
        public async Task Sheet_goes_entered_verified_approved_and_is_counted()
        {
            using var f = new Fixture();
            f.User.IsAdmin = true;
            var (round, ids) = await f.PresidentialInCountingAsync();

            f.User.UserId = "clerk-1";
            var entry = await f.Sheets.SubmitEntryAsync(round, Fixture.StationAileu, Fixture.Sheet(ids, 300, 200, 100));
            Assert.True(entry.Succeeded, entry.Error);
            Assert.False(entry.Value.HasErrors);

            var sheetId = (await f.Db.ResultSheets.SingleAsync()).Id;
            var sameUser = await f.Sheets.SubmitVerificationAsync(sheetId, Fixture.Sheet(ids, 300, 200, 100));
            Assert.Equal(FailureKind.Forbidden, sameUser.Failure);

            f.User.UserId = "clerk-2";
            var verify = await f.Sheets.SubmitVerificationAsync(sheetId, Fixture.Sheet(ids, 300, 200, 100));
            Assert.True(verify.Succeeded, verify.Error);
            Assert.True(verify.Value.Matched);

            Assert.Equal(0, (await f.Results.GetSummaryAsync(round)).SheetsCounted); // not approved yet

            f.User.UserId = "supervisor";
            Assert.True((await f.Sheets.ApproveAsync(sheetId)).Succeeded);

            var summary = await f.Results.GetSummaryAsync(round);
            Assert.Equal(1, summary.SheetsCounted);
            Assert.Equal(600, summary.ValidVotes);
            Assert.Equal(PresidentialOutcome.Runoff, summary.Presidential.Outcome); // A has exactly 50%: not a majority
        }

        [Fact]
        public async Task Mismatched_second_entry_keeps_sheet_entered_and_names_fields()
        {
            using var f = new Fixture();
            f.User.IsAdmin = true;
            var (round, ids) = await f.PresidentialInCountingAsync();

            f.User.UserId = "clerk-1";
            await f.Sheets.SubmitEntryAsync(round, Fixture.StationAileu, Fixture.Sheet(ids, 300, 200, 100));
            var sheetId = (await f.Db.ResultSheets.SingleAsync()).Id;

            f.User.UserId = "clerk-2";
            var typo = Fixture.Sheet(ids, 300, 200, 100) with
            {
                ContestantVotes = new Dictionary<Guid, int> { [ids[0]] = 300, [ids[1]] = 290, [ids[2]] = 100 },
            };
            var verify = await f.Sheets.SubmitVerificationAsync(sheetId, typo);

            Assert.True(verify.Succeeded);
            Assert.False(verify.Value.Matched);
            Assert.Equal(new[] { "B" }, verify.Value.MismatchedFields);
            var sheet = await f.Db.ResultSheets.SingleAsync();
            Assert.Equal(ResultSheetStatus.Entered, sheet.Status);
            Assert.Equal(1, sheet.VerificationMismatches);
            Assert.Equal("B", sheet.LastMismatchFields);
        }

        [Fact]
        public async Task Sheet_with_arithmetic_errors_cannot_be_approved()
        {
            using var f = new Fixture();
            f.User.IsAdmin = true;
            var (round, ids) = await f.PresidentialInCountingAsync();
            var bad = Fixture.Sheet(ids, 300, 200, 100) with { BallotsUsed = 999 }; // doesn't match the votes

            f.User.UserId = "clerk-1";
            var entry = await f.Sheets.SubmitEntryAsync(round, Fixture.StationAileu, bad);
            Assert.True(entry.Succeeded);         // saved as typed from paper...
            Assert.True(entry.Value.HasErrors);   // ...but flagged

            var sheetId = (await f.Db.ResultSheets.SingleAsync()).Id;
            f.User.UserId = "clerk-2";
            Assert.True((await f.Sheets.SubmitVerificationAsync(sheetId, bad)).Value.Matched);

            f.User.UserId = "supervisor";
            var approve = await f.Sheets.ApproveAsync(sheetId);
            Assert.Equal(FailureKind.Validation, approve.Failure);
        }

        [Fact]
        public async Task Clerk_cannot_touch_stations_outside_assigned_municipality()
        {
            using var f = new Fixture();
            f.User.IsAdmin = true;
            var (round, ids) = await f.PresidentialInCountingAsync();
            await f.Scopes.SetAssignmentsAsync("aileu-clerk", new[] { "TLS001" });

            f.User.IsAdmin = false;
            f.User.UserId = "aileu-clerk";
            Assert.True((await f.Sheets.SubmitEntryAsync(round, Fixture.StationAileu, Fixture.Sheet(ids, 1, 2, 3))).Succeeded);
            var outside = await f.Sheets.SubmitEntryAsync(round, Fixture.StationAinaro, Fixture.Sheet(ids, 1, 2, 3));
            Assert.Equal(FailureKind.Forbidden, outside.Failure);

            var visible = await f.Sheets.ListStationsAsync(round);
            Assert.Equal(new[] { Fixture.StationAileu }, visible.Select(s => s.PollingStationAreaId));
        }

        [Fact]
        public async Task Entry_is_refused_while_election_is_not_counting()
        {
            using var f = new Fixture();
            f.User.IsAdmin = true;
            var election = (await f.Admin.CreateAsync(new CreateElectionInput("P", ElectionType.Presidential, DateTime.Today, null, null))).Value;
            var a = (await f.Admin.AddContestantAsync(election, new AddContestantInput(ContestantType.Candidate, "A", null, null))).Value;
            var round = (await f.Db.ElectionRounds.SingleAsync()).Id;

            var result = await f.Sheets.SubmitEntryAsync(round, Fixture.StationAileu, new SheetFiguresInput(1, 1, 1, 0, 0, 0, 0, new Dictionary<Guid, int> { [a] = 1 }));
            Assert.Equal(FailureKind.Conflict, result.Failure);
        }

        [Fact]
        public async Task Reopen_sends_approved_sheet_back_and_uncounts_it()
        {
            using var f = new Fixture();
            f.User.IsAdmin = true;
            var (round, ids) = await f.PresidentialInCountingAsync();
            f.User.UserId = "clerk-1";
            await f.Sheets.SubmitEntryAsync(round, Fixture.StationAileu, Fixture.Sheet(ids, 3, 2, 1));
            var sheetId = (await f.Db.ResultSheets.SingleAsync()).Id;
            f.User.UserId = "clerk-2";
            await f.Sheets.SubmitVerificationAsync(sheetId, Fixture.Sheet(ids, 3, 2, 1));
            await f.Sheets.ApproveAsync(sheetId);
            Assert.Equal(1, (await f.Results.GetSummaryAsync(round)).SheetsCounted);

            Assert.Equal(FailureKind.Validation, (await f.Sheets.ReopenAsync(sheetId, " ")).Failure);
            Assert.True((await f.Sheets.ReopenAsync(sheetId, "Wrong station number on paper")).Succeeded);

            var sheet = await f.Db.ResultSheets.SingleAsync();
            Assert.Equal(ResultSheetStatus.Entered, sheet.Status);
            Assert.Contains("Wrong station number", sheet.Notes);
            Assert.Equal(0, (await f.Results.GetSummaryAsync(round)).SheetsCounted); // cache was invalidated
        }

        [Fact]
        public async Task Lock_then_runoff_between_top_two()
        {
            using var f = new Fixture();
            f.User.IsAdmin = true;
            var (round, ids) = await f.PresidentialInCountingAsync();

            foreach (var (station, votes) in new[] { (Fixture.StationAileu, (a: 300, b: 250, c: 150)), (Fixture.StationAinaro, (a: 100, b: 200, c: 50)) })
            {
                f.User.UserId = "clerk-1";
                await f.Sheets.SubmitEntryAsync(round, station, Fixture.Sheet(ids, votes.a, votes.b, votes.c));
                var id = (await f.Db.ResultSheets.SingleAsync(s => s.PollingStationAreaId == station)).Id;
                f.User.UserId = "clerk-2";
                await f.Sheets.SubmitVerificationAsync(id, Fixture.Sheet(ids, votes.a, votes.b, votes.c));
                f.User.UserId = "supervisor";
                Assert.True((await f.Sheets.ApproveAsync(id)).Succeeded);
            }

            f.User.UserId = "national";
            var locked = await f.Sheets.LockApprovedAsync(round);
            Assert.Equal(2, locked.Value);

            // A 400, B 450, C 200 of 1050 valid: nobody > 50% -> runoff B vs A.
            var summary = await f.Results.GetSummaryAsync(round);
            Assert.Equal(PresidentialOutcome.Runoff, summary.Presidential.Outcome);

            var election = (await f.Db.ElectionRounds.SingleAsync(r => r.Id == round)).ElectionId;
            var runoff = await f.Admin.CreateRunoffAsync(election, new DateTime(2027, 4, 20));
            Assert.True(runoff.Succeeded, runoff.Error);
            var ballot = await f.Db.RoundContestants.Where(rc => rc.ElectionRoundId == runoff.Value).OrderBy(rc => rc.BallotPosition).Select(rc => rc.ContestantId).ToListAsync();
            Assert.Equal(new[] { ids[1], ids[0] }, ballot);

            Assert.Equal(FailureKind.Conflict, (await f.Admin.CreateRunoffAsync(election, DateTime.Today)).Failure);
        }

        [Fact]
        public async Task Changes_are_audited_with_the_current_user()
        {
            using var f = new Fixture();
            f.User.IsAdmin = true;
            f.User.UserId = "manager-7";
            await f.PresidentialInCountingAsync();
            Assert.Contains(await f.Db.AuditLogs.ToListAsync(), a => a.TableName == nameof(Election) && a.UserId == "manager-7");
        }
    }

    public class VoteEntryServiceTests
    {
        [Fact]
        public async Task Adds_then_updates_one_row_per_candidate_and_station_and_rejects_bad_input()
        {
            using var f = new Fixture();
            var cand = new Candidate { Id = Guid.NewGuid(), Name = "Party A" };
            f.Db.Candidates.Add(cand);
            await f.Db.SaveChangesAsync();
            var service = new Application.Votes.VoteEntryService(f.Db, NullLogger<Application.Votes.VoteEntryService>.Instance);

            var added = await service.SaveAsync(cand.Id, Fixture.StationAileu, 10);
            var updated = await service.SaveAsync(cand.Id, Fixture.StationAileu, 25);
            Assert.Equal(Application.Votes.VoteSaveKind.Added, added.Value);
            Assert.Equal(Application.Votes.VoteSaveKind.Updated, updated.Value);
            Assert.Equal(25, (await f.Db.Votes.SingleAsync()).Total);

            Assert.Equal(FailureKind.Validation, (await service.SaveAsync(cand.Id, Fixture.StationAileu, -1)).Failure);
            Assert.Equal(FailureKind.Validation, (await service.SaveAsync(Guid.NewGuid(), Fixture.StationAileu, 1)).Failure);
        }
    }

    public class ResultsQueryServiceTests
    {
        [Fact]
        public async Task Totals_by_municipality_include_empty_municipalities_and_map_keys()
        {
            using var f = new Fixture();
            var cand = new Candidate { Id = Guid.NewGuid(), Name = "Party A" };
            f.Db.Candidates.Add(cand);
            f.Db.Votes.AddRange(
                new Vote { Id = Guid.NewGuid(), CandidateId = cand.Id, PollingStationAreaId = Fixture.StationAileu, Total = 40 });
            await f.Db.SaveChangesAsync();

            var result = await new ResultsQueryService(f.Db).GetTotalsByMunicipalityAsync(null);

            Assert.Equal(new[] { "Aileu", "Ainaro" }, result.Keys);
            Assert.Equal(40, result["Aileu"].TotalVote);
            Assert.Equal("tl-an", result["Aileu"].AreaKey);
            Assert.Null(result["Ainaro"]);
        }
    }
}
