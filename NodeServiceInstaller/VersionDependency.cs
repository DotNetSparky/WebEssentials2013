using System;

namespace MadsKristensen.NodeServiceInstaller
{
	public sealed class VersionDependency
	{
		public string Name { get; private set; }
		public string VersionRequired { get; private set; }

		public VersionDependency(string name, string versionRequired)
		{
			Name = name;
			VersionRequired = versionRequired;
		}

		public override string ToString()
		{
			return Name + ": " + VersionRequired;
		}
	}
}