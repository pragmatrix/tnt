using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Json;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TNT
{
    static class TranslationLoader
    {
        public static Dictionary<string, string> LoadTranslation()
        {
            var languageTags = GetLanguagesToLookFor(CultureInfo.CurrentUICulture).ToArray();
            return LoadTranslation(languageTags);
        }

        public static Dictionary<string, string> LoadTranslation(IEnumerable<string> languageTags)
        {
            var baseDirectory = GetEntryAssemblyDirectory();
            var translations =
                languageTags
                    .Select(language => Path.Combine(baseDirectory, ".tnt-content/" + language + ".tnt"))
                    .Where(File.Exists)
                    .Select(path => File.ReadAllText(path, Encoding.UTF8))
                    .Select(JsonValue.Parse)
                    .SelectMany(GetTranslationPairs);

            var table = new Dictionary<string, string>();

            foreach (var (original, translated) in translations)
            {
                // languages are processed from more specific to less specific,
                // so if the table contains the original already, it's defined in a more
                // specific translation.
                if (!table.ContainsKey(original))
                    table.Add(original, translated);
            }

            return table;
        }

        // https://stackoverflow.com/questions/52797/how-do-i-get-the-path-of-the-assembly-the-code-is-in
        public static string GetEntryAssemblyDirectory()
        {
            var codeBase = Assembly.GetEntryAssembly().CodeBase;
            var uri = new UriBuilder(codeBase);
            var path = Uri.UnescapeDataString(uri.Path);
            return Path.GetDirectoryName(path);
        }

        // Get languages from more specific to less specific.
        static IEnumerable<string> GetLanguagesToLookFor(CultureInfo ci)
        {
            // did we reach the invariant culture?
            if (ci.Name == "")
                yield break;
            yield return ci.Name;
            if (ci.Parent != null)
                foreach (var l in GetLanguagesToLookFor(ci.Parent))
                    yield return l;
        }

        static (string, string)[] GetTranslationPairs(JsonValue value)
        {
            return ((JsonArray) value)
                .Select(pair => ((string) pair[0], (string) pair[1]))
                .ToArray();
        }
    }
}

