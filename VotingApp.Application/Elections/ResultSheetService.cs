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
using VotingApp.Domain.Entities;

namespace VotingApp.Application.Elections
{
    /// <summary>Figures typed from a paper results sheet.</summary>
    public sealed record SheetFiguresInput(
        int? RegisteredVoters,
        int? BallotsReceived,
        int? BallotsUsed,
        int? BallotsSpoiled,
        int? BallotsUnused,
        int BlankVotes,
        int InvalidVotes,
        IReadOnlyDictionary<Guid, int> ContestantVotes);

    public sealed record StationProgress(
        Guid PollingStationAreaId,
        string MunicipalityId,
        string MunicipalityName,
        string VillageName,
        string StationName,
        Guid? SheetId,
        ResultSheetStatus? Status,
        int ErrorCount,
        int WarningCount,
        int VerificationMismatches,
        string LastMismatchFields);

    public sealed record EntryForm(
        ElectionRound Round,
        PollingStationArea Station,
        IReadOnlyList<Contestant> Ballot,
        ResultSheet Sheet,
        ResultSheetValidation Validation);

    public sealed record VerificationOutcome(bool Matched, IReadOnlyList<string> MismatchedFields);

    /// <summary>
    /// Results-sheet workflow: Entered (first entry) → Verified (independent second entry by another
    /// user that matches) → Approved (municipal tabulation, no arithmetic errors) → Locked (national).
    /// Users only reach sheets inside their municipality scope.
    /// </summary>
    public class ResultSheetService
    {
        private static readonly ElectionStatus[] EntryOpenStatuses = { ElectionStatus.Counting, ElectionStatus.Provisional };

        private readonly IAppDbContext db;
        private readonly ICurrentUser currentUser;
        private readonly UserScopeService scopes;
        private readonly ResultSheetValidator validator;
        private readonly ElectionResultsService results;

        public ResultSheetService(IAppDbContext db, ICurrentUser currentUser, UserScopeService scopes,
            ResultSheetValidator validator, ElectionResultsService results)
        {
            this.db = db;
            this.currentUser = currentUser;
            this.scopes = scopes;
            this.validator = validator;
            this.results = results;
        }

        // ---------- Queries ----------

        /// <summary>All polling stations in scope with the state of their sheet for this round.</summary>
        public async Task<List<StationProgress>> ListStationsAsync(Guid roundId, string municipalityId = null, CancellationToken ct = default)
        {
            var scope = await scopes.GetCurrentScopeAsync(ct: ct);
            var ballot = await BallotIdsAsync(roundId, ct);

            var stations = await db.PollingStationAreas.AsNoTracking()
                .Select(p => new { p.Id, p.AreaId, Village = p.Area.Name, Station = p.PollingStation.Name })
                .ToListAsync(ct);
            var municipalities = await db.Areas.AsNoTracking()
                .Where(a => a.Level == AreaHierarchy.Municipality)
                .ToDictionaryAsync(a => a.Id, a => a.Name, ct);
            var sheets = await db.ResultSheets.AsNoTracking()
                .Where(s => s.ElectionRoundId == roundId)
                .Include(s => s.Entries)
                .ToDictionaryAsync(s => s.PollingStationAreaId, ct);

            var list = new List<StationProgress>();
            foreach (var st in stations)
            {
                if (st.AreaId == null || st.AreaId.Length < AreaHierarchy.MunicipalityCodeLength || !scope.Allows(st.AreaId)) continue;
                var muni = AreaHierarchy.MunicipalityIdOf(st.AreaId);
                if (!string.IsNullOrEmpty(municipalityId) && muni != municipalityId) continue;

                sheets.TryGetValue(st.Id, out var sheet);
                var validation = sheet == null ? null : validator.Validate(sheet.ToFigures(), ballot);
                list.Add(new StationProgress(st.Id, muni, municipalities.TryGetValue(muni, out var mName) ? mName : muni,
                    st.Village, st.Station, sheet?.Id, sheet?.Status,
                    validation?.Issues.Count(i => i.Severity == IssueSeverity.Error) ?? 0,
                    validation?.Issues.Count(i => i.Severity == IssueSeverity.Warning) ?? 0,
                    sheet?.VerificationMismatches ?? 0, sheet?.LastMismatchFields));
            }
            return list.OrderBy(s => s.MunicipalityName).ThenBy(s => s.VillageName).ThenBy(s => s.StationName).ToList();
        }

