using System;
using System.Collections.Generic;
using Directory = Pri.LongPath.Directory;
using Path = Pri.LongPath.Path;

namespace MadsKristensen.NodeServiceInstaller
{
	public class LongPathScanner
	{
		const int LongPathLimit = 240;

		readonly List<string> _longPaths = new List<string>();
		readonly List<string> _longestPaths = new List<string>();

		public int LengthOffset { get; set; }
		public int LongestPathLength { get; set; }

		public List<string> LongPaths
		{
			get
			{
				return _longPaths;
			}
		}

		public List<string> LongestPaths
		{
			get
			{
				return _longestPaths;
			}
		}

		public void Scan(string baseDirectory, string assumeRootPath)
		{
			if (baseDirectory == null)
				throw new ArgumentNullException("baseDirectory");
			if (assumeRootPath == null)
				throw new ArgumentNullException("assumeRootPath");

			LengthOffset = assumeRootPath.Length - baseDirectory.Length;
			Scan(baseDirectory);
		}

		public void Scan(string baseDirectory)
		{
			LongestPathLength = 0;
			_longPaths.Clear();
			_longestPaths.Clear();

			baseDirectory = Path.GetFullPath(baseDirectory);
			ScanInternal(baseDirectory);
			_longPaths.Sort((a,b) =>
			{
				int x = b.Length.CompareTo(a.Length);
				if (x != 0)
					return x;
				return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
			});
		}

		void ScanInternal(string dir)
		{
			string[] dirs = Directory.GetDirectories(dir);
			foreach (string i in dirs)
			{
				string s = i + @"\";
				int x = s.Length + LengthOffset;
				if (x > LongPathLimit)
					_longPaths.Add(s);
				if (x == LongestPathLength)
					_longestPaths.Add(i);
				else if (x > LongestPathLength)
				{
					LongestPathLength = x;
					_longestPaths.Clear();
					_longestPaths.Add(s);
				}
				ScanInternal(i);
			}

			string[] files = Directory.GetFiles(dir);
			foreach (string i in files)
			{
				int x = i.Length + LengthOffset;
				if (x > LongPathLimit)
					_longPaths.Add(i);
				if (x == LongestPathLength)
					_longestPaths.Add(i);
				else if (x > LongestPathLength)
				{
					LongestPathLength = x;
					_longestPaths.Clear();
					_longestPaths.Add(i);
				}
			}
		}
	}
}