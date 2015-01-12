using System;

namespace MadsKristensen.NodeServiceInstaller
{
	class Program
	{
		static void Main(string[] args)
		{
			using (var installer = new NodeInstaller())
			{
				//installer.BaseDirectory = @"resources\nodejs";
				installer.CleanTarget = false;
				installer.InstallDirectory = @"C:\we-node";
				installer.PackageOutputDirectory = @"C:\we-node\package";

				if (installer.Execute())
					Console.WriteLine("Execution completed succcessfully.");
				else
					Console.WriteLine("Execution failed.");
			}

			Console.Write("Done -- press enter to exit: ");
			Console.ReadLine();
		}
	}
}