        public async Task<OperationResult<EntryForm>> GetEntryFormAsync(Guid roundId, Guid pollingStationAreaId, CancellationToken ct = default)
        {
            var round = await db.ElectionRounds.AsNoTracking()
                .Include(r => r.Election)
                .Include(r => r.Contestants).ThenInclude(rc => rc.Contestant)
                .FirstOrDefaultAsync(r => r.Id == roundId, ct);
            if (round == null) return OperationResult<EntryForm>.NotFound("Round not found.");

            var station = await db.PollingStationAreas.AsNoTracking()
                .Include(p => p.Area).Include(p => p.PollingStation)
                .FirstOrDefaultAsync(p => p.Id == pollingStationAreaId, ct);
            if (station == null) return OperationResult<EntryForm>.NotFound("Polling station not found.");
            if (!(await scopes.GetCurrentScopeAsync(ct: ct)).Allows(station.AreaId))
                return OperationResult<EntryForm>.Fail(FailureKind.Forbidden, "This polling station is outside your assigned municipalities.");

            var sheet = await db.ResultSheets.AsNoTracking().Include(s => s.Entries)
                .FirstOrDefaultAsync(s => s.ElectionRoundId == roundId && s.PollingStationAreaId == pollingStationAreaId, ct);
            var ballot = round.Contestants.OrderBy(rc => rc.BallotPosition).Select(rc => rc.Contestant).ToList();
            var validation = sheet == null ? null : validator.Validate(sheet.ToFigures(), ballot.Select(c => c.Id));
            return OperationResult<EntryForm>.Success(new EntryForm(round, station, ballot, sheet, validation));
        }

        // ---------- Commands ----------

        /// <summary>First entry (or correction before verification). Saves even with arithmetic errors, which then block approval.</summary>
        public async Task<OperationResult<ResultSheetValidation>> SubmitEntryAsync(Guid roundId, Guid pollingStationAreaId, SheetFiguresInput input, CancellationToken ct = default)
        {
            var form = await GetEntryFormAsync(roundId, pollingStationAreaId, ct);
            if (!form.Succeeded) return OperationResult<ResultSheetValidation>.Fail(form.Failure, form.Errors.ToArray());
            if (!EntryOpenStatuses.Contains(form.Value.Round.Status))
                return OperationResult<ResultSheetValidation>.Fail(FailureKind.Conflict, "Result entry is not open for this round (it must be in Counting or Provisional).");

            var inputError = CheckInput(input, form.Value.Ballot);
            if (inputError != null) return OperationResult<ResultSheetValidation>.Fail(FailureKind.Validation, inputError);

            var sheet = await db.ResultSheets.Include(s => s.Entries)
                .FirstOrDefaultAsync(s => s.ElectionRoundId == roundId && s.PollingStationAreaId == pollingStationAreaId, ct);
            if (sheet == null)
            {
                sheet = new ResultSheet { Id = Guid.NewGuid(), ElectionRoundId = roundId, PollingStationAreaId = pollingStationAreaId };
                db.ResultSheets.Add(sheet);
            }
            else if (sheet.Status > ResultSheetStatus.Entered)
            {
                return OperationResult<ResultSheetValidation>.Fail(FailureKind.Conflict,
                    $"This sheet is already {sheet.Status}. Ask a supervisor to reopen it before changing figures.");
            }

            Apply(sheet, input);
            sheet.Status = ResultSheetStatus.Entered;
            sheet.EnteredByUserId = currentUser.UserId;
            sheet.EnteredAt = DateTime.UtcNow;
            sheet.LastMismatchFields = null;

            var saved = await SaveAsync(ct);
            if (!saved.Succeeded) return OperationResult<ResultSheetValidation>.Fail(saved.Failure, saved.Errors.ToArray());
            return OperationResult<ResultSheetValidation>.Success(validator.Validate(sheet.ToFigures(), form.Value.Ballot.Select(c => c.Id)));
        }

        /// <summary>
        /// Blind second entry by a different user. If every figure matches the first entry the sheet becomes
        /// Verified; otherwise the mismatched fields are recorded for a supervisor and the sheet stays Entered.
        /// </summary>
        public async Task<OperationResult<VerificationOutcome>> SubmitVerificationAsync(Guid sheetId, SheetFiguresInput input, CancellationToken ct = default)
        {
            var sheet = await LoadSheetInScopeAsync(sheetId, ct);
            if (!sheet.Succeeded) return OperationResult<VerificationOutcome>.Fail(sheet.Failure, sheet.Errors.ToArray());
            var s = sheet.Value;

            if (s.Status != ResultSheetStatus.Entered)
                return OperationResult<VerificationOutcome>.Fail(FailureKind.Conflict, $"Only entered sheets can be verified (this one is {s.Status}).");
            if (s.EnteredByUserId == currentUser.UserId)
                return OperationResult<VerificationOutcome>.Fail(FailureKind.Forbidden, "The second entry must be made by a different person than the first.");

            var ballot = await BallotAsync(s.ElectionRoundId, ct);
            var inputError = CheckInput(input, ballot);
            if (inputError != null) return OperationResult<VerificationOutcome>.Fail(FailureKind.Validation, inputError);

            var mismatches = Compare(s, input, ballot);
            if (mismatches.Count == 0)
            {
                s.Status = ResultSheetStatus.Verified;
                s.VerifiedByUserId = currentUser.UserId;
                s.VerifiedAt = DateTime.UtcNow;
                s.LastMismatchFields = null;
            }
            else
            {
                s.VerificationMismatches++;
                s.LastMismatchFields = string.Join(", ", mismatches);
            }

            var saved = await SaveAsync(ct);
            if (!saved.Succeeded) return OperationResult<VerificationOutcome>.Fail(saved.Failure, saved.Errors.ToArray());
            InvalidateIfCounted(s);
            return OperationResult<VerificationOutcome>.Success(new VerificationOutcome(mismatches.Count == 0, mismatches));
        }

