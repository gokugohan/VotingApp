using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VotingApp.Application.AuditLogs;
using VotingApp.Application.Security;

namespace VotingApp.Controllers
{
    [Authorize(Policy = Permissions.ViewAuditLog)]
    public class AuditLogController : BaseController
    {
        private readonly AuditLogService auditLogs;

        public AuditLogController(AuditLogService auditLogs) => this.auditLogs = auditLogs;

        public async Task<IActionResult> Index() =>
            View(await auditLogs.ListRecentAsync(ct: HttpContext.RequestAborted));
    }
}
