using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VotingApp.Application.Elections;
using VotingApp.Application.Security;
using VotingApp.Domain.Elections;

namespace VotingApp.Controllers
{
    public class OfflineFiguresDto
    {
        public int? RegisteredVoters { get; set; }
        public int? BallotsReceived { get; set; }
        public int? BallotsUsed { get; set; }
        public int? BallotsSpoiled { get; set; }
        public int? BallotsUnused { get; set; }
        public int BlankVotes { get; set; }
        public int InvalidVotes { get; set; }
        public Dictionary<Guid, int> Votes { get; set; } = new();
    }

    public class OfflineSubmissionDto
    {
        public Guid Id { get; set; }
        /// <summary>"entry" or "verify".</summary>
        public string Kind { get; set; }
        public Guid RoundId { get; set; }
        public Guid StationId { get; set; }
        public DateTime? QueuedAt { get; set; }
        public OfflineFiguresDto Figures { get; set; }
    }

    /// <summary>
    /// Result entry that survives a lost connection: the page (cached by a service worker) holds the
    /// ballot and the clerk's stations; sheets are queued on the device and sent to <see cref="Sync"/>.
    /// </summary>
    [Authorize(Policy = AnyResultsRole)]
    public class OfflineEntryController : BaseController
    {
        private const string AnyResultsRole = Permissions.EnterResults + "|" + Permissions.VerifyResults;
        private const int MaxBatch = 50;

        private readonly OfflineEntryService offline;
        private readonly IAuthorizationService authz;

        public OfflineEntryController(OfflineEntryService offline, IAuthorizationService authz)
        {
            this.offline = offline;
            this.authz = authz;
        }

        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Index(Guid roundId)
        {
            var pack = await offline.GetPackAsync(roundId, HttpContext.RequestAborted);
            if (pack == null) return NotFound();
            ViewData["CanEnter"] = (await authz.AuthorizeAsync(User, Permissions.EnterResults)).Succeeded;
            ViewData["CanVerify"] = (await authz.AuthorizeAsync(User, Permissions.VerifyResults)).Succeeded;
            return View(pack);
        }

        /// <summary>Applies queued sheets; safe to resend (each id is applied at most once).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sync([FromBody] List<OfflineSubmissionDto> submissions)
        {
            if (submissions == null || submissions.Count == 0) return BadRequest(new { message = "Nothing to sync." });
            if (submissions.Count > MaxBatch) return BadRequest(new { message = $"Send at most {MaxBatch} sheets at a time." });

            var canEnter = (await authz.AuthorizeAsync(User, Permissions.EnterResults)).Succeeded;
            var canVerify = (await authz.AuthorizeAsync(User, Permissions.VerifyResults)).Succeeded;

            var outcomes = new List<SubmissionOutcome>();
            foreach (var s in submissions)
            {
                var kind = s.Kind == "verify" ? SubmissionKind.Verification : s.Kind == "entry" ? SubmissionKind.Entry : (SubmissionKind?)null;
                if (kind == null || s.Figures == null)
                {
                    outcomes.Add(new SubmissionOutcome(s.Id, OfflineEntryService.Rejected, "Unknown or incomplete submission."));
                    continue;
                }
                if ((kind == SubmissionKind.Entry && !canEnter) || (kind == SubmissionKind.Verification && !canVerify))
                {
                    outcomes.Add(new SubmissionOutcome(s.Id, OfflineEntryService.Rejected, "You do not have permission for this kind of entry."));
                    continue;
                }
                var f = s.Figures;
                var figures = new SheetFiguresInput(f.RegisteredVoters, f.BallotsReceived, f.BallotsUsed, f.BallotsSpoiled, f.BallotsUnused,
                    f.BlankVotes, f.InvalidVotes, f.Votes ?? new Dictionary<Guid, int>());
                outcomes.Add(await offline.SubmitAsync(
                    new OfflineSubmission(s.Id, kind.Value, s.RoundId, s.StationId, s.QueuedAt?.ToUniversalTime(), figures),
                    HttpContext.RequestAborted));
            }
            return Json(outcomes);
        }
    }
}
