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

namespace VotingApp.Application.Candidates
{
    public sealed record CandidateInput(string Name, string Description, string ShortBiography, string Avatar);

    public sealed record CandidateVoteSummary(int TotalVotes, IReadOnlyList<Vote> Votes);

    public class CandidateService
    {
        private readonly IAppDbContext db;
        private readonly ILogger<CandidateService> logger;

        public CandidateService(IAppDbContext db, ILogger<CandidateService> logger)
        {
            this.db = db;
            this.logger = logger;
        }

        public Task<List<Candidate>> ListAsync(CancellationToken ct = default) =>
            db.Candidates.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);

        public Task<Candidate> GetAsync(Guid id, CancellationToken ct = default) =>
            db.Candidates.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);

        public async Task<OperationResult<Guid>> CreateAsync(CandidateInput input, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(input.Name))
                return OperationResult<Guid>.Fail(FailureKind.Validation, "Name is required.");

            var candidate = new Candidate
            {
                Id = Guid.NewGuid(),
                Name = input.Name.Trim(),
                Description = input.Description,
                ShortBiography = input.ShortBiography,
                Avatar = input.Avatar,
            };
            db.Candidates.Add(candidate);
            await db.SaveChangesAsync(ct);
            return OperationResult<Guid>.Success(candidate.Id);
        }

        /// <summary>Updates details; the avatar is only replaced when <see cref="CandidateInput.Avatar"/> is set.</summary>
        public async Task<OperationResult> UpdateAsync(Guid id, CandidateInput input, CancellationToken ct = default)
        {
            var candidate = await db.Candidates.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (candidate == null) return OperationResult.NotFound("Candidate not found.");

            if (!string.IsNullOrWhiteSpace(input.Name)) candidate.Name = input.Name.Trim();
            candidate.Description = input.Description;
            candidate.ShortBiography = input.ShortBiography;
            if (!string.IsNullOrEmpty(input.Avatar)) candidate.Avatar = input.Avatar;

            await db.SaveChangesAsync(ct);
            return OperationResult.Success();
        }

        public async Task<CandidateVoteSummary> GetVoteSummaryAsync(Guid id, CancellationToken ct = default)
        {
            var votes = await db.Votes.AsNoTracking()
                .Where(v => v.CandidateId == id)
                .Include(v => v.PollingStationArea).ThenInclude(p => p.Area)
                .Include(v => v.PollingStationArea).ThenInclude(p => p.PollingStation)
                .ToListAsync(ct);
            return new CandidateVoteSummary(votes.Sum(v => v.Total), votes);
        }

        /// <summary>Deletes a candidate that has no recorded votes and is not an election contestant.</summary>
        public async Task<OperationResult> DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var candidate = await db.Candidates.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (candidate == null) return OperationResult.NotFound("Candidate not found.");

            if (await db.Votes.AnyAsync(v => v.CandidateId == id, ct))
                return OperationResult.Fail(FailureKind.Conflict, "This candidate has recorded votes and cannot be deleted.");
            if (await db.Contestants.AnyAsync(c => c.CandidateId == id, ct))
                return OperationResult.Fail(FailureKind.Conflict, "This candidate is a contestant in an election and cannot be deleted.");

            db.Candidates.Remove(candidate);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                logger.LogWarning(ex, "Deleting candidate {CandidateId} failed", id);
                return OperationResult.Fail(FailureKind.Conflict, "The candidate is still referenced and cannot be deleted.");
            }
            return OperationResult.Success();
        }
    }
}
