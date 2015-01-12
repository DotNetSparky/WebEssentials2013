using System;

namespace MadsKristensen.NodeServiceInstaller
{
	public class ExecResult
	{
		public int ExitCode { get; set; }
		public string StdOut { get; set; }
		public string StdError { get; set; }

		public ExecResult(int exitCode, string stdOut, string stdError)
		{
			ExitCode = exitCode;
			StdOut = stdOut;
			StdError = stdError;
		}
	}
}