        public async Task<OperationResult> ApproveAsync(Guid sheetId, CancellationToken ct = default)
        {
            var sheet = await LoadSheetInScopeAsync(sheetId, ct);
            if (!sheet.Succeeded) return sheet;
            var s = sheet.Value;
            if (s.Status != ResultSheetStatus.Verified)
                return OperationResult.Fail(FailureKind.Conflict, $"Only verified sheets can be approved (this one is {s.Status}).");

            var validation = validator.Validate(s.ToFigures(), (await BallotAsync(s.ElectionRoundId, ct)).Select(c => c.Id));
            if (validation.HasErrors)
                return OperationResult.Fail(FailureKind.Validation,
                    validation.Issues.Where(i => i.Severity == IssueSeverity.Error).Select(i => i.Message).ToArray());

            s.Status = ResultSheetStatus.Approved;
            s.ApprovedByUserId = currentUser.UserId;
            s.ApprovedAt = DateTime.UtcNow;
            var saved = await SaveAsync(ct);
            if (saved.Succeeded) results.Invalidate(s.ElectionRoundId);
            return saved;
        }

        /// <summary>Sends a verified or approved sheet back to Entered, recording the reason. Locked sheets cannot be reopened.</summary>
        public async Task<OperationResult> ReopenAsync(Guid sheetId, string reason, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return OperationResult.Fail(FailureKind.Validation, "A reason is required to reopen a sheet.");
            var sheet = await LoadSheetInScopeAsync(sheetId, ct);
            if (!sheet.Succeeded) return sheet;
            var s = sheet.Value;
            if (s.Status == ResultSheetStatus.Locked)
                return OperationResult.Fail(FailureKind.Conflict, "Locked sheets are part of the national tabulation and cannot be reopened here.");
            if (s.Status < ResultSheetStatus.Verified)
                return OperationResult.Fail(FailureKind.Conflict, "The sheet is not verified or approved.");

            var wasCounted = s.Status >= ResultSheetStatus.Approved;
            s.Status = ResultSheetStatus.Entered;
            s.VerifiedByUserId = null;
            s.VerifiedAt = null;
            s.ApprovedByUserId = null;
            s.ApprovedAt = null;
            var note = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC] Reopened: {reason.Trim()}";
            s.Notes = string.IsNullOrEmpty(s.Notes) ? note : s.Notes + Environment.NewLine + note;
            if (s.Notes.Length > 2000) s.Notes = s.Notes.Substring(s.Notes.Length - 2000);

            var saved = await SaveAsync(ct);
            if (saved.Succeeded && wasCounted) results.Invalidate(s.ElectionRoundId);
            return saved;
        }

        /// <summary>National tabulation: locks every approved sheet of the round. Requires national scope.</summary>
        public async Task<OperationResult<int>> LockApprovedAsync(Guid roundId, CancellationToken ct = default)
        {
            if (!(await scopes.GetCurrentScopeAsync(nationalWhenUnassigned: true, ct)).IsNational)
                return OperationResult<int>.Fail(FailureKind.Forbidden, "Locking is done at national level.");

            var sheets = await db.ResultSheets.Where(s => s.ElectionRoundId == roundId && s.Status == ResultSheetStatus.Approved).ToListAsync(ct);
            var now = DateTime.UtcNow;
            foreach (var s in sheets)
            {
                s.Status = ResultSheetStatus.Locked;
                s.LockedByUserId = currentUser.UserId;
                s.LockedAt = now;
            }
            var saved = await SaveAsync(ct);
            if (!saved.Succeeded) return OperationResult<int>.Fail(saved.Failure, saved.Errors.ToArray());
            results.Invalidate(roundId);
            return OperationResult<int>.Success(sheets.Count);
        }

        // ---------- Helpers ----------

