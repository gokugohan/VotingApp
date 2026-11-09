using VotingApp.Domain.Entities;
using VotingApp.Domain.Elections.Rules;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VotingApp.Domain.Elections
{
    /// <summary>
    /// The official count for one polling station in one round: ballot reconciliation,
    /// blank/invalid votes and votes per contestant, plus who entered/verified/approved it.
    /// </summary>
    public class ResultSheet
    {
        public Guid Id { get; set; }

        public Guid ElectionRoundId { get; set; }
        public ElectionRound ElectionRound { get; set; }

        public Guid PollingStationAreaId { get; set; }
        public PollingStationArea PollingStationArea { get; set; }

        // Reconciliation figures. Nullable because imported/legacy data may not have them;
        // ResultSheetValidator reports them as missing before a sheet can be approved.
        public int? RegisteredVoters { get; set; }
        public int? BallotsReceived { get; set; }
        /// <summary>Ballots found in the ballot box (= votes cast).</summary>
        public int? BallotsUsed { get; set; }
        public int? BallotsSpoiled { get; set; }
        public int? BallotsUnused { get; set; }

        public int BlankVotes { get; set; }
        public int InvalidVotes { get; set; }

        public ResultSheetStatus Status { get; set; }

        /// <summary>Relative path of the uploaded photo/scan of the signed sheet.</summary>
        public string ScanPath { get; set; }
        public string Notes { get; set; }

        public string EnteredByUserId { get; set; }
        public DateTime? EnteredAt { get; set; }
        public string VerifiedByUserId { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public string ApprovedByUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string LockedByUserId { get; set; }
        public DateTime? LockedAt { get; set; }

        /// <summary>Number of second entries that did not match the first entry.</summary>
        public int VerificationMismatches { get; set; }

        /// <summary>Fields that differed on the last failed second entry (for the supervisor).</summary>
        public string LastMismatchFields { get; set; }

        /// <summary>Optimistic concurrency: two clerks saving the same sheet cannot silently overwrite each other.</summary>
        public byte[] RowVersion { get; set; }

        public ICollection<ResultSheetEntry> Entries { get; set; } = new List<ResultSheetEntry>();

        public ResultSheetFigures ToFigures() => new()
        {
            RegisteredVoters = RegisteredVoters,
            BallotsReceived = BallotsReceived,
            BallotsUsed = BallotsUsed,
            BallotsSpoiled = BallotsSpoiled,
            BallotsUnused = BallotsUnused,
            BlankVotes = BlankVotes,
            InvalidVotes = InvalidVotes,
            ContestantVotes = Entries.ToDictionary(e => e.ContestantId, e => e.Votes),
        };
    }

    public class ResultSheetEntry
    {
        public Guid Id { get; set; }
        public Guid ResultSheetId { get; set; }
        public ResultSheet ResultSheet { get; set; }
        public Guid ContestantId { get; set; }
        public Contestant Contestant { get; set; }
        public int Votes { get; set; }
    }
}
