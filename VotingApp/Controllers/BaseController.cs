using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using VotingApp.Application.Common;
using VotingApp.Web.Localization;

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

        /// <summary>Translator for the current request (messages from services are matched by template).</summary>
        protected Texts T => HttpContext.RequestServices.GetRequiredService<Texts>();

        /// <summary>Copies a failed command's messages into ModelState (for form posts), translated.</summary>
        protected void AddErrors(OperationResult result)
        {
            foreach (var error in result.Errors.DefaultIfEmpty("The operation failed."))
            {
                ModelState.AddModelError(string.Empty, T.Message(error));
            }
        }

        /// <summary>One-shot message for the next page, translated into the user's language.</summary>
        protected void Flash(string message, bool error = false) => TempData[error ? "Error" : "Success"] = T.Message(message);
    }
}
