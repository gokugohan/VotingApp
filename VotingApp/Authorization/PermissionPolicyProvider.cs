using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using VotingApp.Authorization.Requirements;

namespace VotingApp.Authorization
{
    /// <summary>
    /// Builds policies on the fly from permission names, e.g. [Authorize(Policy = Permissions.EnterVotes)].
    /// Several permissions joined with '|' mean "any of": see <see cref="AnyPermission"/>.
    /// Other policy names fall back to the default provider.
    /// </summary>
    public class PermissionPolicyProvider : IAuthorizationPolicyProvider
    {
        private const string Prefix = "PERMISSION.";

        public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        {
            FallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
        }

        public DefaultAuthorizationPolicyProvider FallbackPolicyProvider { get; }

        /// <summary>Policy name satisfied by any of the given permissions.</summary>
        public static string AnyPermission(params string[] permissions) => string.Join("|", permissions);

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => FallbackPolicyProvider.GetDefaultPolicyAsync();

        public Task<AuthorizationPolicy> GetFallbackPolicyAsync() => FallbackPolicyProvider.GetFallbackPolicyAsync();

        public Task<AuthorizationPolicy> GetPolicyAsync(string policyName)
        {
            var parts = policyName.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length > 0 && parts.All(p => p.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)))
            {
                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(parts))
                    .Build();
                return Task.FromResult(policy);
            }
            return FallbackPolicyProvider.GetPolicyAsync(policyName);
        }
    }
}
