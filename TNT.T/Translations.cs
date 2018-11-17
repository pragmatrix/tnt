using System.IO;
using System.Linq;

namespace TNT
{
    /// Provides information about the translations available.
    public static class Translations
    {
        /// Returns language tags of the translations available.
        public static string[] Available
        {
            get
            {
                var contentPath = Path.Combine(TranslationLoader.GetEntryAssemblyDirectory(), ".tnt-content");
                return
                    Directory.EnumerateFiles(contentPath, "*.tnt")
                        .Select(Path.GetFileNameWithoutExtension)
                        .ToArray();
            }
        }
    }
}
