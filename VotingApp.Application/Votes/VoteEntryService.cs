using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VotingApp.Application.Abstractions;
using VotingApp.Application.Common;
using VotingApp.Domain.Entities;

namespace VotingApp.Application.Votes
{
    public enum VoteSaveKind { Added, Updated }

    /// <summary>Quick entry of one candidate's total at one polling station (legacy Votes table).</summary>
    public class VoteEntryService
    {
        private readonly IAppDbContext db;
        private readonly ILogger<VoteEntryService> logger;

        public VoteEntryService(IAppDbContext db, ILogger<VoteEntryService> logger)
        {
            this.db = db;
            this.logger = logger;
        }

        public async Task<OperationResult<VoteSaveKind>> SaveAsync(Guid candidateId, Guid pollingStationAreaId, int total, CancellationToken ct = default)
        {
            if (candidateId == Guid.Empty || pollingStationAreaId == Guid.Empty)
                return OperationResult<VoteSaveKind>.Fail(FailureKind.Validation, "Candidate and polling station are required.");
            if (total < 0)
                return OperationResult<VoteSaveKind>.Fail(FailureKind.Validation, "Total votes cannot be negative.");
            if (!await db.Candidates.AnyAsync(c => c.Id == candidateId, ct) ||
                !await db.PollingStationAreas.AnyAsync(p => p.Id == pollingStationAreaId, ct))
                return OperationResult<VoteSaveKind>.Fail(FailureKind.Validation, "Unknown candidate or polling station.");

            var vote = await db.Votes.FirstOrDefaultAsync(v => v.CandidateId == candidateId && v.PollingStationAreaId == pollingStationAreaId, ct);
            var kind = VoteSaveKind.Updated;
            if (vote == null)
            {
                vote = new Vote { Id = Guid.NewGuid(), CandidateId = candidateId, PollingStationAreaId = pollingStationAreaId };
                db.Votes.Add(vote);
                kind = VoteSaveKind.Added;
            }
            vote.Total = total;

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                // Typically the unique (candidate, station) index: another clerk inserted the same row first.
                logger.LogWarning(ex, "Saving vote for candidate {CandidateId} at {StationId} failed", candidateId, pollingStationAreaId);
                return OperationResult<VoteSaveKind>.Fail(FailureKind.Conflict, "The vote was changed by someone else at the same time. Reload and try again.");
            }
            return OperationResult<VoteSaveKind>.Success(kind);
        }

        public Task<Vote> GetWithDetailsAsync(Guid id, CancellationToken ct = default) =>
            db.Votes.AsNoTracking()
                .Include(v => v.Candidate)
                .Include(v => v.PollingStationArea).ThenInclude(p => p.PollingStation)
                .Include(v => v.PollingStationArea).ThenInclude(p => p.Area)
                .FirstOrDefaultAsync(v => v.Id == id, ct);

        public Task<List<Vote>> ListForPollingStationAsync(Guid pollingStationAreaId, CancellationToken ct = default) =>
            db.Votes.AsNoTracking()
                .Where(v => v.PollingStationAreaId == pollingStationAreaId)
                .Include(v => v.Candidate)
                .Include(v => v.PollingStationArea).ThenInclude(p => p.Area)
                .Include(v => v.PollingStationArea).ThenInclude(p => p.PollingStation)
                .ToListAsync(ct);
    }
}
