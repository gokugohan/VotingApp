using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VotingApp.Domain.Elections;
using VotingApp.Domain.Entities;

namespace VotingApp.Application.Abstractions
{
    /// <summary>
    /// Persistence port used by application services. Implemented by the EF Core context in
    /// Infrastructure; saving also writes audit log entries for the current user.
    /// </summary>
    public interface IAppDbContext
    {
        DbSet<Area> Areas { get; }
        DbSet<PollingStation> PollingStations { get; }
        DbSet<PollingStationArea> PollingStationAreas { get; }
        DbSet<Candidate> Candidates { get; }
        DbSet<Vote> Votes { get; }
        DbSet<Audit> AuditLogs { get; }

        DbSet<Election> Elections { get; }
        DbSet<ElectionRound> ElectionRounds { get; }
        DbSet<Contestant> Contestants { get; }
        DbSet<RoundContestant> RoundContestants { get; }
        DbSet<PartyListCandidate> PartyListCandidates { get; }
        DbSet<ResultSheet> ResultSheets { get; }
        DbSet<ResultSheetEntry> ResultSheetEntries { get; }
        DbSet<UserAreaAssignment> UserAreaAssignments { get; }
        DbSet<ElectionStation> ElectionStations { get; }
        DbSet<Party> Parties { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>The signed-in user, as seen by application services.</summary>
    public interface ICurrentUser
    {
        /// <summary>Identity user id, or null when anonymous.</summary>
        string UserId { get; }
        bool IsAuthenticated { get; }
        bool IsAdmin { get; }
    }
}
