using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VotingApp.Application.Common;

namespace VotingApp.Controllers
{
    /// <summary>Requires sign-in by default; actions opt out with [AllowAnonymous].</summary>
    [Authorize]
    public abstract class BaseController : Controller
    {
        /// <summary>Maps a failed command to the matching HTTP response (for JSON endpoints).</summary>
        protected IActionResult JsonFailure(OperationResult result) => result.Failure switch
        {
            FailureKind.NotFound => NotFound(new { success = false, message = result.Error }),
            FailureKind.Forbidden => StatusCode(403, new { success = false, message = result.Error }),
            FailureKind.Conflict => Conflict(new { success = false, message = result.Error }),
            _ => BadRequest(new { success = false, message = result.Error }),
        };

        /// <summary>Copies a failed command's messages into ModelState (for form posts).</summary>
        protected void AddErrors(OperationResult result)
        {
            foreach (var error in result.Errors.DefaultIfEmpty("The operation failed."))
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }

        protected void Flash(string message, bool error = false) => TempData[error ? "Error" : "Success"] = message;
    }
}
