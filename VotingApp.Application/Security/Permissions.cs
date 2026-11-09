using System.Collections.Generic;
using System.Linq;

namespace VotingApp.Application.Security
{
    public sealed record PermissionItem(string Name, string Description, string Group);

    /// <summary>
    /// Permission claim values (claim type <see cref="CustomClaimTypes.Permission"/>), used as
    /// authorization policy names. Existing values are stored in the database: never change them.
    /// </summary>
    public static class Permissions
    {
        public const string AdminRole = "Admin";

        // Accounts and system
        public const string ViewAccounts = "PERMISSION.VIEW.ACCOUNT";
        public const string DeleteUsers = "PERMISSION.VIEW.DELETE_USER";
        public const string ViewAuditLog = "PERMISSION.VIEW.LOG";

        // Reference data
        public const string ManageAreas = "PERMISSION.ADD.AREA";
        public const string ManagePollingStations = "PERMISSION.ADD.POLLING_STATION";
        public const string ManageCandidates = "PERMISSION.ADD.CANDIDATE";

        // Legacy quick vote entry
        public const string EnterVotes = "PERMISSION.ADD.VOTING";

        // Elections and results workflow
        public const string ManageElections = "PERMISSION.ELECTION.MANAGE";
        public const string EnterResults = "PERMISSION.RESULTS.ENTER";
        public const string VerifyResults = "PERMISSION.RESULTS.VERIFY";
        public const string ApproveResults = "PERMISSION.RESULTS.APPROVE";
        public const string LockResults = "PERMISSION.RESULTS.LOCK";

        public static IReadOnlyList<PermissionItem> All { get; } = new List<PermissionItem>
        {
            new(ViewAccounts, "View user accounts", "Accounts"),
            new(DeleteUsers, "Delete users", "Accounts"),
            new(ViewAuditLog, "View the audit log", "Accounts"),
            new(ManageAreas, "Manage administrative areas", "Reference data"),
            new(ManagePollingStations, "Manage polling stations", "Reference data"),
            new(ManageCandidates, "Manage candidates", "Reference data"),
            new(EnterVotes, "Quick vote entry (legacy)", "Votes"),
            new(ManageElections, "Set up elections, rounds and contestants", "Elections"),
            new(EnterResults, "Enter polling station result sheets", "Results"),
            new(VerifyResults, "Verify result sheets (second entry)", "Results"),
            new(ApproveResults, "Approve result sheets (municipal tabulation)", "Results"),
            new(LockResults, "Lock result sheets (national tabulation)", "Results"),
        };

        public static bool IsKnown(string permission) => All.Any(p => p.Name == permission);
    }
}
