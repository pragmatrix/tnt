using System;
using System.Runtime.CompilerServices;

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

    public class ExtractionError
    {
        public static readonly string TranslateMe = mkStr();

        static string mkStr()
        {
            return ("" + new Random().Next()).t();
        }
    }

    public class Explicit
    {
        public static readonly string TranslateMe = "explicit".t("en");

        public static readonly string Language = "en";
        public static readonly string TranslateMe2 = "explicit2".t(Language + "-US");
    }
}
