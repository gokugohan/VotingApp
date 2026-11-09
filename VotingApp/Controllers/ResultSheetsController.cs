using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VotingApp.Application.Common;
using VotingApp.Application.Elections;
using VotingApp.Application.Security;
using VotingApp.Infrastructure;
using VotingApp.Models;

namespace VotingApp.Controllers
{
    /// <summary>Polling station results sheets: entry, blind verification, approval and locking.</summary>
    public class ResultSheetsController : BaseController
    {
        private const string AnyResultsRole =
            Permissions.EnterResults + "|" + Permissions.VerifyResults + "|" + Permissions.ApproveResults + "|" + Permissions.LockResults;

        private readonly ResultSheetService sheets;
        private readonly ElectionAdminService elections;
        private readonly ElectionResultsService results;
        private readonly SheetScanStorage scans;

        public ResultSheetsController(ResultSheetService sheets, ElectionAdminService elections,
            ElectionResultsService results, SheetScanStorage scans)
        {
            this.scans = scans;
            this.sheets = sheets;
            this.elections = elections;
            this.results = results;
        }

        /// <summary>Progress board for a round: every station in the user's scope with its sheet status.</summary>
        [Authorize(Policy = AnyResultsRole)]
        public async Task<IActionResult> Index(Guid roundId, string municipalityId = null)
        {
            var round = await elections.GetRoundAsync(roundId, HttpContext.RequestAborted);
            if (round == null) return NotFound();
            ViewData["Round"] = round;
            ViewData["MunicipalityId"] = municipalityId;
            ViewData["Summary"] = await results.GetSummaryAsync(roundId, Domain.Elections.ResultSheetStatus.Entered, HttpContext.RequestAborted);
            return View(await sheets.ListStationsAsync(roundId, municipalityId, HttpContext.RequestAborted));
        }

        [Authorize(Policy = Permissions.EnterResults)]
        public async Task<IActionResult> Enter(Guid roundId, Guid stationId)
        {
            var form = await sheets.GetEntryFormAsync(roundId, stationId, HttpContext.RequestAborted);
            if (!form.Succeeded) return FailurePage(form, roundId);
            var model = form.Value.Sheet == null
                ? SheetFormModel.Blank(roundId, stationId, form.Value.Ballot)
                : SheetFormModel.From(form.Value.Sheet, form.Value.Ballot);
            return View("Sheet", new SheetPageModel { Context = form.Value, Form = model });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.EnterResults)]
        [RequestSizeLimit(SheetScanStorage.MaxBytes + 1024 * 1024)]
        public async Task<IActionResult> Enter([Bind(Prefix = "Form")] SheetFormModel model)
        {
            string scanExtension = null;
            if (model.Scan != null)
            {
                scanExtension = await SheetScanStorage.DetectAsync(model.Scan, HttpContext.RequestAborted);
                if (scanExtension == null) ModelState.AddModelError("Form.Scan", "The scan must be a JPEG, PNG or PDF file under 8 MB.");
            }
            if (ModelState.IsValid)
            {
                var result = await sheets.SubmitEntryAsync(model.RoundId, model.StationId, model.ToInput(), HttpContext.RequestAborted);
                if (result.Succeeded && scanExtension != null)
                {
                    var sheetId = await sheets.FindSheetIdAsync(model.RoundId, model.StationId, HttpContext.RequestAborted);
                    var path = await scans.SaveAsync(model.Scan, scanExtension, HttpContext.RequestAborted);
                    if (sheetId.HasValue) await sheets.AttachScanAsync(sheetId.Value, path, HttpContext.RequestAborted);
                }
                if (result.Succeeded)
                {
                    Flash(result.Value.HasErrors
                        ? "Saved, but the figures do not add up. Check the paper sheet; it cannot be approved until corrected."
                        : "Saved. A second clerk must now verify it.", error: result.Value.HasErrors);
                    return RedirectToAction(nameof(Index), new { roundId = model.RoundId });
                }
                if (result.Failure == FailureKind.Forbidden || result.Failure == FailureKind.NotFound) return FailurePage(result, model.RoundId);
                AddErrors(result);
            }
            return await RedisplayAsync(model, isVerification: false);
        }

