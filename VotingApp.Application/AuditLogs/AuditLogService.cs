using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VotingApp.Application.Abstractions;
using VotingApp.Domain.Entities;

namespace VotingApp.Application.AuditLogs
{
    public class AuditLogService
    {
        private readonly IAppDbContext db;

        public AuditLogService(IAppDbContext db) => this.db = db;

        /// <summary>Most recent entries first, capped so the page stays responsive as the log grows.</summary>
        public Task<List<Audit>> ListRecentAsync(int take = 1000, CancellationToken ct = default) =>
            db.AuditLogs.AsNoTracking().OrderByDescending(a => a.DateTime).Take(take).ToListAsync(ct);
    }
}
