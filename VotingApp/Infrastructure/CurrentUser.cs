using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using VotingApp.Application.Abstractions;
using VotingApp.Application.Security;

namespace VotingApp.Infrastructure
{
    /// <summary><see cref="ICurrentUser"/> backed by the current HTTP request.</summary>
    public class CurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor accessor;

        public CurrentUser(IHttpContextAccessor accessor) => this.accessor = accessor;

        private ClaimsPrincipal Principal => accessor.HttpContext?.User;

        public string UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
        public bool IsAdmin => Principal?.IsInRole(Permissions.AdminRole) == true;
    }
}
