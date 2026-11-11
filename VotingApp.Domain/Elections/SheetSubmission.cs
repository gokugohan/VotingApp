using System;

namespace VotingApp.Domain.Elections
{
    public enum SubmissionKind
    {
        Entry = 1,
        Verification = 2,
    }

    /// <summary>
    /// A result-sheet submission sent from the offline queue, recorded by the id the device gave it.
    /// When a flaky connection makes the device send the same submission again, the stored outcome is
    /// returned instead of applying it twice (e.g. counting a verification mismatch twice).
    /// </summary>
    public class SheetSubmission
    {
        /// <summary>Generated on the device when the sheet was queued.</summary>
        public Guid Id { get; set; }
        public string UserId { get; set; }
        public SubmissionKind Kind { get; set; }
        public Guid ElectionRoundId { get; set; }
        public Guid PollingStationAreaId { get; set; }
        /// <summary>When the clerk saved it on the device (as reported by the device).</summary>
        public DateTime? QueuedAtUtc { get; set; }
        public DateTime ReceivedAtUtc { get; set; }
        /// <summary>saved, verified, mismatch or rejected.</summary>
        public string Outcome { get; set; }
        public string Message { get; set; }
    }
}
