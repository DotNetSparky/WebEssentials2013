using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Directory = Pri.LongPath.Directory;
using DirectoryInfo = Pri.LongPath.DirectoryInfo;
using File = Pri.LongPath.File;
using FileInfo = Pri.LongPath.FileInfo;
using Path = Pri.LongPath.Path;

namespace MadsKristensen.NodeServiceInstaller
{
	public class NodeInstaller : TaskBase // Microsoft.Build.Utilities.Task
	{
		private readonly List<string> _toRemove = new List<string>()
		{
			"CNAME",
			"*.old",
			"*.patch",
			"*.ico",
			"Makefile.*",
			"Rakefile",
			"*.yml",
			"test.*",
			"generate-*",
			"benchmark",
			"build",
			"scripts",
			"test",
			"tst",
			"tests",
			"testing",
			"*.tscache",
		};

		private readonly List<string> _docFiles = new List<string>()
		{
			"*.md",
			"*.markdown",
			"*.html",
			"*.txt",
			"LICENSE",
			"README",
			"CHANGELOG",
			"media",
			"images",
			"man",
			"examples",
			"example",
		};

		// resources\nodejs
		const string TypicalInstallBasePath = @"C:\Users\xxxxxxxxxx\AppData\Local\Microsoft\VisualStudio\12.0\Extensions\xxxxxxxx.zzz\Resources\nodejs";
		const string ToolsFolderName = "tools";
		const string DocsFolderName = "node_modules_docs";
		const string ZipPackageFileName = "NodePackage.zip";
		const string DocsPackageFileName = "NodeDocs.zip";
		const string ReadmeDocsFileName = "README - LICENSE.txt";
		const string ReadmeRemovedFileName = "README - Missing Files.txt";
		const string LogFileName = "install-log.txt";
		const string NodeExeDownloadUrl = "http://nodejs.org/dist/latest/node.exe";
		const string NpmDownloadUrl = "http://nodejs.org/dist/npm/npm-1.4.9.zip";
		const string ModulePackageJsonFileName = "package.json";
		const string NodeExeFileName = "node.exe";
		const string NpmExeFileName = "npm.cmd";
		const string CommandExe = "cmd.exe";
		const string NpmModuleName = "npm";

		// TODO: rename _mdoulesChanged to be _packageDirty
		bool _modulesChanged;
		string _toolsPath;
		string _nodeExePath;
		string _npmCmdPath;
		string _logPath;

		public string InstallDirectory { get; set; }
		public string PackageOutputDirectory { get; set; }
		public bool CleanTarget { get; set; }
		public bool InstallNode { get; set; }
		public bool OptimizeNpm { get; set; }
		public bool InstallModules { get; set; }
		public bool OptimizeModules { get; set; }

		public NodeInstaller()
		{
			CleanTarget = false;
			InstallNode = true;
			InstallModules = true;
			OptimizeModules = false;
		}

