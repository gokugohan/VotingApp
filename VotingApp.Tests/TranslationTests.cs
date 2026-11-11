using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VotingApp.Tests
{
    /// <summary>
    /// Every text the views pass through the translator (T["..."], T.Html("...")) must exist in each
    /// language file, with the same {n} placeholders and the same markup.
    /// </summary>
    public class TranslationTests
    {
        private static readonly string[] Languages = { "pt", "tet" };

        // Texts translated with a computed key (enum names).
        private static readonly string[] DynamicKeys = { "Setup", "Voting", "Counting", "Provisional", "Certified", "Party", "Coalition", "Entered" };

        private static string SolutionDir() => Path.GetDirectoryName(WebProjectDir());

        /// <summary>
        /// Texts that reach the user from C#: labels ([Display]), validation and service messages. Plain
        /// literals are taken as they are; interpolated ones become templates ($"{a} of {b}" => "{0} of {1}"),
        /// which is how <see cref="VotingApp.Web.Localization.TranslationCatalog.TranslateMessage"/> matches them.
        /// </summary>
        private static HashSet<string> TextsUsedInCode()
        {
            var literal = new Regex(@"(\$?)""((?:[^""\\]|\\.)*)""");
            var hole = new Regex(@"\{[^{}]+\}");
            var texts = new HashSet<string>();
            foreach (var project in new[] { "VotingApp", "VotingApp.Application", "VotingApp.Domain" })
            {
                foreach (var file in Directory.EnumerateFiles(Path.Combine(SolutionDir(), project), "*.cs", SearchOption.AllDirectories))
                {
                    if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) || file.Contains("Migrations")) continue;
                    foreach (Match m in literal.Matches(File.ReadAllText(file)))
                    {
                        var text = m.Groups[2].Value.Replace("\\\"", "\"");
                        if (m.Groups[1].Value == "$")
                        {
                            var n = 0;
                            text = hole.Replace(text, _ => "{" + n++ + "}");
                        }
                        texts.Add(text);
                    }
                }
            }
            return texts;
        }

        private static string WebProjectDir()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "VotingApp.sln"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "VotingApp");
        }

        private static List<string> KeysUsedInViews()
        {
            var web = WebProjectDir();
            var pattern = new Regex(@"T(?:\.Html\(|\[)\s*""((?:[^""\\]|\\.)*)""");
            var keys = Directory.EnumerateFiles(web, "*.cshtml", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                .SelectMany(f => pattern.Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value.Replace("\\\"", "\"")))
                .Concat(DynamicKeys)
                .Distinct()
                .ToList();
            Assert.True(keys.Count > 100, "Expected the views to use the translator.");
            return keys;
        }

        private static Dictionary<string, string> Load(string language) =>
            JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(Path.Combine(WebProjectDir(), "Resources", "i18n", language + ".json")));

        private static string Placeholders(string text) =>
            string.Join(",", Regex.Matches(text, @"\{\d+\}").Select(m => m.Value).OrderBy(x => x));

        private static string Tags(string text) =>
            string.Join(",", Regex.Matches(text, @"</?\w+>").Select(m => m.Value).OrderBy(x => x));

        [Theory]
        [InlineData("pt")]
        [InlineData("tet")]
        public void Every_view_text_is_translated(string language)
        {
            var catalog = Load(language);
            var missing = KeysUsedInViews().Where(k => !catalog.TryGetValue(k, out var v) || string.IsNullOrWhiteSpace(v)).ToList();
            Assert.True(missing.Count == 0, $"Missing in {language}.json:\n" + string.Join("\n", missing));
        }

        [Theory]
        [InlineData("pt")]
        [InlineData("tet")]
        public void Translations_keep_placeholders_and_markup(string language)
        {
            var problems = Load(language)
                .Where(kv => !kv.Key.StartsWith("_", StringComparison.Ordinal))
                .Where(kv => Placeholders(kv.Key) != Placeholders(kv.Value) || Tags(kv.Key) != Tags(kv.Value))
                .Select(kv => $"{kv.Key} => {kv.Value}")
                .ToList();
            Assert.True(problems.Count == 0, $"Placeholder or markup mismatch in {language}.json:\n" + string.Join("\n", problems));
        }

        [Fact]
        public void Language_files_have_no_stale_entries()
        {
            var used = KeysUsedInViews().ToHashSet();
            used.UnionWith(TextsUsedInCode());
            foreach (var language in Languages)
            {
                var stale = Load(language).Keys.Where(k => !k.StartsWith("_", StringComparison.Ordinal) && !used.Contains(k)).ToList();
                Assert.True(stale.Count == 0, $"Entries in {language}.json no longer used by any view:\n" + string.Join("\n", stale));
            }
        }
    
        private static VotingApp.Web.Localization.TranslationCatalog Catalog()
        {
            return new VotingApp.Web.Localization.TranslationCatalog(new FakeWebHost(WebProjectDir()));
        }

        private sealed class FakeWebHost : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
        {
            public FakeWebHost(string root) { ContentRootPath = root; }
            public string WebRootPath { get; set; }
            public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; }
            public string ApplicationName { get; set; } = "VotingApp";
            public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            public string ContentRootPath { get; set; }
            public string EnvironmentName { get; set; } = "Test";
        }

        [Theory]
        [InlineData("Votes cast (520) exceed registered voters (500).", "Os votos expressos (520) excedem os eleitores inscritos (500).")]
        [InlineData("Ballots received is required.", "Boletins recebidos é obrigatório.")]
        [InlineData("Only entered sheets can be verified (this one is Approved).", "Só é possível verificar atas introduzidas (esta está Aprovada).")]
        [InlineData("The entries differ in: Ballots used, Party X.", "As introduções diferem em: Boletins utilizados, Party X.")]
        [InlineData("Sheet approved and counted.", "Ata aprovada e contada.")]
        [InlineData("Status set to Voting.", "Estado alterado para Votação.")]
        [InlineData("Line 3: gender must be F or M.", "Linha 3: o sexo deve ser F ou M.")]
        [InlineData("Positions 4-6 need at least one woman.", "As posições 4-6 precisam de pelo menos uma mulher.")]
        [InlineData("Roles updated for ana@example.test.", "Funções atualizadas para ana@example.test.")]
        [InlineData("Something nobody translated (42).", "Something nobody translated (42).")]
        public void Service_messages_are_translated_with_their_values(string english, string portuguese)
        {
            var catalog = Catalog();
            Assert.Equal(portuguese, catalog.TranslateMessage("pt", english));
            Assert.Equal(english, catalog.TranslateMessage("en", english));
        }

        [Fact]
        public void Result_sheet_labels_are_translated()
        {
            var catalog = Catalog();
            var labels = typeof(VotingApp.Models.SheetFormModel).GetProperties()
                .Select(p => p.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
                    .Cast<System.ComponentModel.DataAnnotations.DisplayAttribute>().FirstOrDefault()?.Name)
                .Where(n => n != null)
                .ToList();
            Assert.NotEmpty(labels);
            foreach (var language in Languages)
            {
                var untranslated = labels.Where(l => catalog.Translate(language, l) == l).ToList();
                Assert.True(untranslated.Count == 0, $"{language}: " + string.Join(", ", untranslated));
            }
        }
    
        [Fact]
        public void Password_rule_errors_follow_the_language()
        {
            var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            context.Request.Headers["Cookie"] = "lang=pt";
            var accessor = new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = context };
            var errors = new VotingApp.Web.Localization.TranslatedIdentityErrors(new VotingApp.Web.Localization.Texts(Catalog(), accessor));

            Assert.Equal("A palavra-passe deve ter pelo menos 8 caracteres.", errors.PasswordTooShort(8).Description);
            Assert.Equal("PasswordRequiresDigit", errors.PasswordRequiresDigit().Code);
            Assert.Equal("A palavra-passe deve conter um algarismo (0-9).", errors.PasswordRequiresDigit().Description);
        }
    }
}
