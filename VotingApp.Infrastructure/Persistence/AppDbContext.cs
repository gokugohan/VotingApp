using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VotingApp.Application.Abstractions;
using VotingApp.Domain.Elections;
using VotingApp.Domain.Entities;
using VotingApp.Domain.Enums;
using VotingApp.Infrastructure.Identity;
using VotingApp.Infrastructure.Persistence.Configurations;

namespace VotingApp.Infrastructure.Persistence
{
    public class AppDbContext : IdentityDbContext<AppUser, IdentityRole, string>, IAppDbContext
    {
        private readonly ICurrentUser currentUser;

        public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser = null)
            : base(options)
        {
            this.currentUser = currentUser;
        }

        public DbSet<Audit> AuditLogs { get; set; }
        public DbSet<Area> Areas { get; set; }
        public DbSet<PollingStation> PollingStations { get; set; }
        public DbSet<PollingStationArea> PollingStationAreas { get; set; }
        public DbSet<Candidate> Candidates { get; set; }
        public DbSet<Vote> Votes { get; set; }

        public DbSet<Election> Elections { get; set; }
        public DbSet<ElectionRound> ElectionRounds { get; set; }
        public DbSet<Contestant> Contestants { get; set; }
        public DbSet<RoundContestant> RoundContestants { get; set; }
        public DbSet<PartyListCandidate> PartyListCandidates { get; set; }
        public DbSet<ResultSheet> ResultSheets { get; set; }
        public DbSet<ResultSheetEntry> ResultSheetEntries { get; set; }
        public DbSet<UserAreaAssignment> UserAreaAssignments { get; set; }
        public DbSet<ElectionStation> ElectionStations { get; set; }
        public DbSet<Party> Parties { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            // Historical table name (was the DbSet name before the refactor).
            builder.Entity<PollingStationArea>().ToTable("PollingStationArea");
            builder.ConfigureElections();
        }

        /// <summary>
        /// Every save through this context (application services and Identity alike) writes an audit
        /// row per changed entity, attributed to the current user.
        /// </summary>
        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            AddAuditEntries(currentUser?.UserId);
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            AddAuditEntries(currentUser?.UserId);
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        private void AddAuditEntries(string userId)
        {
            ChangeTracker.DetectChanges();
            var auditEntries = new List<AuditEntry>();
            foreach (var entry in ChangeTracker.Entries().ToList())
            {
                if (entry.Entity is Audit || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var auditEntry = new AuditEntry(entry) { TableName = entry.Entity.GetType().Name, UserId = userId };
                foreach (var property in entry.Properties)
                {
                    var name = property.Metadata.Name;
                    // Never copy secrets into the audit log.
                    if (name is nameof(AppUser.PasswordHash) or nameof(AppUser.SecurityStamp) or nameof(AppUser.ConcurrencyStamp))
                        continue;
                    if (property.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[name] = property.CurrentValue;
                        continue;
                    }
                    switch (entry.State)
                    {
                        case EntityState.Added:
                            auditEntry.AuditType = AuditType.Create;
                            auditEntry.NewValues[name] = property.CurrentValue;
                            break;
                        case EntityState.Deleted:
                            auditEntry.AuditType = AuditType.Delete;
                            auditEntry.OldValues[name] = property.OriginalValue;
                            break;
                        case EntityState.Modified when property.IsModified:
                            auditEntry.ChangedColumns.Add(name);
                            auditEntry.AuditType = AuditType.Update;
                            auditEntry.OldValues[name] = property.OriginalValue;
                            auditEntry.NewValues[name] = property.CurrentValue;
                            break;
                    }
                }
                auditEntries.Add(auditEntry);
            }
            foreach (var auditEntry in auditEntries)
            {
                AuditLogs.Add(auditEntry.ToAudit());
            }
        }
    }
}