		public override bool Execute()
		{
			if (string.IsNullOrEmpty(InstallDirectory))
				throw new InvalidOperationException("BaseDirectory not set.");

			if (string.IsNullOrEmpty(InstallDirectory))
				InstallDirectory = Directory.GetCurrentDirectory();

			InstallDirectory = Path.GetFullPath(InstallDirectory);

			if (string.IsNullOrEmpty(PackageOutputDirectory))
				PackageOutputDirectory = InstallDirectory;

			// TODO: (Shaun's note to self) contribute change to Pri.LongPath to make Path.Combine match the framework's interface (add 'params' option and use a better concantenation method)
			_nodeExePath = Path.Combine(InstallDirectory, NodeExeFileName);
			_npmCmdPath = Path.Combine(InstallDirectory, NpmExeFileName);
			_toolsPath = Path.Combine(InstallDirectory, ToolsFolderName);

			_modulesChanged = false;

			Directory.CreateDirectory(PackageOutputDirectory);

//			if (CleanTarget)
//				ClearPath(InstallDirectory);
			Directory.CreateDirectory(_toolsPath);
			_logPath = Path.Combine(InstallDirectory, LogFileName);
			Log.LogFile = _logPath;

			// Force npm to install modules to the subdirectory
			// https://npmjs.org/doc/files/npm-folders.html#More-Information

			// TODO: write out full contents of package.json (or should we just include this file in the project source--would be a good way for editing versions without having to recompile)

			// We install our modules in this subdirectory so that
			// we can clean up their dependencies without catching
			// npm's modules, which we don't want.
			//File.WriteAllText(Path.Combine(_toolsPath, ModulePackageJsonFileName), "{}");

			// Since this is a synchronous job, I have
			// no choice but to synchronously wait for
			// the tasks to finish. However, the async
			// still saves threads.

			try
			{
				if (InstallNode)
					DownloadNode();
				if (InstallModules)
					InstallNodeModules();
				CheckForLongPaths();
				if (OptimizeModules)
				{
					ReorganizeModules();
//					DedupeNodeModules();
//					ExtractDocFiles();
//					FlattenNodeModules(_toolsPath);
//					CleanPath();
//
				}
//				if (_modulesChanged)
//					CreateDocsPackage();
//
				CheckForLongPaths();
//
//				CreatePackage();
			}
			catch (AggregateException ae)
			{
				ae.Handle(x =>
				{
					Log.LogMessage("Unhandled exception: " + x.GetType() + " " + x.Message);
					return false;
				});
			}
			catch (Exception ex)
			{
				Log.LogMessage("Unhandled exception: " + ex.GetType() + " " + ex.Message);
				return false;
			}
			return true;
		}

		private void ClearPath(string path)
		{
			string[] dirs = Directory.GetDirectories(path);
			foreach (string dir in dirs)
			{
				Log.LogMessage(MessageImportance.High, "Removing " + dir + "...");
				Directory.Delete(dir, true);
			}

			string[] files = Directory.GetFiles(path);
			foreach (string file in files)
			{
				Log.LogMessage(MessageImportance.High, "Removing " + file + "...");
				File.Delete(file);
			}
		}

		private void ExtractDocFiles()
		{
			Log.LogMessage(MessageImportance.High, "Extracting docs...");

			string docsFolder = Path.Combine(InstallDirectory, DocsFolderName);
			if (ExtractDocFilesInternal(_toolsPath, docsFolder))
				_modulesChanged = true;
		}

		bool ExtractDocFilesInternal(string path, string docPath)
		{
			bool dirCreated = false;
			bool madeChanges = false;

			foreach (string pattern in _docFiles)
			{
				string[] dirs = Directory.GetDirectories(path, pattern);
				foreach (string dir in dirs)
				{
					if (!dirCreated)
					{
						Directory.CreateDirectory(docPath);
						dirCreated = true;
					}
					string destination = Path.Combine(docPath, Path.GetFileName(dir));
					Log.LogMessage(MessageImportance.High, dir + "/ --> " + destination + "...");
					Directory.Move(dir, destination);
					madeChanges = true;
				}

				string[] files = Directory.GetFiles(path, pattern);
				foreach (string file in files)
				{
					if (!dirCreated)
					{
						Directory.CreateDirectory(docPath);
						dirCreated = true;
					}
					string destination = Path.Combine(docPath, Path.GetFileName(file));
					Log.LogMessage(MessageImportance.High, file + " --> " + destination + "...");
					File.Move(file, destination);
					madeChanges = true;
				}
			}

			string[] tocheck = Directory.GetDirectories(path);
			foreach (string s in tocheck)
			{
				string destination = Path.Combine(docPath, Path.GetFileName(s));
				if (ExtractDocFilesInternal(s, destination))
					madeChanges = true;
			}

			return madeChanges;
		}

