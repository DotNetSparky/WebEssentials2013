using System;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;

namespace MadsKristensen.NodeServiceInstaller
{
		public sealed class LogWrapper : MarshalByRefObject, IDisposable
		{
			readonly object _syncRoot = new object();
			volatile bool _writeFile;
			StreamWriter _file;
			string _logFile;
			bool _disposed;

			public string LogFile
			{
				get
				{
					return _logFile;
				}
				set
				{
					lock (_syncRoot)
					{
						_logFile = value;
						_writeFile = !string.IsNullOrEmpty(value);
					}
				}
			}

			void Write(string format, params object[] args)
			{
				Write(args != null ? string.Format(format, args) : format);
			}

			void Write(string s)
			{
				Console.WriteLine(s);
				if (!_disposed && _writeFile)
				{
					lock (_syncRoot)
					{
						if (_writeFile)
						{
							if (_file == null)
							{
								FileStream fs = new FileStream(LogFile, FileMode.Create, FileAccess.Write, FileShare.Read);
								_file = new StreamWriter(fs, Encoding.UTF8);
							}
							_file.WriteLine(s);
						}
					}
				}
			}

			public void LogMessage(string message, params object[] messageArgs)
			{
				LogMessage(MessageImportance.Normal, message, messageArgs);
			}

			public void LogMessage(MessageImportance importance, string message, params object[] messageArgs)
			{
				if (message == null)
					throw new ArgumentNullException("message");

				Write(message, messageArgs);
			}

			public void LogMessage(MessageImportance importance, string message)
			{
				if (message == null)
					throw new ArgumentNullException("message");

				Write(message);
			}

			public void LogMessage(string message)
			{
				Write(message);
			}

			public void LogError(string message, params object[] messageArgs)
			{
				if (message == null)
					throw new ArgumentNullException("message");
			}

			public void LogError(string message)
			{
				Write(message);
			}

			public void LogWarning(string message, params object[] messageArgs)
			{
				if (message == null)
					throw new ArgumentNullException("message");

				Write(message, messageArgs);
			}

			public void LogWarning(string message)
			{
				Write(message);
			}

			public void LogWarningFromException(Exception exception)
			{
				LogWarningFromException(exception, false);
			}

			public void LogWarningFromException(Exception exception, bool showStackTrace)
			{
				if (exception == null)
					throw new ArgumentNullException("exception");

				string message = exception.Message;
				if (showStackTrace)
					message = message + Environment.NewLine + exception.StackTrace;
				LogWarning(message);
			}

			public void Dispose()
			{
				_disposed = true;
				if (_file != null)
				{
					_file.Dispose();
					_file = null;
				}
			}
		}
	}
