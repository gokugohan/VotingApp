using Microsoft.EntityFrameworkCore;
using VotingApp.Domain.Elections;
using VotingApp.Domain.Entities;

namespace VotingApp.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// EF mapping for the election model. Delete behaviour avoids SQL Server's "multiple cascade paths"
    /// error: an Election cascades to its Rounds and Contestants, a Round to its ResultSheets,
    /// a ResultSheet to its Entries; links back to Contestant/PollingStationArea are Restrict.
    /// </summary>
    public static class ElectionConfiguration
    {
        public static void ConfigureElections(this ModelBuilder builder)
        {
            builder.Entity<Election>(e =>
            {
                e.Property(x => x.Name).IsRequired().HasMaxLength(200);
                e.Property(x => x.Notes).HasMaxLength(2000);
                e.Property(x => x.ElectionDate).HasColumnType("date");
                e.Property(x => x.ThresholdPercent).HasPrecision(5, 2);
                e.HasMany(x => x.Rounds).WithOne(r => r.Election).HasForeignKey(r => r.ElectionId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.Contestants).WithOne(c => c.Election).HasForeignKey(c => c.ElectionId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ElectionRound>(e =>
            {
                e.Property(x => x.VotingDate).HasColumnType("date");
                e.HasIndex(x => new { x.ElectionId, x.Number }).IsUnique();
                e.HasMany(x => x.ResultSheets).WithOne(s => s.ElectionRound).HasForeignKey(s => s.ElectionRoundId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Contestant>(e =>
            {
                e.Property(x => x.Name).IsRequired().HasMaxLength(200);
                e.Property(x => x.Acronym).HasMaxLength(30);
                e.Property(x => x.LogoPath).HasMaxLength(300);
                e.Property(x => x.Description).HasMaxLength(2000);
                e.HasIndex(x => new { x.ElectionId, x.Name }).IsUnique();
                e.HasOne(x => x.Candidate).WithMany().HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict);
                e.HasMany(x => x.ListCandidates).WithOne(l => l.Contestant).HasForeignKey(l => l.ContestantId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<RoundContestant>(e =>
            {
                e.HasKey(x => new { x.ElectionRoundId, x.ContestantId });
                e.HasOne(x => x.ElectionRound).WithMany(r => r.Contestants).HasForeignKey(x => x.ElectionRoundId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Contestant).WithMany().HasForeignKey(x => x.ContestantId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<PartyListCandidate>(e =>
            {
                e.Property(x => x.FullName).IsRequired().HasMaxLength(200);
                e.HasIndex(x => new { x.ContestantId, x.Position, x.IsSubstitute }).IsUnique();
            });

            builder.Entity<ResultSheet>(e =>
            {
                e.Property(x => x.RowVersion).IsRowVersion();
                e.Property(x => x.ScanPath).HasMaxLength(300);
                e.Property(x => x.Notes).HasMaxLength(2000);
                e.Property(x => x.EnteredByUserId).HasMaxLength(450);
                e.Property(x => x.VerifiedByUserId).HasMaxLength(450);
                e.Property(x => x.ApprovedByUserId).HasMaxLength(450);
                e.Property(x => x.LockedByUserId).HasMaxLength(450);
                e.Property(x => x.LastMismatchFields).HasMaxLength(1000);
                // One sheet per polling station per round.
                e.HasIndex(x => new { x.ElectionRoundId, x.PollingStationAreaId }).IsUnique();
                e.HasIndex(x => new { x.ElectionRoundId, x.Status });
                e.HasOne(x => x.PollingStationArea).WithMany().HasForeignKey(x => x.PollingStationAreaId).OnDelete(DeleteBehavior.Restrict);
                e.HasMany(x => x.Entries).WithOne(en => en.ResultSheet).HasForeignKey(en => en.ResultSheetId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ResultSheetEntry>(e =>
            {
                e.HasIndex(x => new { x.ResultSheetId, x.ContestantId }).IsUnique();
                e.HasOne(x => x.Contestant).WithMany().HasForeignKey(x => x.ContestantId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ElectionStation>(e =>
            {
                e.Property(x => x.StationName).IsRequired().HasMaxLength(200);
                e.Property(x => x.AreaId).IsRequired().HasMaxLength(450);
                e.Property(x => x.VillageName).HasMaxLength(200);
                e.Property(x => x.AdministrativePostId).HasMaxLength(450);
                e.Property(x => x.AdministrativePostName).HasMaxLength(200);
                e.Property(x => x.MunicipalityId).IsRequired().HasMaxLength(450);
                e.Property(x => x.MunicipalityName).HasMaxLength(200);
                e.HasIndex(x => new { x.ElectionId, x.PollingStationAreaId }).IsUnique();
                e.HasIndex(x => x.PollingStationAreaId);
                e.HasOne(x => x.Election).WithMany().HasForeignKey(x => x.ElectionId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<UserAreaAssignment>(e =>
            {
                e.Property(x => x.UserId).IsRequired().HasMaxLength(450);
                e.Property(x => x.MunicipalityId).IsRequired();
                e.HasIndex(x => new { x.UserId, x.MunicipalityId }).IsUnique();
                e.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Cascade);
            });

            // Legacy table: one row per candidate per polling station. Enforced so concurrent
            // SaveVote calls can't create duplicates (the second insert fails and is reported).
            builder.Entity<Vote>().HasIndex(x => new { x.CandidateId, x.PollingStationAreaId }).IsUnique();
        }
    }
}