		private void CreateDocsPackage()
		{
			// TODO: create as a temp file and then rename once finished
			string targetZipFilePath = Path.Combine(PackageOutputDirectory, DocsPackageFileName);
			if (File.Exists(targetZipFilePath))
				File.Delete(targetZipFilePath);
			string docsFolder = Path.Combine(InstallDirectory, DocsFolderName);
			if (Directory.Exists(docsFolder))
			{
				Log.LogMessage("Packaging docs --> " + DocsPackageFileName);
				ZipPackager.CreateFromDirectory(docsFolder, targetZipFilePath);
				File.WriteAllText(Path.Combine(PackageOutputDirectory, ReadmeDocsFileName), string.Format("Various documentation files (README's, licenses, etc.) can be found in {0}.{1}", DocsPackageFileName, Environment.NewLine));
				Log.LogMessage("Zip package created.");
			}
		}

		private void CleanPath()
		{
			Log.LogMessage(MessageImportance.High, "Cleaning modules...");

			var s = new StringBuilder();
			string pathToClean = Path.Combine(_toolsPath, NodeModule.ModulesDirectoryName);
			CleanPathInternal(s, pathToClean, pathToClean.Length);
			if (s.Length > 0)
			{
				File.WriteAllText(Path.Combine(InstallDirectory, ReadmeRemovedFileName), "The following files were removed for the purpose of optimizing the package size:" + Environment.NewLine + Environment.NewLine + s);
			}
		}

		private void CleanPathInternal(StringBuilder output, string path, int pathPrefixLength)
		{
			//Log.LogMessage(MessageImportance.High, "Cleaning " + path + "...");
			foreach (string pattern in _toRemove)
			{
				string[] dirs = Directory.GetDirectories(path, pattern);
				foreach (string dir in dirs)
				{
					Log.LogMessage(MessageImportance.High, "  del " + dir + "/...");
					output.Append(dir.Substring(pathPrefixLength));
					output.AppendLine("/*");
					Directory.Delete(dir, true);
				}

				string[] files = Directory.GetFiles(path, pattern);
				foreach (string file in files)
				{
					Log.LogMessage(MessageImportance.High, "  del " + file + "...");
					output.AppendLine(file.Substring(pathPrefixLength));
					File.Delete(file);
				}
			}

			string[] tocheck = Directory.GetDirectories(path);
			foreach (string s in tocheck)
			{
				CleanPathInternal(output, s, pathPrefixLength);
			}
		}

		void DownloadNode()
		{
			try
			{
				Task.WaitAll(
					DownloadNodeAsync(),
					DownloadNpmAsync()
				);
			}
			catch (AggregateException ae)
			{
				ae.Handle(x =>
				{
					Log.LogMessage("Unhandled exception: " + x.GetType() + " " + x.Message);
					return false;
				});
				throw;
			}
		}

		Task DownloadNodeAsync()
		{
			var file = new FileInfo(_nodeExePath);
			if (file.Exists && file.Length > 0)
			{
				Log.LogMessage(MessageImportance.Low, "nodejs exists, skip download");
				return Task.FromResult<object>(null);
			}
			_modulesChanged = true;
			Log.LogMessage(MessageImportance.High, "Downloading nodejs ...");
			return WebClientDoAsync(wc => wc.DownloadFileTaskAsync(NodeExeDownloadUrl, _nodeExePath));
		}

		async Task DownloadNpmAsync()
		{
			var file = new FileInfo(_npmCmdPath);
			if (file.Exists && file.Length > 0)
			{
				Log.LogMessage(MessageImportance.Low, "npm exists, skip download");
				return;
			}

			_modulesChanged = true;

			Log.LogMessage(MessageImportance.High, "Downloading npm ...");
			var npmZip = await WebClientDoAsync(wc => wc.OpenReadTaskAsync(NpmDownloadUrl));

			try
			{
				ExtractZipWithOverwrite(npmZip, InstallDirectory);

				await Task.Delay(3000);

				Log.LogMessage(MessageImportance.High, "Updating npm...");
				var output = await ExecWithOutputAsync(@"cmd", @"/c " + _npmCmdPath + " install npm@latest", InstallDirectory, "npm");

				if (output.ExitCode != 0)
				{
					Log.LogError("[npm] error: " + output.StdError);
					throw new InvalidOperationException("npm failed to update.");
				}
			}
			catch (Exception ex)
			{
				Log.LogMessage(MessageImportance.High, "EXCEPTION while extracting npm: " + ex.GetType() + " " + ex.Message);
				// Make sure the next build doesn't see a half-installed npm
				Directory.Delete(Path.Combine(InstallDirectory, NodeModule.ModulesDirectoryName, NpmModuleName), true);
				throw;
			}
		}

