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
			var tt = ThreadTranslations;
			if (tt == null)
			{
				tt = ResolveTranslations();
				ThreadTranslations = tt;
			}

			return
				tt.TryGetValue(original, out var translated)
					? translated
					: original;
		}

		[ThreadStatic] static Dictionary<string, string> ThreadTranslations;
		static Dictionary<string, string> GlobalTranslations;
		static readonly object Section = new object();

		static Dictionary<string, string> ResolveTranslations()
		{
			lock (Section)
			{
				var global = GlobalTranslations;
				if (global != null)
					return global;
				global = LoadTranslations();
				GlobalTranslations = global;
				return global;
			}
		}

		static Dictionary<string, string> LoadTranslations()
		{
			var translations =
				GetLanguagesToLookFor(CultureInfo.CurrentUICulture)
				.SelectMany(language => 
					GetTranslationFilePaths(GetEntryAssemblyDirectory(), language)
						.OrderBy(path => path))
				.Select(path => File.ReadAllText(path, Encoding.UTF8))
				.Select(JsonValue.Parse)
				.SelectMany(GetTranslations);

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

		static readonly string[] UsableStates = { "needsReview", "final" };

		static (string, string)[] GetTranslations(JsonValue value)
		{
			return ((JsonArray)value["records"])
					.Select(record => (record[0].ToString(), record[1].ToString(), record[2].ToString()))
					.Where(t => UsableStates.Contains(t.Item1))
					.Select(t => (t.Item2, t.Item3))
					.ToArray();
		}

		// Get languages from more specific to less specific.
		static IEnumerable<string> GetLanguagesToLookFor(CultureInfo ci)
		{
			yield return ci.Name;
			if (ci.Parent != null)
				foreach (var l in GetLanguagesToLookFor(ci.Parent))
					yield return l;
		}

		static IEnumerable<string> GetTranslationFilePaths(string directory, string language)
		{
			return Directory.GetFiles(directory, "*" + language + ".tnt");
		}

		// https://stackoverflow.com/questions/52797/how-do-i-get-the-path-of-the-assembly-the-code-is-in
		static string GetEntryAssemblyDirectory()
		{
			var codeBase = Assembly.GetEntryAssembly().CodeBase;
			var uri = new UriBuilder(codeBase);
			var path = Uri.UnescapeDataString(uri.Path);
			return Path.GetDirectoryName(path);
		}
	}
}
 