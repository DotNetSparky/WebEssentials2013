using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using DirectoryInfo = Pri.LongPath.DirectoryInfo;
using File = Pri.LongPath.File;
using FileInfo = Pri.LongPath.FileInfo;
using FileSystemInfo = Pri.LongPath.FileSystemInfo;
using Path = Pri.LongPath.Path;

namespace MadsKristensen.NodeServiceInstaller
{
	public static class ZipPackager
	{
		const int MinimumYear = 1980;
		const int MaximumYear = 2107;

		public static void CreateFromDirectory(string sourceDirectory, string targetPackageFileName, params string[] excludePaths)
		{
			sourceDirectory = Path.GetFullPath(sourceDirectory);

			// if the destination file is located within the source directory, add it to the exclude list
			string targetPackagePath = Path.GetFullPath(targetPackageFileName);
			List<string> excludeFullPaths = new List<string>(excludePaths == null ? 1 : (excludePaths.Length + 1))
			{
				targetPackagePath
			};
			if (excludePaths != null)
				excludeFullPaths.AddRange(excludePaths.Select(i => Path.IsPathRooted(i) ? Path.GetFullPath(i) : Path.Combine(sourceDirectory, i)));

			using (ZipArchive archive = ZipFile.Open(targetPackagePath, ZipArchiveMode.Create))
			{
				bool directoryIsEmpty = true;
				DirectoryInfo directoryInfo = new DirectoryInfo(sourceDirectory);
				string parentPath = directoryInfo.FullName;
				foreach (FileSystemInfo i in directoryInfo.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
				{
					directoryIsEmpty = false;
					string fullPath = i.FullName;
					if (!excludeFullPaths.Exists(x => string.Equals(fullPath, x, StringComparison.OrdinalIgnoreCase)))
					{
						int relativePathLength = i.FullName.Length - parentPath.Length;
						string relativePath = i.FullName.Substring(parentPath.Length, relativePathLength).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
						if (i is FileInfo)
						{
							using (Stream stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
							{
								ZipArchiveEntry zipArchiveEntry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
								DateTime dateTime = File.GetLastWriteTime(fullPath);
								if (dateTime.Year < MinimumYear || dateTime.Year > MaximumYear)
									dateTime = new DateTime(MinimumYear, 1, 1, 0, 0, 0);
								zipArchiveEntry.LastWriteTime = dateTime;
								using (Stream destination1 = zipArchiveEntry.Open())
								{
									stream.CopyTo(destination1);
								}
							}
						}
						else
						{
							DirectoryInfo possiblyEmptyDir = i as DirectoryInfo;
							if (possiblyEmptyDir != null && IsDirEmpty(possiblyEmptyDir))
								archive.CreateEntry(relativePath + Path.DirectorySeparatorChar);
						}
					}
				}
				if (directoryIsEmpty)
					archive.CreateEntry(directoryInfo.Name + Path.DirectorySeparatorChar);
			}
		}

		private static bool IsDirEmpty(DirectoryInfo possiblyEmptyDir)
		{
			return !possiblyEmptyDir.EnumerateFileSystemInfos("*", SearchOption.AllDirectories).Any();
		}
	}
}