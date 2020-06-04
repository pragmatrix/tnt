using System;
using System.Runtime.CompilerServices;
using static TNT.T;

namespace TNT.Tests.CSharp
{
    public class TranslateableTextClass
    {
        public static readonly string TranslateMe = "original".t();
    }

    [CompilerGenerated]
    public class CompilerGeneratedTextClass
    {
        public static readonly string TranslateMe = "originalCG".t();
    }

    public class NonEmptyStringWithConcatenationCantBeExtracted
    {
        public static readonly string TranslateMe = mkStr();

        static string mkStr()
        {
            return ("xx" + new Random().Next()).t();
        }
    }

    public class EmptyStringWithConcatenation
    {
        public static readonly string TranslateMe = mkStr();

        static string mkStr()
        {
            // note: this extraction did not work before an update to a more recent (3.0 or 3.1) dotnet core version.
            // I am not sure if we even want to extract empty strings anyway. But this works now.
            return ("" + new Random().Next()).t();
        }
    }

    public class Explicit
    {
        public static readonly string TranslateMe = "explicit".t("en");

        public static readonly string Language = "en";
        public static readonly string TranslateMe2 = "explicit2".t(Language + "-US");
    }

    public class FormattableString
    {
        public static readonly string String = "String";
        public static readonly string TranslateMe = t($"Formattable {String}");
    }

}
