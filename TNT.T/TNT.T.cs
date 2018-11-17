using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace TNT
{
    public static class T
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string t(this string original)
        {
            return
                Translations.TryGetValue(original, out var translated)
                    ? translated
                    : original;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string t(FormattableString formattable)
        {
            var translatedFormattableString =
                Translations.TryGetValue(formattable.Format, out var translated)
                    ? FormattableStringFactory.Create(translated, formattable.GetArguments())
                    : formattable;

            return translatedFormattableString.ToString();
        }

        static readonly Dictionary<string, string> Translations = TranslationLoader.LoadTranslation();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string t(this string original, string languageTag)
        {
            lock (Section)
            {
                if (SpecificTranslations.TryGetValue(languageTag, out var translation))
                {
                    return translation.TryGetValue(original, out var translated)
                        ? translated
                        : original;
                }

                // no merging of languages supported for now.
                SpecificTranslations.Add(
                    languageTag, 
                    TranslationLoader.LoadTranslation(new[] {languageTag}));

                return t(original, languageTag);
            }
        }

        static readonly object Section = new object();
        static readonly Dictionary<string, Dictionary<string, string>> SpecificTranslations = 
            new Dictionary<string, Dictionary<string, string>>();
    }
}