        private async Task<OperationResult<ResultSheet>> LoadSheetInScopeAsync(Guid sheetId, CancellationToken ct)
        {
            var sheet = await db.ResultSheets.Include(s => s.Entries).Include(s => s.PollingStationArea)
                .FirstOrDefaultAsync(s => s.Id == sheetId, ct);
            if (sheet == null) return OperationResult<ResultSheet>.NotFound("Result sheet not found.");
            if (!(await scopes.GetCurrentScopeAsync(ct: ct)).Allows(sheet.PollingStationArea.AreaId))
                return OperationResult<ResultSheet>.Fail(FailureKind.Forbidden, "This polling station is outside your assigned municipalities.");
            return OperationResult<ResultSheet>.Success(sheet);
        }

        private Task<List<Contestant>> BallotAsync(Guid roundId, CancellationToken ct) =>
            db.RoundContestants.AsNoTracking().Where(rc => rc.ElectionRoundId == roundId)
                .OrderBy(rc => rc.BallotPosition).Select(rc => rc.Contestant).ToListAsync(ct);

        private Task<List<Guid>> BallotIdsAsync(Guid roundId, CancellationToken ct) =>
            db.RoundContestants.AsNoTracking().Where(rc => rc.ElectionRoundId == roundId).Select(rc => rc.ContestantId).ToListAsync(ct);

        private static string CheckInput(SheetFiguresInput input, IReadOnlyCollection<Contestant> ballot)
        {
            if (input == null) return "Figures are required.";
            var numbers = new[] { input.RegisteredVoters, input.BallotsReceived, input.BallotsUsed, input.BallotsSpoiled, input.BallotsUnused, input.BlankVotes, input.InvalidVotes };
            if (numbers.Any(n => n < 0) || (input.ContestantVotes?.Values.Any(v => v < 0) ?? false))
                return "Figures cannot be negative.";
            var ballotIds = ballot.Select(c => c.Id).ToHashSet();
            if (input.ContestantVotes == null || input.ContestantVotes.Keys.Any(id => !ballotIds.Contains(id)))
                return "Votes were submitted for a contestant who is not on this round's ballot.";
            if (ballotIds.Any(id => !input.ContestantVotes.ContainsKey(id)))
                return "Enter a number (0 if none) for every contestant on the ballot.";
            return null;
        }

        private void Apply(ResultSheet sheet, SheetFiguresInput input)
        {
            sheet.RegisteredVoters = input.RegisteredVoters;
            sheet.BallotsReceived = input.BallotsReceived;
            sheet.BallotsUsed = input.BallotsUsed;
            sheet.BallotsSpoiled = input.BallotsSpoiled;
            sheet.BallotsUnused = input.BallotsUnused;
            sheet.BlankVotes = input.BlankVotes;
            sheet.InvalidVotes = input.InvalidVotes;

            foreach (var (contestantId, votes) in input.ContestantVotes)
            {
                var entry = sheet.Entries.FirstOrDefault(e => e.ContestantId == contestantId);
                if (entry == null)
                {
                    entry = new ResultSheetEntry { Id = Guid.NewGuid(), ResultSheetId = sheet.Id, ContestantId = contestantId };
                    sheet.Entries.Add(entry);
                    db.ResultSheetEntries.Add(entry);
                }
                entry.Votes = votes;
            }
        }

        private static List<string> Compare(ResultSheet sheet, SheetFiguresInput input, IEnumerable<Contestant> ballot)
        {
            var diff = new List<string>();
            void Check(string name, int? stored, int? entered) { if (stored != entered) diff.Add(name); }
            Check("Registered voters", sheet.RegisteredVoters, input.RegisteredVoters);
            Check("Ballots received", sheet.BallotsReceived, input.BallotsReceived);
            Check("Ballots used", sheet.BallotsUsed, input.BallotsUsed);
            Check("Spoiled ballots", sheet.BallotsSpoiled, input.BallotsSpoiled);
            Check("Unused ballots", sheet.BallotsUnused, input.BallotsUnused);
            Check("Blank votes", sheet.BlankVotes, input.BlankVotes);
            Check("Invalid votes", sheet.InvalidVotes, input.InvalidVotes);
            foreach (var c in ballot)
            {
                var stored = sheet.Entries.FirstOrDefault(e => e.ContestantId == c.Id)?.Votes;
                input.ContestantVotes.TryGetValue(c.Id, out var entered);
                Check(c.Name, stored, entered);
            }
            return diff;
        }

        private void InvalidateIfCounted(ResultSheet sheet)
        {
            if (sheet.Status >= ResultSheetStatus.Verified) results.Invalidate(sheet.ElectionRoundId);
        }

        private async Task<OperationResult> SaveAsync(CancellationToken ct)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                return OperationResult.Success();
            }
            catch (DbUpdateConcurrencyException)
            {
                return OperationResult.Fail(FailureKind.Conflict, "Someone else changed this sheet at the same time. Reload it and try again.");
            }
            catch (DbUpdateException)
            {
                return OperationResult.Fail(FailureKind.Conflict, "The sheet could not be saved because it conflicts with another entry. Reload and try again.");
            }
        }
    }
}
