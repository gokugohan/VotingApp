using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VotingApp.Application.Abstractions;
using VotingApp.Application.Common;
using VotingApp.Domain.Elections;

namespace VotingApp.Application.Elections
{
    public sealed record PartyInput(string Name, string Acronym, PartyKind Kind, string Notes);

    public sealed record PartySummary(Party Party, int Elections);

    /// <summary>One election a party contested, with its published (approved) result.</summary>
    public sealed record PartyHistoryRow(
        Guid ElectionId, string ElectionName, DateTime ElectionDate, Guid RoundId, ElectionStatus RoundStatus,
        string NameOnBallot, long Votes, decimal PercentOfValid, int? Seats, decimal ReportingPercent);

    /// <summary>
    /// Lasting records of parties and coalitions, so each election's contestant can be linked to the
    /// same party as in earlier elections and results compared over time.
    /// </summary>
    public class PartyService
    {
        private readonly IAppDbContext db;
        private readonly ElectionResultsService results;

        public PartyService(IAppDbContext db, ElectionResultsService results)
        {
            this.db = db;
            this.results = results;
        }

        public async Task<List<PartySummary>> ListAsync(CancellationToken ct = default)
        {
            var parties = await db.Parties.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);
            var counts = await db.Contestants.Where(c => c.PartyId != null)
                .GroupBy(c => c.PartyId)
                .Select(g => new { PartyId = g.Key.Value, Count = g.Select(c => c.ElectionId).Distinct().Count() })
                .ToDictionaryAsync(x => x.PartyId, x => x.Count, ct);
            return parties.Select(p => new PartySummary(p, counts.TryGetValue(p.Id, out var n) ? n : 0)).ToList();
        }

        public Task<Party> GetAsync(Guid id, CancellationToken ct = default) =>
            db.Parties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

        public async Task<OperationResult<Guid>> CreateAsync(PartyInput input, CancellationToken ct = default)
        {
            var error = await CheckAsync(null, input, ct);
            if (error != null) return OperationResult<Guid>.Fail(FailureKind.Validation, error);
            var party = New(input);
            db.Parties.Add(party);
            await db.SaveChangesAsync(ct);
            return OperationResult<Guid>.Success(party.Id);
        }

        public async Task<OperationResult> UpdateAsync(Guid id, PartyInput input, CancellationToken ct = default)
        {
            var party = await db.Parties.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (party == null) return OperationResult.NotFound("Party not found.");
            var error = await CheckAsync(id, input, ct);
            if (error != null) return OperationResult.Fail(FailureKind.Validation, error);
            party.Name = input.Name.Trim();
            party.Acronym = string.IsNullOrWhiteSpace(input.Acronym) ? null : input.Acronym.Trim();
            party.Kind = input.Kind;
            party.Notes = input.Notes;
            await db.SaveChangesAsync(ct);
            return OperationResult.Success();
        }

        /// <summary>Deletes a party that has never been on a ballot.</summary>
        public async Task<OperationResult> DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var party = await db.Parties.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (party == null) return OperationResult.NotFound("Party not found.");
            if (await db.Contestants.AnyAsync(c => c.PartyId == id, ct))
                return OperationResult.Fail(FailureKind.Conflict, "This party has been on a ballot; its record is kept for the results history.");
            db.Parties.Remove(party);
            await db.SaveChangesAsync(ct);
            return OperationResult.Success();
        }

        /// <summary>
        /// The registry record for a name, matched ignoring case and surrounding spaces; created if missing.
        /// The caller saves the changes.
        /// </summary>
        public async Task<Party> FindOrCreateAsync(string name, string acronym, PartyKind kind, CancellationToken ct = default)
        {
            var normalized = (name ?? string.Empty).Trim();
            var existing = await db.Parties.FirstOrDefaultAsync(p => p.Name.ToUpper() == normalized.ToUpper(), ct);
            if (existing != null) return existing;
            var party = New(new PartyInput(normalized, acronym, kind, null));
            db.Parties.Add(party);
            return party;
        }

        /// <summary>Links (or unlinks) an election's contestant to a party record, e.g. to fix a name spelled differently.</summary>
        public async Task<OperationResult> LinkContestantAsync(Guid contestantId, Guid? partyId, CancellationToken ct = default)
        {
            var contestant = await db.Contestants.Include(c => c.Election).FirstOrDefaultAsync(c => c.Id == contestantId, ct);
            if (contestant == null) return OperationResult.NotFound("Contestant not found.");
            if (contestant.Type == ContestantType.Candidate)
                return OperationResult.Fail(FailureKind.Validation, "Presidential candidates are linked through their candidate profile.");
            if (partyId.HasValue && !await db.Parties.AnyAsync(p => p.Id == partyId, ct))
                return OperationResult.NotFound("Party not found.");
            contestant.PartyId = partyId;
            await db.SaveChangesAsync(ct);
            foreach (var roundId in await db.ElectionRounds.Where(r => r.ElectionId == contestant.ElectionId).Select(r => r.Id).ToListAsync(ct))
            {
                results.Invalidate(roundId);
            }
            return OperationResult.Success();
        }

        /// <summary>Every election the party contested (newest first) with its approved result.</summary>
        public async Task<List<PartyHistoryRow>> GetHistoryAsync(Guid partyId, CancellationToken ct = default)
        {
            var appearances = await db.RoundContestants.AsNoTracking()
                .Where(rc => rc.Contestant.PartyId == partyId && rc.ElectionRound.Status >= ElectionStatus.Counting)
                .Select(rc => new
                {
                    rc.ContestantId,
                    rc.ElectionRoundId,
                    NameOnBallot = rc.Contestant.Name,
                    rc.ElectionRound.ElectionId,
                    ElectionName = rc.ElectionRound.Election.Name,
                    rc.ElectionRound.Election.ElectionDate,
                })
                .ToListAsync(ct);

            var rows = new List<PartyHistoryRow>();
            foreach (var a in appearances)
            {
                var summary = await results.GetSummaryAsync(a.ElectionRoundId, ResultSheetStatus.Approved, ct);
                var mine = summary?.Contestants.FirstOrDefault(c => c.ContestantId == a.ContestantId);
                if (summary == null || mine == null) continue;
                rows.Add(new PartyHistoryRow(a.ElectionId, a.ElectionName, a.ElectionDate, a.ElectionRoundId, summary.RoundStatus,
                    a.NameOnBallot, mine.Votes, mine.PercentOfValid, mine.Seats, summary.ReportingPercent));
            }
            return rows.OrderByDescending(r => r.ElectionDate).ToList();
        }

        private async Task<string> CheckAsync(Guid? id, PartyInput input, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(input.Name)) return "Name is required.";
            var name = input.Name.Trim();
            if (await db.Parties.AnyAsync(p => p.Id != id && p.Name.ToUpper() == name.ToUpper(), ct))
                return "A party with this name already exists.";
            return null;
        }

        private static Party New(PartyInput input) => new()
        {
            Id = Guid.NewGuid(),
            Name = input.Name.Trim(),
            Acronym = string.IsNullOrWhiteSpace(input.Acronym) ? null : input.Acronym.Trim(),
            Kind = input.Kind,
            Notes = input.Notes,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
