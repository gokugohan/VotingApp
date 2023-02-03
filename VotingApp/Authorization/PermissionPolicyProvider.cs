using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VotingApp.Authorization.Requirements;

namespace VotingApp.Authorization
{
    // https://www.zehntec.com/blog/permission-based-authorization-in-asp-net-core/
    public class PermissionPolicyProvider : IAuthorizationPolicyProvider
    {
        public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        {
            // There can only be one policy provider in ASP.NET Core.
            // We only handle permissions related policies, for the rest
            /// we will use the default provider.
            this.FallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
        }

        public DefaultAuthorizationPolicyProvider FallbackPolicyProvider { get; }


        public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        {
            return this.FallbackPolicyProvider.GetDefaultPolicyAsync();
        }

        public Task<AuthorizationPolicy> GetFallbackPolicyAsync()
        {
            return this.FallbackPolicyProvider.GetFallbackPolicyAsync();
        }

        public Task<AuthorizationPolicy> GetPolicyAsync(string policyName)
        {
            bool isValidPermissionInitial = policyName.StartsWith("PERMISSION.", StringComparison.OrdinalIgnoreCase);
            if (isValidPermissionInitial)
            {
                var policy = new AuthorizationPolicyBuilder();
                policy.AddRequirements(new PermissionRequirement(policyName));
                return Task.FromResult(policy.Build());
            }

            // Policy is not for permissions, try the default provider.
            return this.FallbackPolicyProvider.GetPolicyAsync(policyName);
        }
    }
}
