using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VotingApp.Web.Localization;

namespace VotingApp.Controllers
{
    /// <summary>Remembers the visitor's language choice (cookie, one year) and returns to the page.</summary>
    [AllowAnonymous]
    public class LanguageController : BaseController
    {
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Set(string lang, string returnUrl)
        {
            if (TranslationCatalog.IsSupported(lang))
            {
                Response.Cookies.Append(Texts.Cookie, lang, new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps,
                });
            }
            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
        }
    }
}
