using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VotingApp.Application.Abstractions;
using VotingApp.Application.Common;
using VotingApp.Domain.Elections;
using VotingApp.Domain.Elections.Rules;

namespace VotingApp.Application.Elections
{
    public sealed record CreateElectionInput(string Name, ElectionType Type, DateTime ElectionDate, int? TotalSeats, decimal? ThresholdPercent);

    public sealed record AddContestantInput(ContestantType Type, string Name, string Acronym, Guid? CandidateId);

    /// <summary>Election set-up: elections, rounds, ballots and the presidential runoff.</summary>
    public class ElectionAdminService
    {
        private readonly IAppDbContext db;
        private readonly ElectionResultsService results;
        private readonly PartyListService lists;
        private readonly StationRosterService rosters;

        public ElectionAdminService(IAppDbContext db, ElectionResultsService results, PartyListService lists, StationRosterService rosters)
        {
            this.db = db;
            this.results = results;
            this.lists = lists;
            this.rosters = rosters;
        }

        public Task<List<Election>> ListAsync(CancellationToken ct = default) =>
            db.Elections.AsNoTracking().Include(e => e.Rounds).OrderByDescending(e => e.ElectionDate).ToListAsync(ct);

        public Task<Election> GetAsync(Guid id, CancellationToken ct = default) =>
            db.Elections.AsNoTracking()
                .Include(e => e.Rounds).ThenInclude(r => r.Contestants).ThenInclude(rc => rc.Contestant)
                .Include(e => e.Contestants)
                .FirstOrDefaultAsync(e => e.Id == id, ct);

        public Task<ElectionRound> GetRoundAsync(Guid roundId, CancellationToken ct = default) =>
            db.ElectionRounds.AsNoTracking()
                .Include(r => r.Election)
                .Include(r => r.Contestants).ThenInclude(rc => rc.Contestant)
                .FirstOrDefaultAsync(r => r.Id == roundId, ct);

        public async Task<OperationResult<Guid>> CreateAsync(CreateElectionInput input, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(input.Name))
                return OperationResult<Guid>.Fail(FailureKind.Validation, "Name is required.");
            if (await db.Elections.AnyAsync(e => e.Name == input.Name.Trim(), ct))
                return OperationResult<Guid>.Fail(FailureKind.Conflict, "An election with this name already exists.");

            var parliamentary = input.Type == ElectionType.Parliamentary;
            int? seats = parliamentary ? input.TotalSeats ?? ParliamentarySeatAllocator.DefaultSeats : null;
            decimal? threshold = parliamentary ? input.ThresholdPercent ?? ParliamentarySeatAllocator.DefaultThresholdPercent : null;
            if (parliamentary && (seats <= 0 || threshold < 0 || threshold >= 100))
                return OperationResult<Guid>.Fail(FailureKind.Validation, "Seats must be positive and the threshold between 0 and 100.");

            var election = new Election
            {
                Id = Guid.NewGuid(),
                Name = input.Name.Trim(),
                Type = input.Type,
                Status = ElectionStatus.Setup,
                ElectionDate = input.ElectionDate.Date,
                TotalSeats = seats,
                ThresholdPercent = threshold,
                CreatedAt = DateTime.UtcNow,
            };
            election.Rounds.Add(new ElectionRound { Id = Guid.NewGuid(), Number = 1, VotingDate = input.ElectionDate.Date, Status = ElectionStatus.Setup });
            db.Elections.Add(election);
            await db.SaveChangesAsync(ct);
            return OperationResult<Guid>.Success(election.Id);
        }

