using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;

namespace VotingApp.Web.Localization
{
    public sealed record Language(string Code, string NativeName);

    /// <summary>
    /// Translations keyed by the English text, loaded from Resources/i18n/{code}.json. Anything not
    /// translated falls back to English, so a missing entry never breaks a page.
    /// </summary>
    public sealed class TranslationCatalog
    {
        public const string Default = "en";

        public static readonly IReadOnlyList<Language> Languages = new[]
        {
            new Language("en", "English"),
            new Language("pt", "Português"),
            new Language("tet", "Tetun"),
        };

        private readonly Dictionary<string, IReadOnlyDictionary<string, string>> catalogs = new();

        public TranslationCatalog(IWebHostEnvironment environment)
        {
            foreach (var language in Languages.Where(l => l.Code != Default))
            {
                var file = Path.Combine(environment.ContentRootPath, "Resources", "i18n", language.Code + ".json");
                catalogs[language.Code] = File.Exists(file) ? Load(file) : new Dictionary<string, string>();
            }
        }

        public static IReadOnlyDictionary<string, string> Load(string file)
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file)) ?? new();
            // Keys starting with "_" are notes for translators, not texts.
            return map.Where(kv => !kv.Key.StartsWith("_", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(kv.Value))
                      .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public string Translate(string language, string english) =>
            language != Default && catalogs.TryGetValue(language, out var map) && map.TryGetValue(english, out var text) ? text : english;

        public static bool IsSupported(string code) => Languages.Any(l => l.Code == code);
    }

    /// <summary>
    /// Per-request translator for views: <c>@T["Stations reporting"]</c>, or with arguments
    /// <c>@T["{0} of {1} stations", a, b]</c>. The language comes from the "lang" cookie, else the
    /// browser's Accept-Language, else English.
    /// </summary>
    public sealed class Texts
    {
        public const string Cookie = "lang";
        private readonly TranslationCatalog catalog;

        public Texts(TranslationCatalog catalog, IHttpContextAccessor accessor)
        {
            this.catalog = catalog;
            Lang = Resolve(accessor.HttpContext);
        }

        public string Lang { get; }

        public string this[string english] => catalog.Translate(Lang, english);

        public string this[string english, params object[] args] =>
            string.Format(CultureInfo.CurrentCulture, catalog.Translate(Lang, english), args);

        /// <summary>
        /// A translated sentence that contains markup (e.g. "&lt;strong&gt;{0}&lt;/strong&gt; leads"). The
        /// sentence comes from our own translation files; the arguments are data and are HTML-encoded.
        /// </summary>
        public IHtmlContent Html(string english, params object[] args) =>
            new HtmlString(string.Format(CultureInfo.CurrentCulture, catalog.Translate(Lang, english),
                args.Select(a => (object)HtmlEncoder.Default.Encode(Convert.ToString(a, CultureInfo.CurrentCulture) ?? string.Empty)).ToArray()));

        public static string Resolve(HttpContext context)
        {
            if (context == null) return TranslationCatalog.Default;
            var cookie = context.Request.Cookies[Cookie];
            if (TranslationCatalog.IsSupported(cookie)) return cookie;

            // Browser preference: Tetun ("tet"), Portuguese ("pt", "pt-PT", "pt-BR"), otherwise English.
            foreach (var part in context.Request.Headers.AcceptLanguage.ToString().Split(','))
            {
                var tag = part.Split(';')[0].Trim().ToLowerInvariant();
                if (tag.StartsWith("tet", StringComparison.Ordinal)) return "tet";
                if (tag.StartsWith("pt", StringComparison.Ordinal)) return "pt";
                if (tag.StartsWith("en", StringComparison.Ordinal)) return "en";
            }
            return TranslationCatalog.Default;
        }
    }
}
