using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VotingApp.Application.Abstractions;
using VotingApp.Domain.Entities;

namespace VotingApp.Application.Elections
{
    /// <summary>The municipalities a user may work in; <see cref="IsNational"/> means all of them.</summary>
    public sealed class UserScope
    {
        private readonly HashSet<string> municipalities;

        private UserScope(bool national, IEnumerable<string> municipalities)
        {
            IsNational = national;
            this.municipalities = new HashSet<string>(municipalities ?? Enumerable.Empty<string>());
        }

        public static UserScope National { get; } = new(true, null);
        public static UserScope For(IEnumerable<string> municipalityIds) => new(false, municipalityIds);

        public bool IsNational { get; }
        public IReadOnlyCollection<string> MunicipalityIds => municipalities;

        public bool Allows(string areaId) =>
            IsNational || (!string.IsNullOrEmpty(areaId) && areaId.Length >= AreaHierarchy.MunicipalityCodeLength &&
                           municipalities.Contains(AreaHierarchy.MunicipalityIdOf(areaId)));
    }

    /// <summary>Resolves and manages which municipalities each user may enter/verify/approve results for.</summary>
    public class UserScopeService
    {
        private readonly IAppDbContext db;
        private readonly ICurrentUser currentUser;

        public UserScopeService(IAppDbContext db, ICurrentUser currentUser)
        {
            this.db = db;
            this.currentUser = currentUser;
        }

        /// <summary>
        /// Admins are national. Other users are limited to their assigned municipalities; a user with
        /// no assignments is national only if <paramref name="nationalWhenUnassigned"/> (e.g. national tabulation staff).
        /// </summary>
        public async Task<UserScope> GetCurrentScopeAsync(bool nationalWhenUnassigned = false, CancellationToken ct = default)
        {
            if (!currentUser.IsAuthenticated) return UserScope.For(null);
            if (currentUser.IsAdmin) return UserScope.National;

            var ids = await db.UserAreaAssignments.AsNoTracking()
                .Where(a => a.UserId == currentUser.UserId)
                .Select(a => a.MunicipalityId)
                .ToListAsync(ct);
            if (ids.Count == 0 && nationalWhenUnassigned) return UserScope.National;
            return UserScope.For(ids);
        }

        public async Task<Dictionary<string, List<string>>> GetAllAssignmentsAsync(CancellationToken ct = default)
        {
            var rows = await db.UserAreaAssignments.AsNoTracking().ToListAsync(ct);
            return rows.GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.Select(r => r.MunicipalityId).OrderBy(x => x).ToList());
        }

        /// <summary>Replaces a user's municipality assignments.</summary>
        public async Task SetAssignmentsAsync(string userId, IEnumerable<string> municipalityIds, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(userId)) throw new ArgumentException("User is required.", nameof(userId));
            var wanted = new HashSet<string>(municipalityIds ?? Enumerable.Empty<string>());
            var valid = await db.Areas.Where(a => a.Level == AreaHierarchy.Municipality).Select(a => a.Id).ToListAsync(ct);
            wanted.IntersectWith(valid);

            var existing = await db.UserAreaAssignments.Where(a => a.UserId == userId).ToListAsync(ct);
            foreach (var row in existing.Where(r => !wanted.Contains(r.MunicipalityId)))
            {
                db.UserAreaAssignments.Remove(row);
            }
            foreach (var id in wanted.Except(existing.Select(r => r.MunicipalityId)))
            {
                db.UserAreaAssignments.Add(new UserAreaAssignment { Id = Guid.NewGuid(), UserId = userId, MunicipalityId = id });
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
