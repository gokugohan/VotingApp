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
        private static readonly string[] DynamicKeys = { "Setup", "Voting", "Counting", "Provisional", "Certified", "Party", "Coalition" };

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
            foreach (var language in Languages)
            {
                var stale = Load(language).Keys.Where(k => !k.StartsWith("_", StringComparison.Ordinal) && !used.Contains(k)).ToList();
                Assert.True(stale.Count == 0, $"Entries in {language}.json no longer used by any view:\n" + string.Join("\n", stale));
            }
        }
    }
}