		async Task WebClientDoAsync(Func<WebClient, Task> transactor)
		{
			try
			{
				await transactor(new WebClient());
				return;
			}
			catch (WebException e)
			{
				Log.LogWarningFromException(e);
				if (!IsHttpStatusCode(e, HttpStatusCode.ProxyAuthenticationRequired))
					throw;
			}

			await transactor(CreateWebClientWithProxyAuthSetup());
		}

		async Task<T> WebClientDoAsync<T>(Func<WebClient, Task<T>> transactor)
		{
			try
			{
				return await transactor(new WebClient());
			}
			catch (WebException e)
			{
				Log.LogWarningFromException(e);
				if (!IsHttpStatusCode(e, HttpStatusCode.ProxyAuthenticationRequired))
					throw;
			}

			return await transactor(CreateWebClientWithProxyAuthSetup());
		}

		static bool IsHttpStatusCode(WebException e, HttpStatusCode status)
		{
			HttpWebResponse response;
			return e.Status == WebExceptionStatus.ProtocolError
				&& (response = e.Response as HttpWebResponse) != null
				&& response.StatusCode == status;
		}

		static WebClient CreateWebClientWithProxyAuthSetup(IWebProxy proxy = null, ICredentials credentials = null)
		{
			var wc = new WebClient { Proxy = proxy ?? WebRequest.GetSystemWebProxy() };
			wc.Proxy.Credentials = credentials ?? CredentialCache.DefaultCredentials;
			return wc;
		}

		void InstallNodeModules()
		{
			try
			{
				var moduleResults = Task.WhenAll(
					InstallModuleAsync("jscs", "jscs", _toolsPath),
					InstallModuleAsync("lessc", "less", _toolsPath),
					InstallModuleAsync("handlebars", "handlebars", _toolsPath),
					InstallModuleAsync("jshint", "jshint", _toolsPath),
					InstallModuleAsync("tslint", "tslint", _toolsPath),
					InstallModuleAsync("node-sass", "node-sass", _toolsPath),
					InstallModuleAsync("coffee", "coffee-script", _toolsPath),
					InstallModuleAsync("autoprefixer", "autoprefixer", _toolsPath),
					InstallModuleAsync("iced", "iced-coffee-script", _toolsPath),
					InstallModuleAsync("LiveScript", "LiveScript", _toolsPath),
					InstallModuleAsync("coffeelint", "coffeelint", _toolsPath),
					InstallModuleAsync("sjs", "sweet.js", _toolsPath),
					InstallModuleAsync(null, "xregexp", _toolsPath),
					InstallModuleAsync("rtlcss", "rtlcss", _toolsPath),
					InstallModuleAsync("cson", "cson", _toolsPath)
				).Result.Where(r => r.Result != InstallResult.AlreadyPresent).ToArray();

				if (moduleResults.Any(x => x.Result == InstallResult.Error))
					throw new InvalidOperationException("One or more modules failed to install.");

				if (!moduleResults.Any())
					Log.LogMessage(MessageImportance.High, "All modules are already installed.");

				Log.LogMessage(MessageImportance.High, "Installed " + moduleResults.Length + " modules.");
			}
			catch (AggregateException ae)
			{
				ae.Handle(x =>
				{
					Log.LogMessage("Unhandled exception: " + x.GetType() + " " + x.Message);
					return false;
				});
			}
		}