        [Authorize(Policy = Permissions.VerifyResults)]
        public async Task<IActionResult> Verify(Guid roundId, Guid stationId)
        {
            var form = await sheets.GetEntryFormAsync(roundId, stationId, HttpContext.RequestAborted);
            if (!form.Succeeded) return FailurePage(form, roundId);
            if (form.Value.Sheet == null) return NotFound();
            // Blind entry: the first entry's figures are not shown.
            var model = SheetFormModel.Blank(roundId, stationId, form.Value.Ballot);
            model.SheetId = form.Value.Sheet.Id;
            return View("Sheet", new SheetPageModel { Context = form.Value, Form = model, IsVerification = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.VerifyResults)]
        public async Task<IActionResult> Verify([Bind(Prefix = "Form")] SheetFormModel model)
        {
            if (ModelState.IsValid && model.SheetId.HasValue)
            {
                var result = await sheets.SubmitVerificationAsync(model.SheetId.Value, model.ToInput(), HttpContext.RequestAborted);
                if (result.Succeeded)
                {
                    Flash(result.Value.Matched
                        ? "Verified: both entries match."
                        : "The entries differ in: " + string.Join(", ", result.Value.MismatchedFields) + ". A supervisor must check the paper sheet.",
                        error: !result.Value.Matched);
                    return RedirectToAction(nameof(Index), new { roundId = model.RoundId });
                }
                if (result.Failure == FailureKind.Forbidden || result.Failure == FailureKind.NotFound) return FailurePage(result, model.RoundId);
                AddErrors(result);
            }
            return await RedisplayAsync(model, isVerification: true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ApproveResults)]
        public async Task<IActionResult> Approve(Guid roundId, Guid sheetId)
        {
            var result = await sheets.ApproveAsync(sheetId, HttpContext.RequestAborted);
            Flash(result.Succeeded ? "Sheet approved and counted." : string.Join(" ", result.Errors), error: !result.Succeeded);
            return RedirectToAction(nameof(Index), new { roundId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ApproveResults)]
        public async Task<IActionResult> Reopen(Guid roundId, Guid sheetId, string reason)
        {
            var result = await sheets.ReopenAsync(sheetId, reason, HttpContext.RequestAborted);
            Flash(result.Succeeded ? "Sheet reopened for correction." : result.Error, error: !result.Succeeded);
            return RedirectToAction(nameof(Index), new { roundId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.LockResults)]
        public async Task<IActionResult> Lock(Guid roundId)
        {
            var result = await sheets.LockApprovedAsync(roundId, HttpContext.RequestAborted);
            Flash(result.Succeeded ? $"{result.Value} approved sheet(s) locked." : result.Error, error: !result.Succeeded);
            return RedirectToAction(nameof(Index), new { roundId });
        }

        private async Task<IActionResult> RedisplayAsync(SheetFormModel model, bool isVerification)
        {
            var form = await sheets.GetEntryFormAsync(model.RoundId, model.StationId, HttpContext.RequestAborted);
            if (!form.Succeeded) return FailurePage(form, model.RoundId);
            // Keep contestant names in sync with the ballot (only ids/votes are posted back).
            var names = form.Value.Ballot.ToDictionary(c => c.Id, c => c.Name);
            foreach (var v in model.Votes) v.Name = names.TryGetValue(v.ContestantId, out var n) ? n : v.Name;
            return View("Sheet", new SheetPageModel { Context = form.Value, Form = model, IsVerification = isVerification });
        }

        private IActionResult FailurePage(OperationResult result, Guid roundId)
        {
            if (result.Failure == FailureKind.NotFound) return NotFound();
            Flash(result.Error, error: true);
            return RedirectToAction(nameof(Index), new { roundId });
        }
    }
}
