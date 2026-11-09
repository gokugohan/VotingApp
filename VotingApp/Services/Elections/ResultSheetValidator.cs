using System;
using System.Collections.Generic;
using System.Linq;

namespace VotingApp.Services.Elections
{
    public enum IssueSeverity
    {
        /// <summary>Unusual but possible; show to the supervisor.</summary>
        Warning,
        /// <summary>The numbers do not reconcile; the sheet cannot be approved.</summary>
        Error,
    }

    public sealed record ValidationIssue(string Code, IssueSeverity Severity, string Message);

    public sealed record ResultSheetValidation(IReadOnlyList<ValidationIssue> Issues)
    {
        public bool HasErrors => Issues.Any(i => i.Severity == IssueSeverity.Error);
        public bool HasWarnings => Issues.Any(i => i.Severity == IssueSeverity.Warning);
    }

    /// <summary>The figures on one polling station results sheet, independent of storage.</summary>
    public sealed record ResultSheetFigures
    {
        public int? RegisteredVoters { get; init; }
        public int? BallotsReceived { get; init; }
        /// <summary>Ballots found in the ballot box (= votes cast).</summary>
        public int? BallotsUsed { get; init; }
        public int? BallotsSpoiled { get; init; }
        public int? BallotsUnused { get; init; }
        public int BlankVotes { get; init; }
        public int InvalidVotes { get; init; }
        public IReadOnlyDictionary<Guid, int> ContestantVotes { get; init; } = new Dictionary<Guid, int>();
    }

    /// <summary>
    /// Arithmetic and plausibility checks for a polling station results sheet.
    /// Errors block approval; warnings are flagged for review.
    /// </summary>
    public class ResultSheetValidator
    {
        public const string MissingFigure = "MISSING_FIGURE";
        public const string NegativeFigure = "NEGATIVE_FIGURE";
        public const string VotesDoNotMatchBallotsUsed = "VOTES_NE_BALLOTS_USED";
        public const string BallotsDoNotReconcile = "BALLOTS_DO_NOT_RECONCILE";
        public const string TurnoutAboveRegistered = "TURNOUT_ABOVE_REGISTERED";
        public const string VeryHighTurnout = "VERY_HIGH_TURNOUT";
        public const string SingleContestantTakesAll = "SINGLE_CONTESTANT_ALL_VOTES";
        public const string NoContestantVotes = "NO_CONTESTANT_VOTES";

        /// <summary>Turnout at or above this share of registered voters is flagged.</summary>
        public decimal HighTurnoutPercent { get; init; } = 95m;

        /// <summary>A single contestant with all valid votes is only flagged above this many valid votes.</summary>
        public int SingleContestantMinValidVotes { get; init; } = 50;

        public ResultSheetValidation Validate(ResultSheetFigures sheet, IEnumerable<Guid> expectedContestants = null)
        {
            if (sheet == null) throw new ArgumentNullException(nameof(sheet));
            var issues = new List<ValidationIssue>();

            void Required(int? value, string name)
            {
                if (value == null) issues.Add(new ValidationIssue(MissingFigure, IssueSeverity.Error, $"{name} is required."));
            }
            Required(sheet.BallotsReceived, "Ballots received");
            Required(sheet.BallotsUsed, "Ballots used");
            Required(sheet.BallotsSpoiled, "Spoiled ballots");
            Required(sheet.BallotsUnused, "Unused ballots");

            var figures = new (string Name, int? Value)[]
            {
                ("Registered voters", sheet.RegisteredVoters),
                ("Ballots received", sheet.BallotsReceived),
                ("Ballots used", sheet.BallotsUsed),
                ("Spoiled ballots", sheet.BallotsSpoiled),
                ("Unused ballots", sheet.BallotsUnused),
                ("Blank votes", sheet.BlankVotes),
                ("Invalid votes", sheet.InvalidVotes),
            };
            foreach (var (name, value) in figures.Where(f => f.Value < 0))
            {
                issues.Add(new ValidationIssue(NegativeFigure, IssueSeverity.Error, $"{name} cannot be negative."));
            }
            if (sheet.ContestantVotes.Values.Any(v => v < 0))
            {
                issues.Add(new ValidationIssue(NegativeFigure, IssueSeverity.Error, "Contestant votes cannot be negative."));
            }

            if (expectedContestants != null)
            {
                var missing = expectedContestants.Where(id => !sheet.ContestantVotes.ContainsKey(id)).ToList();
                if (missing.Count > 0)
                {
                    issues.Add(new ValidationIssue(MissingFigure, IssueSeverity.Error,
                        $"Votes are missing for {missing.Count} contestant(s) on the ballot (enter 0 if none)."));
                }
            }

            long validVotes = sheet.ContestantVotes.Values.Sum(v => (long)v);
            long votesCast = validVotes + sheet.BlankVotes + sheet.InvalidVotes;

            if (sheet.BallotsUsed is int used && used != votesCast)
            {
                issues.Add(new ValidationIssue(VotesDoNotMatchBallotsUsed, IssueSeverity.Error,
                    $"Valid + blank + invalid votes ({votesCast}) must equal ballots used ({used})."));
            }

            if (sheet.BallotsReceived is int received && sheet.BallotsUsed is int u &&
                sheet.BallotsSpoiled is int spoiled && sheet.BallotsUnused is int unused &&
                received != u + spoiled + unused)
            {
                issues.Add(new ValidationIssue(BallotsDoNotReconcile, IssueSeverity.Error,
                    $"Ballots received ({received}) must equal used + spoiled + unused ({u + spoiled + unused})."));
            }

            // Turnout checks are warnings, not errors: depending on the election's rules some voters
            // (e.g. officials, voters allowed to vote outside their station) may not be on the station's register.
            if (sheet.RegisteredVoters is int registered && registered > 0)
            {
                long cast = sheet.BallotsUsed ?? votesCast;
                if (cast > registered)
                {
                    issues.Add(new ValidationIssue(TurnoutAboveRegistered, IssueSeverity.Warning,
                        $"Votes cast ({cast}) exceed registered voters ({registered})."));
                }
                else if (cast * 100m / registered >= HighTurnoutPercent)
                {
                    issues.Add(new ValidationIssue(VeryHighTurnout, IssueSeverity.Warning,
                        $"Turnout is {Math.Round(cast * 100m / registered, 1)}% of registered voters."));
                }
            }

            if (sheet.ContestantVotes.Count > 1 && validVotes >= SingleContestantMinValidVotes &&
                sheet.ContestantVotes.Values.Any(v => v == validVotes))
            {
                issues.Add(new ValidationIssue(SingleContestantTakesAll, IssueSeverity.Warning,
                    "One contestant received every valid vote at this station."));
            }

            if (votesCast > 0 && validVotes == 0)
            {
                issues.Add(new ValidationIssue(NoContestantVotes, IssueSeverity.Warning,
                    "No valid votes for any contestant: every ballot was blank or invalid."));
            }

            return new ResultSheetValidation(issues);
        }
    }
}
