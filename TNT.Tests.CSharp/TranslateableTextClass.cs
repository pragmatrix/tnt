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
}