		async Task<ModuleInstallResult> InstallModuleAsync(string cmdName, string moduleName, string location)
		{
			string installLocation = Path.Combine(_toolsPath, NodeModule.ModulesDirectoryName, location);
			string moduleLocation = Path.Combine(installLocation, NodeModule.ModulesDirectoryName, moduleName);

			if (string.IsNullOrEmpty(cmdName))
			{
				if (File.Exists(Path.Combine(moduleLocation, ModulePackageJsonFileName)))
				{
					Log.LogMessage(MessageImportance.Normal, "Already installed: " + moduleName);
					return new ModuleInstallResult(moduleName, InstallResult.AlreadyPresent);
				}
			}
			else
			{
				if (File.Exists(Path.Combine(installLocation, NodeModule.ModulesDirectoryName, ".bin", cmdName + ".cmd")))
				{
					Log.LogMessage(MessageImportance.Normal, "Already installed: " + moduleName);
					return new ModuleInstallResult(moduleName, InstallResult.AlreadyPresent);
				}
			}

			Log.LogMessage(MessageImportance.High, "[" + moduleName + "] Installing...");

			var output = await ExecWithOutputAsync(CommandExe, "/c " + _npmCmdPath + " install \"" + moduleName + "\"", installLocation, moduleName);

			if (output.ExitCode != 0)
			{
				Log.LogError("[" + moduleName + "] error: " + output.StdError);
				return new ModuleInstallResult(moduleName, InstallResult.Error);
			}

			return new ModuleInstallResult(moduleName, InstallResult.Installed);
		}

		void DedupeNodeModules()
		{
			Log.LogMessage(MessageImportance.Normal, "Flattening modules - npm dedup...");
			_modulesChanged = true;
			Task<ExecResult> exec = ExecWithOutputAsync(@"cmd", @"/c " + _npmCmdPath + " dedup ", _toolsPath, "npm dedup");
			exec.Wait();
			if (exec.Result.ExitCode != 0)
			{
				Log.LogError("npm dedup error: " + exec.Result.StdError);
				throw new InvalidOperationException("Npm dedupe error");
			}
		}

		/// <summary>Invokes a command-line process asynchronously, capturing its output to a string.</summary>
		/// <returns>Null if the process exited successfully; the process' full output if it failed.</returns>
		async Task<ExecResult> ExecWithOutputAsync(string filename, string args, string workingDirectory, string label)
		{
			using (var output = new StringWriter())
			{
				using (var error = new StringWriter())
				{
					int result = await ExecAsync(filename, args, workingDirectory, output, error, label);
					var o = new ExecResult(result, output.ToString().Trim(), error.ToString().Trim());
					return o;
				}
			}
		}

		/// <summary>Invokes a command-line process asynchronously.</summary>
		Task<int> ExecAsync(string filename, string args, string workingDirectory, TextWriter stdout, TextWriter stderr, string label)
		{
			stdout = stdout ?? TextWriter.Null;
			stderr = stderr ?? TextWriter.Null;

			workingDirectory = workingDirectory != null ? Path.GetFullPath(workingDirectory) : _toolsPath;

			var p = new Process
			{
				StartInfo = new ProcessStartInfo(filename, args)
				{
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardError = true,
					RedirectStandardOutput = true,
					WorkingDirectory = workingDirectory,
				},
				EnableRaisingEvents = true,
			};

			p.OutputDataReceived += (sender, e) =>
			{
				//Log.LogMessage(MessageImportance.Normal, "[" + label + "] stdout: " + e.Data);
				stdout.WriteLine(e.Data);
			};
			p.ErrorDataReceived += (sender, e) =>
			{
				Log.LogMessage(MessageImportance.Normal, "[" + label + "] stderr: " + e.Data);
				stderr.WriteLine(e.Data);
			};

			p.Start();
			p.BeginErrorReadLine();
			p.BeginOutputReadLine();
			var processTaskCompletionSource = new TaskCompletionSource<int>();

			p.EnableRaisingEvents = true;
			p.Exited += (s, e) =>
			{
				p.WaitForExit();
				Log.LogMessage(MessageImportance.Normal, "[" + label + "] " + (p.ExitCode == 0 ? "Install completed." : ("Install failed, exit code: " + p.ExitCode)));
				processTaskCompletionSource.TrySetResult(p.ExitCode);
			};

			return processTaskCompletionSource.Task;
		}

