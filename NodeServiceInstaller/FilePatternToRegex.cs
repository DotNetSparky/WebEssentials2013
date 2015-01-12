using System;
using System.Text.RegularExpressions;

namespace MadsKristensen.NodeServiceInstaller
{
	// based on code from http://stackoverflow.com/questions/652037/how-do-i-check-if-a-filename-matches-a-wildcard-pattern

	// TODO: use glob patterns (using Minimatch) instead, for consistency

	public static class FilePatternToRegex
	{
		private const string NonDotCharacters = @"[^.]*";
		private static readonly Regex IlegalCharactersRegex = new Regex(@"[\/:<>|""]", RegexOptions.Compiled);
		private static readonly Regex CatchExtensionRegex = new Regex(@"^\s*.+\.([^\.]+)\s*$", RegexOptions.Compiled);

		public static Regex Convert(string pattern)
		{
			if (string.IsNullOrEmpty(pattern))
				throw new ArgumentException("Pattern cannot be null or empty.", "pattern");

			if (IlegalCharactersRegex.IsMatch(pattern))
				throw new ArgumentException("Pattern contains ilegal characters.", "pattern");

			bool hasExtension = CatchExtensionRegex.IsMatch(pattern);
			bool matchExact = false;
			if (pattern.IndexOf('?') > -1)
				matchExact = true;
			else if (hasExtension)
				matchExact = CatchExtensionRegex.Match(pattern).Groups[1].Length != 3;

			string regexString = Regex.Escape(pattern);
			regexString = "^" + Regex.Replace(regexString, @"\\\*", ".*");
			regexString = Regex.Replace(regexString, @"\\\?", ".");
			if (!matchExact && hasExtension)
				regexString += NonDotCharacters;

			regexString += "$";

			Regex regex = new Regex(regexString, RegexOptions.Compiled | RegexOptions.IgnoreCase);
			return regex;
		}
	}
}