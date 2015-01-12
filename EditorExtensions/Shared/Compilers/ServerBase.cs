using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace MadsKristensen.EditorExtensions
{
	public abstract class ServerBase : IDisposable
	{
		private Process _process;
		private bool _outsideProcess;
		private string _address;
		private int _basePort;
		private string _baseAuthenticationToken;

		public bool ShowWindow { get; set; }

		protected int BasePort
		{
			get
			{
				return _basePort;
			}
			set
			{
				if (_process != null)
					throw new InvalidOperationException("BasePort cannot be changed once server is started.");

				_basePort = value;
				_address = "http://localhost.:" + BasePort.ToString(CultureInfo.InvariantCulture) + "/";
			}
		}

		protected string BaseAuthenticationToken
		{
			get
			{
				if (_baseAuthenticationToken == null)
				{
					byte[] randomNumber = new byte[32];

					using (RandomNumberGenerator crypto = RNGCryptoServiceProvider.Create())
						crypto.GetBytes(randomNumber);

					_baseAuthenticationToken = Convert.ToBase64String(randomNumber);
				}
				return _baseAuthenticationToken;
			}
		}

		private void SelectAvailablePort()
		{
			Random rand = new Random();
			TcpConnectionInformation[] connections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();

			do
				BasePort = rand.Next(1024, 65535);
			while (connections.Any(t => t.LocalEndPoint.Port == BasePort));
		}

		protected HttpClient Client { get; set; }

		protected virtual string HeartbeatCheckPath { get { return ""; } }

		protected ServerBase()
		{
			Client = new HttpClient();
		}

		protected abstract void StartServer();

		protected void InitializeExternalProcess()
		{
			_address = string.Format(CultureInfo.InvariantCulture, "http://127.0.0.1:{0}/", BasePort);
			Client = new HttpClient();

			_process = null;
			_outsideProcess = true;
			Logger.Log(string.Format("Using outside server process for {0}", _address));
		}

		protected void Initialize(string processStartArgumentsFormat, string serverPath)
		{
			if (BasePort == 0)
				SelectAvailablePort();
			_address = string.Format(CultureInfo.InvariantCulture, "http://127.0.0.1:{0}/", BasePort);

			if (!File.Exists(serverPath))
			{
				Logger.Log(string.Format("Could not start server process for {0} - file not found", _address));
			}

			try
			{
				ProcessStartInfo start = new ProcessStartInfo(serverPath)
				{
					WorkingDirectory = Path.GetDirectoryName(serverPath),
					WindowStyle = ShowWindow ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden,
					Arguments = string.Format(CultureInfo.InvariantCulture, processStartArgumentsFormat,
											 BasePort, BaseAuthenticationToken, Process.GetCurrentProcess().Id),
					UseShellExecute = false,
					CreateNoWindow = !ShowWindow
				};

				_process = Process.Start(start);
				Logger.Log(string.Format("Started server process ({0}) for {1}", _process.Id, _address));
			}
			catch (FileNotFoundException ex)
			{
				Logger.Log(string.Format("Could not start server process for {0} - file not found", _address));
				if (_process != null)
					_process.Dispose();
				_process = null;
			}
			catch (Exception ex)
			{
				Logger.Log(string.Format("Could not start server process for {0} - exception: {1} {2}", _address, ex.GetType(), ex.Message));
				if (_process != null)
					_process.Dispose();
				_process = null;
			}
		}

		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (_process != null)
				{
					if (!_process.HasExited)
					{
						try
						{
							Logger.Log(string.Format("(Server.Dispose) Killing process {0}", _process.Id));
							_process.Kill();
						}
						catch (InvalidOperationException) { }
					}

					_process.Dispose();
					_process = null;
				}
				Client.Dispose();
			}
		}

		protected static async Task<T> Up<T>(T server)
			where T : ServerBase, new()
		{
			AsyncLock mutex = new AsyncLock();

			if (await HeartbeatCheck(server))
				return server;

			int tries = 0;
			while (true)
			{
				using (await mutex.LockAsync())
				{
					if (server != null && server._process != null && server._process.HasExited)
					{
						Logger.Log(string.Format("Server process exited and needs to be restarted ({0}).", server._address));
						server.Dispose();
						server = null;
					}
					if (server == null || (!server._outsideProcess && (server._process == null || server._process.HasExited)))
					{
						Logger.Log("Starting server process...");
						server = new T();
						server.StartServer();

					}
				}

				using (Task task = Task.Delay(200))
				{
					Logger.Log(string.Format("Looking for resource @ {0}", server._address));
					await task.ConfigureAwait(false);

					if (await HeartbeatCheck(server))
						break;
					else
					{
						if (!server._outsideProcess && (server._process == null || server._process.HasExited))
						{
							Logger.Log("Unable to start resource, aborting");
							server.Dispose();
							return null;
						}

						tries++;
						if (tries > 5)
						{
							Logger.Log("Unable to find resource, aborting");
							if (!server._outsideProcess && server._process != null && !server._process.HasExited)
							{
								Logger.Log("Killing server process...");
								server._process.Kill();
							}

							return null;
						}
					}
				}
			}

			return server;
		}

		protected static void Down<T>(T server)
			where T : ServerBase, new()
		{
			if (server != null)
				server.Dispose();
		}

		private static async Task<bool> HeartbeatCheck<T>(T server)
			where T : ServerBase, new()
		{
			if (server == null) return false;
			try
			{
				HttpResponseMessage response = await server.CallWebServer(server._address + server.HeartbeatCheckPath);
				if (response.StatusCode == System.Net.HttpStatusCode.OK)
					return true;
				return false;
			}
			catch { return false; }
		}

		protected async Task<CompilerResult> CallService(string path, bool reattempt)
		{
			string newPath = string.Format("{0}?{1}", _address, path);
			HttpResponseMessage response;
			try
			{
				response = await CallWebServer(newPath);

				// Retry once.
				if (!response.IsSuccessStatusCode && !reattempt)
					return await RetryOnce(path);

				var responseData = await response.Content.ReadAsAsync<NodeServerUtilities.Response>();

				return await responseData.GetCompilerResult();
			}
			catch
			{
				Logger.Log("Something went wrong reaching: " + Uri.EscapeUriString(newPath));
			}

			// Retry once.
			if (!reattempt)
				return await RetryOnce(path);

			return null;
		}

		private async Task<CompilerResult> RetryOnce(string path)
		{
			return await NodeServer.CallServiceAsync(path, true);
		}

		// Making this a separate method so it can throw to caller
		// which is a test criterion for HearbeatCheck.
		private async Task<HttpResponseMessage> CallWebServer(string path)
		{
			return await Client.GetAsync(path).ConfigureAwait(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
	}
}