		void ExtractZipWithOverwrite(Stream sourceZip, string destinationDirectoryName)
		{
			Log.LogMessage(MessageImportance.Normal, "Extract to: " + destinationDirectoryName);
			using (var source = new ZipArchive(sourceZip, ZipArchiveMode.Read))
			{
				foreach (var entry in source.Entries)
				{
					const string prefix = "node_modules/npm/node_modules/";

					var targetSubPath = entry.FullName;
					if (OptimizeNpm)
					{
						// Collapse nested node_modules folders to avoid MAX_PATH issues from Path.GetFullPath
						if (targetSubPath.StartsWith(prefix) && targetSubPath.Length > prefix.Length)
						{
							// If there is another node_modules folder after the prefix, collapse them
							var lastModule = entry.FullName.LastIndexOf("node_modules/", StringComparison.OrdinalIgnoreCase);
							if (lastModule > prefix.Length)
							{
								targetSubPath = targetSubPath.Remove(prefix.Length, lastModule + "node_modules/".Length - prefix.Length);
								Log.LogMessage(MessageImportance.Low, entry.FullName + " => " + targetSubPath);
							}
						}
					}

					var targetPath = Path.GetFullPath(Path.Combine(destinationDirectoryName, targetSubPath));

					Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
					if (!targetPath.EndsWith(@"\"))
						entry.ExtractToFile(targetPath, overwrite: true);
				}
			}
		}

		void CheckForLongPaths()
		{
			Log.LogMessage(MessageImportance.High, "Checking for long paths... (assuming typical install base path of " + TypicalInstallBasePath + ")");

			var scanner = new LongPathScanner();
			scanner.Scan(InstallDirectory, TypicalInstallBasePath);
			Log.LogMessage(MessageImportance.High, string.Format("Found {0} long paths. Longest path length is {1}.", scanner.LongPaths.Count, scanner.LongestPathLength));
		}

		void CreatePackage()
		{
			Log.LogMessage("Creating zip package...");
			string targetZipFilePath = Path.Combine(InstallDirectory, ZipPackageFileName);
			if (File.Exists(targetZipFilePath))
				File.Delete(targetZipFilePath);
			ZipPackager.CreateFromDirectory(InstallDirectory, targetZipFilePath);

			Log.LogMessage("Zip package created.");
		}

		void ReorganizeModules()
		{
			Log.LogMessage("Getting module list...");
			Task<ExecResult> execTask = ExecWithOutputAsync(@"cmd", @"/c " + _npmCmdPath + " la --json", _toolsPath, "npm ls");
			execTask.Wait();

			JObject data = (JObject) JsonConvert.DeserializeObject(execTask.Result.StdOut);
			var package = new NodePackage(".", _toolsPath, Log);
			package.Root.DeserializeJson(data);

			List<string> ignorePaths = new List<string>(_docFiles.Count + _toRemove.Count);
			ignorePaths.AddRange(_docFiles);
			ignorePaths.AddRange(_toRemove);

			package.OptimizeModules(ignorePaths);

			return;

			Log.LogMessage("Moving modules...");
			List<NodeModule> needFixes = new List<NodeModule>();
			foreach (NodeModule i in GetModulesToMove(package.Root))
			{
				string oldPath = i.OriginalPath;
				string newPath = i.RealPath;

				Log.LogMessage("Move:");
				Log.LogMessage("  From: " + oldPath);
				Log.LogMessage("  To:   " + newPath);
				if (Directory.Exists(oldPath))
				{
					string targetParent = Path.GetDirectoryName(newPath);
					Directory.CreateDirectory(targetParent);
					Directory.Move(oldPath, newPath);
				}
				else
				{
					Log.LogMessage("    >> source missing, marked for re-install");
					needFixes.Add(i);
				}
			}

			Log.LogMessage("Removing excess modules...");
			foreach (NodeModule i in GetModulesToRemove(package.Root))
			{
				string oldPath = i.OriginalPath;
				Log.LogMessage("  Remove: " + oldPath);
				if (Directory.Exists(oldPath))
					Directory.Delete(oldPath, true);
				else
					Log.LogMessage("    >> missing");
			}

			if (needFixes.Count > 0)
			{
				Console.Write("Press enter to try re-installing missing modules: ");
				Console.ReadLine();
				foreach (NodeModule i in needFixes)
				{
					Log.LogMessage("Re-installing: " + i);
					string location = i.Parent.RealPath;
					Log.LogMessage("Installing modules into " + location + "...");
					Task<ModuleInstallResult> install = InstallModuleAsync(null, i.Name + "@" + i.Version, location);
					install.Wait();
					if (install.Result.Result == InstallResult.Error)
					{
						Log.LogMessage("Failed to install: " + i);
						throw new InvalidOperationException("Failed to install: " + i);
					}
				}
			}
//				{
//					string location = container.RealPath;
//					Log.LogMessage("Installing modules into " + location + "...");
//					ModuleInstallResult[] results = Task.WhenAll(container.Where(x => x.Add).Select(i => InstallModuleAsync(null, i.Name + "@" + i.Version, location))).Result;
//					if (results.Any(x => x.Result == InstallResult.Error))
//						throw new InvalidOperationException("One or more modules failed to install.");
//					Log.LogMessage("Installed " + results.Count(x => x.Result == InstallResult.Installed).ToString() + " modules.");
//				}

//			Log.LogMessage("Reinstalling moved modules...");
//			try
//			{
//				// we can install all the modules for a common parent simultaneously, but we should wait until those are done before any below it
//				foreach (NodeModule container in package.Root.WalkBreadthFirst().Where(x => x.Children.Any(y => y.Add)))
//				{
//					string location = container.RealPath;
//					Log.LogMessage("Installing modules into " + location + "...");
//					ModuleInstallResult[] results = Task.WhenAll(container.Where(x => x.Add).Select(i => InstallModuleAsync(null, i.Name + "@" + i.Version, location))).Result;
//					if (results.Any(x => x.Result == InstallResult.Error))
//						throw new InvalidOperationException("One or more modules failed to install.");
//					Log.LogMessage("Installed " + results.Count(x => x.Result == InstallResult.Installed).ToString() + " modules.");
//				}
//			}
//			catch (AggregateException ae)
//			{
//				ae.Handle(x =>
//				{
//					Log.LogMessage("Unhandled exception: " + x.GetType() + " " + x.Message);
//					return false;
//				});
//			}
		}

		IEnumerable<NodeModule> GetModulesToMove(NodeModule root)
		{
			var queue = new Queue<NodeModule>();
			queue.Enqueue(root);
			while (queue.Count != 0)
			{
				NodeModule current = queue.Dequeue();
				if (!current.Add)
				{
					foreach (NodeModule i in current)
					{
						queue.Enqueue(i);
					}
				}
				else
					yield return current;
			}
		}

		IEnumerable<NodeModule> GetModulesToRemove(NodeModule root)
		{
			var queue = new Queue<NodeModule>();
			queue.Enqueue(root);
			while (queue.Count != 0)
			{
				NodeModule current = queue.Dequeue();
				if (!current.Remove)
				{
					foreach (NodeModule i in current.Children)
					{
						queue.Enqueue(i);
					}
				}
				else
					yield return current;
			}
		}
	}
}