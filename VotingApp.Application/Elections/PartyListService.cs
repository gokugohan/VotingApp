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
    public sealed record ListEntry(string FullName, Gender Gender);

    /// <summary>A party's closed list: effective candidates in order, then substitutes in order.</summary>
    public sealed record PartyList(
        Contestant Contestant,
        Election Election,
        IReadOnlyList<PartyListCandidate> Effective,
        IReadOnlyList<PartyListCandidate> Substitutes,
        IReadOnlyList<ValidationIssue> Issues)
    {
        public bool IsValid => Effective.Count > 0 && Issues.All(i => i.Severity != IssueSeverity.Error);
    }

    /// <summary>Readiness of one party's list, for the election overview.</summary>
    public sealed record PartyListStatus(Guid ContestantId, int Effective, int Substitutes, bool IsValid, string Problem);

    /// <summary>
    /// A seat holder. <see cref="Replaces"/> is set when the person took the seat of someone who stepped
    /// down; <see cref="IsSubstitute"/> when they come from the substitutes list.
    /// </summary>
    public sealed record ElectedMember(int Position, string FullName, Gender Gender,
        bool IsSubstitute = false, string Replaces = null, Guid CandidateId = default);

    public sealed record StepDownInput(Guid CandidateId, DateTime Date, StepDownReason Reason, string Note);

    /// <summary>Seats won by a party and the people who fill them from the top of its list.</summary>
    public sealed record PartyElected(Guid ContestantId, string Name, int Seats, IReadOnlyList<ElectedMember> Members)
    {
        /// <summary>Seats that cannot be filled because the list is shorter than the seats won.</summary>
        public int Unfilled => Seats - Members.Count;
    }

    /// <summary>
    /// Closed party lists for parliamentary elections: entry (pasted, one person per line),
    /// gender-quota validation, readiness checks, and who is elected from a seat allocation.
    /// </summary>
    public class PartyListService
    {
        private readonly IAppDbContext db;
        private readonly PartyListValidator validator;
        private readonly RoundResultsCache cache;

        public PartyListService(IAppDbContext db, PartyListValidator validator, RoundResultsCache cache)
        {
            this.db = db;
            this.validator = validator;
            this.cache = cache;
        }

        /// <summary>Elected members for a round's projected seats, cached with the round's other results.</summary>
        public Task<List<PartyElected>> GetElectedForRoundAsync(Guid roundId, IReadOnlyList<(Guid ContestantId, string Name, int Seats)> seats, CancellationToken ct = default)
        {
            var key = "elected:" + string.Join(",", seats.Where(s => s.Seats > 0).OrderBy(s => s.ContestantId).Select(s => $"{s.ContestantId:N}={s.Seats}"));
            return cache.GetOrBuildAsync(roundId, key, () => GetElectedAsync(seats, CancellationToken.None));
        }

        /// <summary>
        /// Parses pasted lines "Full name, F" / "Full name; M" (also "Female"/"Male", tab-separated).
        /// Blank lines are ignored; problems are reported with their line number.
        /// </summary>
        public static (List<ListEntry> Entries, List<string> Errors) Parse(string text)
        {
            var entries = new List<ListEntry>();
            var errors = new List<string>();
            var lines = (text ?? string.Empty).Replace("\r", string.Empty).Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0) continue;
                var cut = line.LastIndexOfAny(new[] { ',', ';', '\t' });
                if (cut <= 0)
                {
                    errors.Add($"Line {i + 1}: add the gender after the name, e.g. \"{line}, F\".");
                    continue;
                }
                var name = line.Substring(0, cut).Trim();
                var gender = line.Substring(cut + 1).Trim().ToUpperInvariant() switch
                {
                    "F" or "FEMALE" or "W" or "WOMAN" or "FETO" => Gender.Female,
                    "M" or "MALE" or "MAN" or "MANE" => Gender.Male,
                    _ => (Gender?)null,
                };
                if (name.Length == 0) errors.Add($"Line {i + 1}: the name is missing.");
                else if (name.Length > 200) errors.Add($"Line {i + 1}: the name is longer than 200 characters.");
                else if (gender == null) errors.Add($"Line {i + 1}: gender must be F or M.");
                else entries.Add(new ListEntry(name, gender.Value));
            }
            return (entries, errors);
        }

        /// <summary>Formats a list back to the pasted form, for editing.</summary>
        public static string Format(IEnumerable<PartyListCandidate> candidates) =>
            string.Join(Environment.NewLine, candidates.OrderBy(c => c.Position)
                .Select(c => $"{c.FullName}, {(c.Gender == Gender.Female ? "F" : "M")}"));

        public async Task<PartyList> GetAsync(Guid contestantId, CancellationToken ct = default)
        {
            var contestant = await db.Contestants.AsNoTracking()
                .Include(c => c.Election).ThenInclude(e => e.Rounds)
                .Include(c => c.ListCandidates)
                .FirstOrDefaultAsync(c => c.Id == contestantId, ct);
            if (contestant == null) return null;
            var effective = contestant.ListCandidates.Where(c => !c.IsSubstitute).OrderBy(c => c.Position).ToList();
            var substitutes = contestant.ListCandidates.Where(c => c.IsSubstitute).OrderBy(c => c.Position).ToList();
            return new PartyList(contestant, contestant.Election, effective, substitutes, Validate(effective, contestant.Election));
        }

        /// <summary>
        /// Replaces a party's list. Allowed only while the election is in set-up. Lists that break the
        /// quota can be saved as drafts; the election cannot be opened until every list is valid.
        /// </summary>
        public async Task<OperationResult<IReadOnlyList<ValidationIssue>>> SaveAsync(
            Guid contestantId, IReadOnlyList<ListEntry> effective, IReadOnlyList<ListEntry> substitutes, CancellationToken ct = default)
        {
            var contestant = await db.Contestants.Include(c => c.Election).Include(c => c.ListCandidates)
                .FirstOrDefaultAsync(c => c.Id == contestantId, ct);
            if (contestant == null) return OperationResult<IReadOnlyList<ValidationIssue>>.NotFound("Party not found.");
            if (contestant.Election.Type != ElectionType.Parliamentary)
                return OperationResult<IReadOnlyList<ValidationIssue>>.Fail(FailureKind.Validation, "Only parliamentary elections have party lists.");
            if (contestant.Election.Status != ElectionStatus.Setup)
                return OperationResult<IReadOnlyList<ValidationIssue>>.Fail(FailureKind.Conflict, "Lists can only be changed while the election is in set-up.");

            db.PartyListCandidates.RemoveRange(contestant.ListCandidates.ToList());
            var rows = effective.Select((e, i) => New(contestant.Id, i + 1, e, false))
                .Concat(substitutes.Select((e, i) => New(contestant.Id, i + 1, e, true)))
                .ToList();
            db.PartyListCandidates.AddRange(rows);
            await db.SaveChangesAsync(ct);

            return OperationResult<IReadOnlyList<ValidationIssue>>.Success(Validate(rows.Where(r => !r.IsSubstitute).ToList(), contestant.Election));
        }

        /// <summary>Readiness of every party list in an election.</summary>
        public async Task<List<PartyListStatus>> GetStatusesAsync(Guid electionId, CancellationToken ct = default)
        {
            var election = await db.Elections.AsNoTracking().FirstOrDefaultAsync(e => e.Id == electionId, ct);
            if (election == null || election.Type != ElectionType.Parliamentary) return new List<PartyListStatus>();
            var contestants = await db.Contestants.AsNoTracking().Where(c => c.ElectionId == electionId)
                .Include(c => c.ListCandidates).ToListAsync(ct);
            return contestants.Select(c =>
            {
                var effective = c.ListCandidates.Where(x => !x.IsSubstitute).OrderBy(x => x.Position).ToList();
                var issues = Validate(effective, election);
                var problem = effective.Count == 0
                    ? "No candidates on the list."
                    : issues.FirstOrDefault(i => i.Severity == IssueSeverity.Error)?.Message;
                return new PartyListStatus(c.Id, effective.Count, c.ListCandidates.Count(x => x.IsSubstitute), problem == null, problem);
            }).ToList();
        }

        /// <summary>
        /// Who holds each party's seats. Seats go to the first N effective candidates. When one of them
        /// has stepped down, the seat passes to the next available person: first the effective
        /// candidates below the elected ones, then the substitutes, in list order, skipping anyone who
        /// has also stepped down. Seats nobody can fill are reported as <see cref="PartyElected.Unfilled"/>.
        /// </summary>
        public async Task<List<PartyElected>> GetElectedAsync(IEnumerable<(Guid ContestantId, string Name, int Seats)> seats, CancellationToken ct = default)
        {
            var winners = seats.Where(s => s.Seats > 0).ToList();
            var ids = winners.Select(w => w.ContestantId).ToList();
            var people = (await db.PartyListCandidates.AsNoTracking()
                    .Where(c => ids.Contains(c.ContestantId))
                    .ToListAsync(ct))
                .ToLookup(c => c.ContestantId);

            var result = new List<PartyElected>();
            foreach (var w in winners.OrderByDescending(w => w.Seats).ThenBy(w => w.Name))
            {
                result.Add(new PartyElected(w.ContestantId, w.Name, w.Seats, SeatHolders(people[w.ContestantId], w.Seats)));
            }
            return result;
        }

        /// <summary>Applies the list order and stand-downs to one party's seats (pure, for testing).</summary>
        public static List<ElectedMember> SeatHolders(IEnumerable<PartyListCandidate> list, int seats)
        {
            var ordered = list.OrderBy(c => c.IsSubstitute).ThenBy(c => c.Position).ToList();
            var originallyElected = ordered.Where(c => !c.IsSubstitute).Take(seats).ToList();
            var nextInLine = new Queue<PartyListCandidate>(ordered.Except(originallyElected).Where(c => !c.HasSteppedDown));

            // Vacancies are filled in the order they arose, so a replacement keeps the seat they took
            // when someone else steps down later.
            var replacement = new Dictionary<PartyListCandidate, PartyListCandidate>();
            foreach (var vacancy in originallyElected.Where(c => c.HasSteppedDown).OrderBy(c => c.SteppedDownOn).ThenBy(c => c.Position))
            {
                if (nextInLine.Count > 0) replacement[vacancy] = nextInLine.Dequeue();
            }

            var members = new List<ElectedMember>();
            foreach (var person in originallyElected)
            {
                if (!person.HasSteppedDown) members.Add(Member(person, null));
                else if (replacement.TryGetValue(person, out var taker)) members.Add(Member(taker, person.FullName));
            }
            return members;
        }

        private static ElectedMember Member(PartyListCandidate c, string replaces) =>
            new(c.Position, c.FullName, c.Gender, c.IsSubstitute, replaces, c.Id);

        /// <summary>
        /// Records that a person on a list stepped down after the election. Allowed once results are
        /// provisional or certified; the public elected members update straight away.
        /// </summary>
        public async Task<OperationResult> StepDownAsync(StepDownInput input, CancellationToken ct = default)
        {
            var person = await db.PartyListCandidates.Include(c => c.Contestant).ThenInclude(c => c.Election).ThenInclude(e => e.Rounds)
                .FirstOrDefaultAsync(c => c.Id == input.CandidateId, ct);
            if (person == null) return OperationResult.NotFound("Person not found on the list.");
            var election = person.Contestant.Election;
            if (election.Status is not (ElectionStatus.Provisional or ElectionStatus.Certified))
                return OperationResult.Fail(FailureKind.Conflict, "Stepping down can only be recorded once results are provisional or certified.");
            if (person.HasSteppedDown)
                return OperationResult.Fail(FailureKind.Conflict, $"{person.FullName} is already recorded as stepped down.");
            if (!Enum.IsDefined(input.Reason))
                return OperationResult.Fail(FailureKind.Validation, "Choose a reason.");
            if (input.Date.Date < election.ElectionDate.Date)
                return OperationResult.Fail(FailureKind.Validation, "The date cannot be before the election.");
            if (input.Date.Date > DateTime.UtcNow.Date.AddDays(1))
                return OperationResult.Fail(FailureKind.Validation, "The date cannot be in the future.");
            var note = input.Note?.Trim();
            if (note?.Length > 500)
                return OperationResult.Fail(FailureKind.Validation, "The note is longer than 500 characters.");

            person.SteppedDownOn = input.Date.Date;
            person.StepDownReason = input.Reason;
            person.StepDownNote = string.IsNullOrEmpty(note) ? null : note;
            await db.SaveChangesAsync(ct);
            foreach (var round in election.Rounds) cache.Invalidate(round.Id);
            return OperationResult.Success();
        }

        /// <summary>Undoes a stand-down (a recording mistake, or a member returning from government).</summary>
        public async Task<OperationResult> ReinstateAsync(Guid candidateId, CancellationToken ct = default)
        {
            var person = await db.PartyListCandidates.Include(c => c.Contestant).ThenInclude(c => c.Election).ThenInclude(e => e.Rounds)
                .FirstOrDefaultAsync(c => c.Id == candidateId, ct);
            if (person == null) return OperationResult.NotFound("Person not found on the list.");
            if (!person.HasSteppedDown) return OperationResult.Fail(FailureKind.Conflict, $"{person.FullName} has not stepped down.");

            person.SteppedDownOn = null;
            person.StepDownReason = null;
            person.StepDownNote = null;
            await db.SaveChangesAsync(ct);
            foreach (var round in person.Contestant.Election.Rounds) cache.Invalidate(round.Id);
            return OperationResult.Success();
        }

        private IReadOnlyList<ValidationIssue> Validate(IReadOnlyList<PartyListCandidate> effective, Election election)
        {
            var issues = validator.Validate(effective.Select(c => new ListPosition(c.Position, c.Gender))).ToList();
            if (effective.Count > 0 && election.TotalSeats is int seats && effective.Count < seats)
            {
                issues.Add(new ValidationIssue("LIST_SHORT", IssueSeverity.Warning,
                    $"The list has {effective.Count} effective candidates for {seats} seats; seats beyond the list cannot be filled."));
            }
            return issues;
        }

        private static PartyListCandidate New(Guid contestantId, int position, ListEntry e, bool substitute) => new()
        {
            Id = Guid.NewGuid(),
            ContestantId = contestantId,
            Position = position,
            FullName = e.FullName.Trim(),
            Gender = e.Gender,
            IsSubstitute = substitute,
        };
    }
}
