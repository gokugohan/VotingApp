using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VotingApp.Application.Abstractions;
using VotingApp.Domain.Elections;

namespace VotingApp.Application.Elections
{
    /// <summary>A sheet typed on a device (possibly offline) and sent when the connection allows.</summary>
    public sealed record OfflineSubmission(
        Guid Id, SubmissionKind Kind, Guid RoundId, Guid StationId, DateTime? QueuedAtUtc, SheetFiguresInput Figures);

    /// <summary>
    /// Result of one submission. <see cref="Outcome"/> is "saved", "verified", "mismatch" or "rejected";
    /// <see cref="Replayed"/> is true when the same submission had already been applied.
    /// </summary>
    public sealed record SubmissionOutcome(Guid Id, string Outcome, string Message, bool Replayed = false);

    public sealed record OfflineStation(Guid Id, string Name, string Village, string Municipality, string Stage);

    /// <summary>What a device needs to keep entering results without a connection.</summary>
    public sealed record OfflinePack(
        Guid RoundId, string ElectionName, int RoundNumber, bool EntryOpen,
        IReadOnlyList<BallotContestant> Ballot, IReadOnlyList<OfflineStation> Stations, DateTime GeneratedAtUtc);

    /// <summary>
    /// Offline result entry: the pack a device downloads, and applying queued submissions once it is
    /// back online. Submissions go through <see cref="ResultSheetService"/>, so every online rule still
    /// applies (scope, second entry by another person, sheet stage, election status). Each submission
    /// id is applied at most once.
    /// </summary>
    public class OfflineEntryService
    {
        public const string Saved = "saved", Verified = "verified", Mismatch = "mismatch", Rejected = "rejected";
        private const string Processing = "processing";

        private readonly IAppDbContext db;
        private readonly ICurrentUser currentUser;
        private readonly ResultSheetService sheets;
        private readonly PublicResultsService publicResults;

        public OfflineEntryService(IAppDbContext db, ICurrentUser currentUser, ResultSheetService sheets, PublicResultsService publicResults)
        {
            this.db = db;
            this.currentUser = currentUser;
            this.sheets = sheets;
            this.publicResults = publicResults;
        }

        /// <summary>Ballot and the user's stations that still need a first or second entry.</summary>
        public async Task<OfflinePack> GetPackAsync(Guid roundId, CancellationToken ct = default)
        {
            var round = await db.ElectionRounds.AsNoTracking().Include(r => r.Election).FirstOrDefaultAsync(r => r.Id == roundId, ct);
            if (round == null) return null;
            var stations = (await sheets.ListStationsAsync(roundId, null, ct))
                .Where(s => s.Status == null || s.Status == ResultSheetStatus.Entered)
                .Select(s => new OfflineStation(s.PollingStationAreaId, s.StationName, s.VillageName, s.MunicipalityName,
                    s.Status == null ? "not-received" : "entered"))
                .ToList();
            return new OfflinePack(round.Id, round.Election.Name, round.Number,
                round.Status == ElectionStatus.Counting || round.Status == ElectionStatus.Provisional,
                await publicResults.GetBallotAsync(roundId, ct), stations, DateTime.UtcNow);
        }

        public async Task<SubmissionOutcome> SubmitAsync(OfflineSubmission s, CancellationToken ct = default)
        {
            if (s.Id == Guid.Empty || s.Figures == null)
                return new SubmissionOutcome(s.Id, Rejected, "The submission is incomplete.");

            var previous = await db.SheetSubmissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == s.Id, ct);
            if (previous != null) return Replay(previous, s.Id);

            // Claim the id first: a second copy arriving at the same time fails on the primary key
            // instead of being applied twice.
            var claim = new SheetSubmission
            {
                Id = s.Id, UserId = currentUser.UserId, Kind = s.Kind, ElectionRoundId = s.RoundId,
                PollingStationAreaId = s.StationId, QueuedAtUtc = s.QueuedAtUtc, ReceivedAtUtc = DateTime.UtcNow,
                Outcome = Processing,
            };
            db.SheetSubmissions.Add(claim);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.DiscardChanges();
                var other = await db.SheetSubmissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == s.Id, ct);
                return other != null ? Replay(other, s.Id) : new SubmissionOutcome(s.Id, Rejected, "Could not record the submission; try again.");
            }

            var outcome = await ApplyAsync(s, ct);

            if (outcome.Outcome == Rejected)
            {
                // Not applied: free the id so a corrected copy (or a retry after the cause is fixed) can be sent.
                // Discard whatever a failed save left pending first, so it is not retried here.
                db.DiscardChanges();
                db.SheetSubmissions.Remove(claim);
            }
            else
            {
                claim.Outcome = outcome.Outcome;
                claim.Message = outcome.Message?.Length > 1000 ? outcome.Message.Substring(0, 1000) : outcome.Message;
            }
            await db.SaveChangesAsync(ct);
            return outcome;
        }

        private async Task<SubmissionOutcome> ApplyAsync(OfflineSubmission s, CancellationToken ct)
        {
            if (s.Kind == SubmissionKind.Entry)
            {
                var result = await sheets.SubmitEntryAsync(s.RoundId, s.StationId, s.Figures, ct);
                if (!result.Succeeded) return new SubmissionOutcome(s.Id, Rejected, result.Error);
                return new SubmissionOutcome(s.Id, Saved, result.Value.HasErrors
                    ? "Saved, but the figures do not add up; it cannot be approved until corrected."
                    : "Saved. A second person must now verify it.");
            }

            var sheetId = await sheets.FindSheetIdAsync(s.RoundId, s.StationId, ct);
            if (sheetId == null) return new SubmissionOutcome(s.Id, Rejected, "There is no first entry for this station yet.");
            var verification = await sheets.SubmitVerificationAsync(sheetId.Value, s.Figures, ct);
            if (!verification.Succeeded) return new SubmissionOutcome(s.Id, Rejected, verification.Error);
            return verification.Value.Matched
                ? new SubmissionOutcome(s.Id, Verified, "Verified: both entries match.")
                : new SubmissionOutcome(s.Id, Mismatch, $"The entries differ in: {verification.Value.FieldList}.");
        }

        private SubmissionOutcome Replay(SheetSubmission previous, Guid id)
        {
            if (previous.UserId != currentUser.UserId)
                return new SubmissionOutcome(id, Rejected, "This submission id was already used by another account.");
            if (previous.Outcome == Processing)
                return new SubmissionOutcome(id, Rejected, "This submission is still being processed; sync again in a moment.");
            return new SubmissionOutcome(id, previous.Outcome, previous.Message, Replayed: true);
        }
    }
}
