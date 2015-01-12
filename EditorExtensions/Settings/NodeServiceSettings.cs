using System.ComponentModel;
using ConfOxide;

namespace MadsKristensen.EditorExtensions.Settings
{
    class NodeServiceOptions : SettingsOptionPage<NodeServiceSettings>
    {
        public NodeServiceOptions() : base(s => s.NodeService) { }
    }
    public sealed class NodeServiceSettings : SettingsBase<NodeServiceSettings>
    {
        #region External Node Service
        [Category("External Node Service")]
        [DisplayName("Enable external node.js service")]
        [Description("If enabled, will assume the node.js service is already started. You must specify the port number!")]
        [DefaultValue(false)]
        public bool UseExternalNodeService { get; set; }

        [Category("External Node Service")]
        [DisplayName("Node service port number")]
        [Description("Node service port number")]
        [DefaultValue(0)]
        public int ExternalNodeServicePort { get; set; }
        #endregion

        #region Advanced Node Options
        [Category("Advanced Node Service Options")]
        [DisplayName("Path to WebEssentials node service")]
        [Description("Specify the path to WebEssentials' node services folder")]
        [DefaultValue("C:\\lib\\we-node")]
        public string NodeServicesPath { get; set; }

        [Category("Advanced Node Service Options")]
        [DisplayName("Extra service args")]
        [Description("Specify extra args to be passed to the node services script")]
        [DefaultValue(null)]
        public string ExtraNodeServiceArgs { get; set; }

        [Category("Advanced Node Service Options")]
        [DisplayName("Path to node.exe")]
        [Description("Specify the path to the node.js executable. By default will look in the WebEssentials node service folder specified above.")]
        [DefaultValue(null)]
        public string NodeExePath { get; set; }

        [Category("Advanced Node Service Options")]
        [DisplayName("Show node.js console window")]
        [Description("Shows the console window for debugging purposes.")]
        [DefaultValue(false)]
        public bool ShowConsoleWindow { get; set; }
        #endregion
    }
}