        /// <summary>Adds a contestant and puts it on the first-round ballot (only while the election is in set-up).</summary>
        public async Task<OperationResult<Guid>> AddContestantAsync(Guid electionId, AddContestantInput input, CancellationToken ct = default)
        {
            var election = await db.Elections.Include(e => e.Rounds).Include(e => e.Contestants).FirstOrDefaultAsync(e => e.Id == electionId, ct);
            if (election == null) return OperationResult<Guid>.NotFound("Election not found.");
            if (election.Status != ElectionStatus.Setup || election.Rounds.Count > 1)
                return OperationResult<Guid>.Fail(FailureKind.Conflict, "Contestants can only be added while the first round is in set-up.");
            if (string.IsNullOrWhiteSpace(input.Name))
                return OperationResult<Guid>.Fail(FailureKind.Validation, "Name is required.");

            var expectedCandidateType = election.Type == ElectionType.Presidential;
            if (expectedCandidateType != (input.Type == ContestantType.Candidate))
                return OperationResult<Guid>.Fail(FailureKind.Validation, expectedCandidateType
                    ? "Presidential elections take candidates."
                    : "Parliamentary elections take parties or coalitions.");
            if (election.Contestants.Any(c => string.Equals(c.Name, input.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                return OperationResult<Guid>.Fail(FailureKind.Conflict, "A contestant with this name already exists in this election.");

            var contestant = new Contestant
            {
                Id = Guid.NewGuid(),
                ElectionId = electionId,
                Type = input.Type,
                Name = input.Name.Trim(),
                Acronym = input.Acronym?.Trim(),
                CandidateId = input.CandidateId,
            };
            db.Contestants.Add(contestant);

            var firstRound = election.Rounds.OrderBy(r => r.Number).First();
            var position = await db.RoundContestants.CountAsync(rc => rc.ElectionRoundId == firstRound.Id, ct) + 1;
            db.RoundContestants.Add(new RoundContestant { ElectionRoundId = firstRound.Id, ContestantId = contestant.Id, BallotPosition = position });

            await db.SaveChangesAsync(ct);
            return OperationResult<Guid>.Success(contestant.Id);
        }

        public async Task<OperationResult> RemoveContestantAsync(Guid contestantId, CancellationToken ct = default)
        {
            var contestant = await db.Contestants.Include(c => c.Election).FirstOrDefaultAsync(c => c.Id == contestantId, ct);
            if (contestant == null) return OperationResult.NotFound("Contestant not found.");
            if (contestant.Election.Status != ElectionStatus.Setup)
                return OperationResult.Fail(FailureKind.Conflict, "Contestants can only be removed while the election is in set-up.");
            if (await db.ResultSheetEntries.AnyAsync(e => e.ContestantId == contestantId, ct))
                return OperationResult.Fail(FailureKind.Conflict, "Results are recorded for this contestant.");

            db.RoundContestants.RemoveRange(await db.RoundContestants.Where(rc => rc.ContestantId == contestantId).ToListAsync(ct));
            db.Contestants.Remove(contestant);
            await db.SaveChangesAsync(ct);
            return OperationResult.Success();
        }

        /// <summary>Moves an election (and its current round) forward one status at a time; certified is final.</summary>
        public async Task<OperationResult> SetStatusAsync(Guid electionId, ElectionStatus status, CancellationToken ct = default)
        {
            var election = await db.Elections.Include(e => e.Rounds).Include(e => e.Contestants).FirstOrDefaultAsync(e => e.Id == electionId, ct);
            if (election == null) return OperationResult.NotFound("Election not found.");
            var current = election.Rounds.OrderByDescending(r => r.Number).First();
            if (current.Status == ElectionStatus.Certified)
                return OperationResult.Fail(FailureKind.Conflict, "The current round is certified and cannot change status.");
            if (status < current.Status)
                return OperationResult.Fail(FailureKind.Conflict, "Status can only move forward.");
            if (status > ElectionStatus.Setup && election.Contestants.Count < 2)
                return OperationResult.Fail(FailureKind.Validation, "Add at least two contestants before opening the election.");
            if (election.Type == ElectionType.Parliamentary && current.Status == ElectionStatus.Setup && status > ElectionStatus.Setup)
            {
                // Lists are fixed once voting starts: every party needs a complete, quota-compliant list.
                var names = election.Contestants.ToDictionary(c => c.Id, c => c.Name);
                var invalid = (await lists.GetStatusesAsync(election.Id, ct)).Where(s => !s.IsValid).ToList();
                if (invalid.Count > 0)
                {
                    return OperationResult.Fail(FailureKind.Validation,
                        "Fix these party lists before opening the election: " +
                        string.Join("; ", invalid.Select(s => $"{names[s.ContestantId]} ({s.Problem})")));
                }
            }

            // Opening freezes the polling station list: later changes to stations never alter this election.
            if (current.Number == 1 && current.Status == ElectionStatus.Setup && status > ElectionStatus.Setup)
            {
                if (await rosters.FreezeAsync(election.Id, ct) == 0 && !await db.ElectionStations.AnyAsync(s => s.ElectionId == election.Id, ct))
                    return OperationResult.Fail(FailureKind.Validation, "There are no polling stations to open the election with.");
            }

            // The election's status mirrors its current (latest) round.
            current.Status = status;
            election.Status = status;
            await db.SaveChangesAsync(ct);
            results.Invalidate(current.Id);
            results.Invalidate(PublicResultsService.AllRounds);
            return OperationResult.Success();
        }

        /// <summary>
        /// Presidential only: creates round 2 with the two qualified candidates, using approved and
        /// locked first-round sheets. Refuses if a candidate was elected outright or a tie needs resolution.
        /// </summary>
        public async Task<OperationResult<Guid>> CreateRunoffAsync(Guid electionId, DateTime votingDate, CancellationToken ct = default)
        {
            var election = await db.Elections.Include(e => e.Rounds).FirstOrDefaultAsync(e => e.Id == electionId, ct);
            if (election == null) return OperationResult<Guid>.NotFound("Election not found.");
            if (election.Type != ElectionType.Presidential)
                return OperationResult<Guid>.Fail(FailureKind.Validation, "Only presidential elections have a runoff.");
            if (election.Rounds.Any(r => r.Number == 2))
                return OperationResult<Guid>.Fail(FailureKind.Conflict, "The runoff already exists.");

            var first = election.Rounds.Single(r => r.Number == 1);
            var summary = await results.GetSummaryAsync(first.Id, ResultSheetStatus.Approved, ct);
            if (summary?.Presidential == null)
                return OperationResult<Guid>.Fail(FailureKind.Validation, "No approved first-round results yet.");
            if (summary.Presidential.Outcome != PresidentialOutcome.Runoff)
                return OperationResult<Guid>.Fail(FailureKind.Conflict, summary.Presidential.Outcome switch
                {
                    PresidentialOutcome.Elected => "A candidate was elected in the first round: no runoff is needed.",
                    PresidentialOutcome.TieRequiresResolution => "A tie decides who qualifies; resolve it under the law before creating the runoff.",
                    _ => "First-round results do not yet determine the runoff.",
                });

            var round = new ElectionRound
            {
                Id = Guid.NewGuid(),
                ElectionId = electionId,
                Number = 2,
                VotingDate = votingDate.Date,
                Status = ElectionStatus.Setup,
            };
            var position = 1;
            foreach (var id in summary.Presidential.RunoffContestantIds)
            {
                round.Contestants.Add(new RoundContestant { ElectionRoundId = round.Id, ContestantId = id, BallotPosition = position++ });
            }
            db.ElectionRounds.Add(round);
            election.Status = ElectionStatus.Setup; // now tracks round 2
            await db.SaveChangesAsync(ct);
            return OperationResult<Guid>.Success(round.Id);
        }
    }
}
