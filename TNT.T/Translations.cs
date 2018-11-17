using System.IO;
using System.Linq;

namespace TNT
{
    /// Provides information about the translations available.
    public static class Translations
    {
        /// Returns language tags of the translations available.
        public static readonly string[] Available = ResolveAvailableLanguages();

        static string[] ResolveAvailableLanguages()
        {
            var contentPath = Path.Combine(TranslationLoader.GetEntryAssemblyDirectory(), ".tnt-content");
            return
                Directory.EnumerateFiles(contentPath, "*.tnt")
                    .Select(Path.GetFileNameWithoutExtension)
                    .ToArray();
        }
    }
}

