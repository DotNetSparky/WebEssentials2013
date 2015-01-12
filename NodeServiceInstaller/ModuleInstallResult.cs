using System;

namespace MadsKristensen.NodeServiceInstaller
{
	public enum InstallResult { AlreadyPresent, Installed, Error }

	public class ModuleInstallResult
	{
		public string Name { get; set; }
		public InstallResult Result { get; set; }

		public ModuleInstallResult(string name, InstallResult result)
		{
			Name = name;
			Result = result;
		}
	}
}