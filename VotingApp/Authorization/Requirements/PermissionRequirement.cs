using Microsoft.AspNetCore.Authorization;

namespace VotingApp.Authorization.Requirements
{
    public class PermissionRequirement:IAuthorizationRequirement
    {
        public string Permission { get; private set; }

        public PermissionRequirement(string permission)
        {
            Permission = permission;
        }
    }
}
