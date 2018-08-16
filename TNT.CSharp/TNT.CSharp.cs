using System.Runtime.CompilerServices;

namespace TNT.CSharp
{
	public static class Extensions
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static string t(this string original)
		{
			return original;
		}
	}
}
