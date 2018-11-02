using System;
using System.IO;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Json;

namespace TNT.CSharp
{
	public static class Extensions
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static string t(this string original)
		{
			return
				Translations.TryGetValue(original, out var translated)
					? translated
					: original;
		}

		static readonly Dictionary<string, string> Translations = LoadTranslations();

		static Dictionary<string, string> LoadTranslations()
		{
			var baseDirectory = GetEntryAssemblyDirectory();
			var translations =
				GetLanguagesToLookFor(CultureInfo.CurrentUICulture)
				.Select(language => Path.Combine(baseDirectory, ".tnt/translation-" + language + ".json"))
				.Where(File.Exists)
				.Select(path => File.ReadAllText(path, Encoding.UTF8))
				.Select(JsonValue.Parse)
				.SelectMany(GetTranslationRecords);

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
		static string GetEntryAssemblyDirectory()
		{
			var codeBase = Assembly.GetEntryAssembly().CodeBase;
			var uri = new UriBuilder(codeBase);
			var path = Uri.UnescapeDataString(uri.Path);
			return Path.GetDirectoryName(path);
		}

		// Get languages from more specific to less specific.
		static IEnumerable<string> GetLanguagesToLookFor(CultureInfo ci)
		{
			yield return ci.Name;
			if (ci.Parent != null)
				foreach (var l in GetLanguagesToLookFor(ci.Parent))
					yield return l;
		}

		static (string, string)[] GetTranslationRecords(JsonValue value)
		{
			return ((JsonArray)value["records"])
					.Select(record => ((string)record[0], (string)record[1], (string)record[2]))
					.Where(t => UsableStates.Contains(t.Item1))
					.Select(t => (t.Item2, t.Item3))
					.ToArray();
		}

		static readonly string[] UsableStates = { "needsReview", "final" };
	}
}
 