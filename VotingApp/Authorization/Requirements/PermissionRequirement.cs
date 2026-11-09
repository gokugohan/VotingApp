using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;

namespace VotingApp.Authorization.Requirements
{
    /// <summary>Satisfied when the user has at least one of <see cref="Permissions"/> (via a role or directly).</summary>
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public PermissionRequirement(IEnumerable<string> permissions)
        {
            Permissions = permissions?.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToArray() ?? Array.Empty<string>();
            if (Permissions.Count == 0) throw new ArgumentException("At least one permission is required.", nameof(permissions));
        }

        public IReadOnlyList<string> Permissions { get; }
    }
}
