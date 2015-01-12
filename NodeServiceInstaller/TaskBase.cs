using System;

namespace MadsKristensen.NodeServiceInstaller
{
	public abstract class TaskBase : IDisposable
	{
		readonly LogWrapper _log = new LogWrapper();

		public LogWrapper Log
		{
			get
			{
				return _log;
			}
		}

		public abstract bool Execute();

		public void Dispose()
		{
			Dispose(true);

			if (_log != null)
				_log.Dispose();
		}

		protected virtual void Dispose(bool disposing)
		{

		}
	}
}