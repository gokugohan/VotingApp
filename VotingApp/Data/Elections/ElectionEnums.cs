namespace VotingApp.Data.Elections
{
    public enum ElectionType
    {
        Presidential = 1,
        Parliamentary = 2,
    }

    public enum ElectionStatus
    {
        Setup = 0,
        Voting = 1,
        Counting = 2,
        /// <summary>Results published but not yet certified.</summary>
        Provisional = 3,
        /// <summary>Validated by the competent court; no further changes.</summary>
        Certified = 4,
    }

    public enum ContestantType
    {
        /// <summary>A presidential candidate.</summary>
        Candidate = 1,
        /// <summary>A political party list.</summary>
        Party = 2,
        /// <summary>A coalition list of several parties.</summary>
        Coalition = 3,
    }

    /// <summary>Workflow of a polling station results sheet.</summary>
    public enum ResultSheetStatus
    {
        Draft = 0,
        /// <summary>Entered by a data-entry clerk.</summary>
        Entered = 1,
        /// <summary>Confirmed by a second, independent entry or a supervisor.</summary>
        Verified = 2,
        /// <summary>Approved at municipal tabulation.</summary>
        Approved = 3,
        /// <summary>Included in national tabulation; changes need a formal correction.</summary>
        Locked = 4,
    }

    public enum Gender
    {
        Female = 1,
        Male = 2,
    }
}
