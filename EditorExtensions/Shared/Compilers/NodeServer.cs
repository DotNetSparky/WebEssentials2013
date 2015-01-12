using System;
using System.IO;
using System.Threading.Tasks;
using MadsKristensen.EditorExtensions.Settings;

namespace MadsKristensen.EditorExtensions
{
    public sealed class NodeServer : ServerBase
    {
        //const string DefaultNodePath = @"nodejs\node.exe";
        const string DefaultNodeServicePath = @"C:\lib\we-node";
        const string DefaultNodeExePath = @"nodejs\node.exe";
        const string DefaultNodeArgs = @"tools\server\we-nodejs-server.js --port {0} --anti-forgery-token {1} --environment production --process-id {2}";
        //const string DefaultWorkingDirectory = @"Resources";

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
            return CompilerResult.GenerateResult(path, "", "", false, "Unable to start node", "", null, false);
        }

        public bool UseExternalNodeService { get { return WESettings.Instance.NodeService.UseExternalNodeService; } }
        public int ExternalNodeServicePort { get { return WESettings.Instance.NodeService.ExternalNodeServicePort; } }
        public string NodeServicesPath { get { return WESettings.Instance.NodeService.NodeServicesPath; } }
        public string ExtraNodeServiceArgs { get { return WESettings.Instance.NodeService.ExtraNodeServiceArgs; } }
        public string NodeExePath { get { return WESettings.Instance.NodeService.NodeExePath; } }
        public bool ShowConsoleWindow { get { return WESettings.Instance.NodeService.ShowConsoleWindow; } }

        protected override void StartServer()
        {
            // TODO: make it so changing settings will restart existing node server

            ShowWindow = ShowConsoleWindow;

            if (UseExternalNodeService)
            {
                BasePort = ExternalNodeServicePort;
                if (ExternalNodeServicePort > 0)
                    InitializeExternalProcess();
            }
            else
            {
                string wd = NodeServicesPath;
                if (string.IsNullOrEmpty(wd))
                    wd = DefaultNodeServicePath;
                string nodePath = NodeExePath;
                if (string.IsNullOrEmpty(nodePath))
                    nodePath = DefaultNodeExePath;
                string args = ExtraNodeServiceArgs;
                if (string.IsNullOrEmpty(args))
                    args = DefaultNodeArgs;
                if (!Path.IsPathRooted(wd))
                    wd = Path.Combine(Path.GetDirectoryName(typeof(NodeServer).Assembly.Location), wd);
                nodePath = Path.Combine(wd, nodePath);
                Initialize(args, nodePath);
            }

            Client.DefaultRequestHeaders.Add("origin", "web essentials");
            Client.DefaultRequestHeaders.Add("user-agent", "web essentials");
            Client.DefaultRequestHeaders.Add("web-essentials", "web essentials");
            Client.DefaultRequestHeaders.Add("auth", BaseAuthenticationToken);
        }
    }
}
