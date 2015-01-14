using System;
using System.IO;
using System.Threading.Tasks;
using MadsKristensen.EditorExtensions.Settings;

namespace MadsKristensen.EditorExtensions.Compilers
{
    public sealed class NodeServer : ServerBase
    {
        const string DefaultNodeServicePath = @"C:\lib\WebEssentials\node-server";
        const string DefaultNodeExe = @"node.exe";
        const string DefaultNodeArgs = @"we-nodejs-server.js --port {0} --anti-forgery-token {1} --environment production --process-id {2}";

        private static NodeServer _server;

        public static async Task Up()
        {
            _server = await ServerBase.Up(_server);
        }

        public static void Down()
        {
            ServerBase.Down(_server);
        }

        public static async Task<CompilerResult> CallServiceAsync(string path, bool reattempt = false)
        {
            await Up();
            if (_server != null)
                return await _server.CallService(path, reattempt);
            return CompilerResult.GenerateResult(path, "", "", false, "Unable to start node", "", null);
        }

        string _nodePathUsed;

        public bool UseExternalNodeService { get; set; }
        public int ExternalNodeServicePort { get; set; }
        public string NodeServicesPath { get; set; }
        public string NodeServiceArgs { get; set; }
        public string NodeExePath { get; set; }
        public bool ShowConsoleWindow { get; set; }

        public NodeServer()
        {
            UseExternalNodeService = WESettings.Instance.NodeService.UseExternalNodeService;
            ExternalNodeServicePort = WESettings.Instance.NodeService.ExternalNodeServicePort;
            NodeServicesPath = WESettings.Instance.NodeService.NodeServicesPath;
            if (string.IsNullOrEmpty(NodeServicesPath))
                NodeServicesPath = DefaultNodeServicePath;
            NodeServiceArgs = WESettings.Instance.NodeService.ExtraNodeServiceArgs;
            if (string.IsNullOrEmpty(NodeServiceArgs))
                NodeServiceArgs = DefaultNodeArgs;
            NodeExePath = WESettings.Instance.NodeService.NodeExePath;
            if (string.IsNullOrEmpty(NodeExePath))
                NodeExePath = DefaultNodeExe;
            ShowConsoleWindow = WESettings.Instance.NodeService.ShowConsoleWindow;
        }

        bool IsExternalNodeServiceOptionsChanged()
        {
            return ExternalNodeServicePort != WESettings.Instance.NodeService.ExternalNodeServicePort;
        }

        bool IsInternalNodeServiceOptionsChanged()
        {
            return !string.Equals(NodeServicesPath, WESettings.Instance.NodeService.NodeServicesPath, StringComparison.Ordinal) || !string.Equals(NodeServiceArgs, WESettings.Instance.NodeService.ExtraNodeServiceArgs, StringComparison.Ordinal) || !string.Equals(NodeExePath, WESettings.Instance.NodeService.NodeExePath, StringComparison.Ordinal) || ShowConsoleWindow != WESettings.Instance.NodeService.ShowConsoleWindow;
        }

        protected override bool IsNeedsRestart()
        {
            // did the config change?
            if (UseExternalNodeService != WESettings.Instance.NodeService.UseExternalNodeService || (UseExternalNodeService && IsExternalNodeServiceOptionsChanged()) || (!UseExternalNodeService && IsInternalNodeServiceOptionsChanged()))
                return true;

            return base.IsNeedsRestart();
        }

        bool VerifyNodeInstalled()
        {
            bool reportAlternate = false;
            string nodePath = NodeExePath;
            if (!string.IsNullOrEmpty(nodePath))
            {
                // if a full path was set by the user, try that
                if (Path.IsPathRooted(nodePath))
                {
                    if (VerifyNodeInstalled(nodePath))
                        return true;
                }
                else
                {
                    // for relative paths, try the web services directory
                    // and then try windows environment paths
                    string searchPath = Path.Combine(NodeServicesPath, nodePath);
                    if (VerifyNodeInstalled(searchPath))
                        return true;

                    searchPath = FileHelpers.SearchEnvironmentPath(nodePath);
                    if (!string.IsNullOrEmpty(searchPath) && VerifyNodeInstalled(searchPath))
                        return true;
                }
                Logger.Log(string.Format("Cannot find node.js using '{0}'.", nodePath));
                reportAlternate = true;
            }
            // if path wasn't set and/or it couldn't be found, then try the default node.js install locations
            string appFolder = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(appFolder))
            {
                string path = Path.Combine(appFolder, DefaultNodeExe);
                if (VerifyNodeInstalled(path))
                {
                    if (reportAlternate)
                        Logger.Log(string.Format("Using node.js at {0}. You should update your WebEssentials configuration.", path));
                    return true;
                }
            }
            appFolder = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrEmpty(appFolder))
            {
                string path = Path.Combine(appFolder, DefaultNodeExe);
                if (VerifyNodeInstalled(path))
                {
                    if (reportAlternate)
                        Logger.Log(string.Format("Using node.js at {0}. You should update your WebEssentials configuration.", path));
                    return true;
                }
            }
            if (!reportAlternate)
                Logger.Log("Cannot find node.js, is it installed? You can specify the path in the options for WebEssentials.");
            return false;
        }

        bool VerifyNodeInstalled(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                _nodePathUsed = path;
                return true;
            }
            return false;
        }

        protected override void StartServer()
        {
            ShowWindow = ShowConsoleWindow;

            if (UseExternalNodeService)
            {
                BasePort = ExternalNodeServicePort;
                if (ExternalNodeServicePort > 0)
                    InitializeExternalProcess();
                else
                    Logger.Log("WebEssentials is configured to use an external node service, but the port number has not been set. Update your WebEssentials options to fix this error.");
            }
            else
            {
                if (!VerifyNodeInstalled())
                    return;

//                string wd = NodeServicesPath;
                // TODO: still setup a local install relative to the extension directory by default, so that separate instances of the extension can have their own versions (separate VS installs, or the experiemental instance, etc.)
//                if (!Path.IsPathRooted(wd))
//                    wd = Path.Combine(Path.GetDirectoryName(typeof(NodeServer).Assembly.Location), wd);

                Initialize(NodeServiceArgs, _nodePathUsed);
            }

            Client.DefaultRequestHeaders.Add("origin", "web essentials");
            Client.DefaultRequestHeaders.Add("user-agent", "web essentials");
            Client.DefaultRequestHeaders.Add("web-essentials", "web essentials");
            Client.DefaultRequestHeaders.Add("auth", BaseAuthenticationToken);
        }
    }
}
