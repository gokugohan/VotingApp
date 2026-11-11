using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        private readonly Dictionary<string, List<(Regex Pattern, string Text)>> templates = new();

        public TranslationCatalog(IWebHostEnvironment environment)
        {
            foreach (var language in Languages.Where(l => l.Code != Default))
            {
                var file = Path.Combine(environment.ContentRootPath, "Resources", "i18n", language.Code + ".json");
                catalogs[language.Code] = File.Exists(file) ? Load(file) : new Dictionary<string, string>();
                templates[language.Code] = Templates(catalogs[language.Code]);
            }
        }

        /// <summary>
        /// Keys with placeholders ("Votes add up to {0}, but ballots used is {1}.") turned into patterns,
        /// so a finished English message from a service can be matched and translated with its values.
        /// Keys with little fixed text are skipped: they would match almost anything.
        /// </summary>
        public static List<(Regex Pattern, string Text)> Templates(IReadOnlyDictionary<string, string> map) =>
            map.Where(kv => kv.Key.Contains('{') && Regex.Replace(kv.Key, @"\{\d+\}", "").Trim().Length >= 8)
               .OrderByDescending(kv => kv.Key.Length)
               .Select(kv => (new Regex("^" + Regex.Replace(Regex.Escape(kv.Key), @"\\{(\d+)}", m => $"(?<p{m.Groups[1].Value}>.+?)") + "$",
                   RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromMilliseconds(50)), kv.Value))
               .ToList();

        /// <summary>
        /// Translates a finished message: an exact key first, then a key with placeholders whose fixed
        /// text matches (the values are carried over). Unknown messages stay in English.
        /// </summary>
        public string TranslateMessage(string language, string english)
        {
            if (string.IsNullOrEmpty(english) || language == Default || !catalogs.TryGetValue(language, out var map)) return english;
            if (map.TryGetValue(english, out var exact)) return exact;
            foreach (var (pattern, text) in templates[language])
            {
                Match m;
                try { m = pattern.Match(english); } catch (RegexMatchTimeoutException) { continue; }
                if (!m.Success) continue;
                return Regex.Replace(text, @"\{(\d+)\}", p => m.Groups["p" + p.Groups[1].Value] is { Success: true } g ? Value(map, g.Value) : p.Value);
            }
            return english;
        }

        /// <summary>A value inside a message (a field name, a status, a list of fields) is translated when it is itself a known text.</summary>
        private static string Value(IReadOnlyDictionary<string, string> map, string value)
        {
            if (map.TryGetValue(value, out var whole)) return whole;
            if (!value.Contains(", ")) return value;
            return string.Join(", ", value.Split(", ").Select(part => map.TryGetValue(part, out var t) ? t : part));
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

        /// <summary>Dates and numbers of the current language (see <see cref="AppCultures"/>).</summary>
        public CultureInfo Culture => AppCultures.For(Lang);

        /// <summary>A full date: "1 May 2027", "1 de maio de 2027", "1 Maiu 2027".</summary>
        public string Date(DateTime date) => date.ToString(AppCultures.LongDatePattern(Lang), Culture);

        /// <summary>A short date: "1 May 2027", "1 mai. 2027", "1 Mai 2027".</summary>
        public string ShortDate(DateTime date) => date.ToString("d MMM yyyy", Culture);

        public string this[string english] => catalog.Translate(Lang, english);

        /// <summary>Translates a finished message from a service or validator (see <see cref="TranslationCatalog.TranslateMessage"/>).</summary>
        public string Message(string english) => catalog.TranslateMessage(Lang, english);

        public string this[string english, params object[] args] =>
            string.Format(Culture, catalog.Translate(Lang, english), args);

        /// <summary>
        /// A translated sentence that contains markup (e.g. "&lt;strong&gt;{0}&lt;/strong&gt; leads"). The
        /// sentence comes from our own translation files; the arguments are data and are HTML-encoded.
        /// </summary>
        public IHtmlContent Html(string english, params object[] args) =>
            new HtmlString(string.Format(Culture, catalog.Translate(Lang, english),
                args.Select(a => (object)HtmlEncoder.Default.Encode(Convert.ToString(a, Culture) ?? string.Empty)).ToArray()));

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
