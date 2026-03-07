using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mirage.Logging
{
    public class StandaloneLogger : ILogger, ILogHandler
    {
        public ILogHandler logHandler { get; set; }
        public bool logEnabled { get; set; } = true;
        public LogType filterLogType { get; set; } = LogType.Log;

        public StandaloneLogger()
        {
            logHandler = this;
        }

        public bool IsLogTypeAllowed(LogType logType)
        {
            if (!logEnabled) return false;
            if (logType == LogType.Exception) return true;
            if (filterLogType == LogType.Exception) return false;
            return logType <= filterLogType;
        }

        #region ILogger Implementation

        public void Log(LogType logType, object message) => Log(logType, message, null);

        public void Log(LogType logType, object message, Object context)
        {
            if (IsLogTypeAllowed(logType))
                logHandler.LogFormat(logType, context, "{0}", message);
        }

        public void Log(LogType logType, string tag, object message) => Log(logType, tag, message, null);

        public void Log(LogType logType, string tag, object message, Object context)
        {
            if (IsLogTypeAllowed(logType))
                logHandler.LogFormat(logType, context, "[{0}] {1}", tag, message);
        }

        public void Log(object message) => Log(LogType.Log, message);

        public void Log(string tag, object message) => Log(LogType.Log, tag, message);

        public void Log(string tag, object message, Object context) => Log(LogType.Log, tag, message, context);

        public void LogWarning(string tag, object message) => Log(LogType.Warning, tag, message);

        public void LogWarning(string tag, object message, Object context) => Log(LogType.Warning, tag, message, context);

        public void LogError(string tag, object message) => Log(LogType.Error, tag, message);

        public void LogError(string tag, object message, Object context) => Log(LogType.Error, tag, message, context);

        public void LogException(Exception exception) => LogException(exception, null);

        void ILogger.LogFormat(LogType logType, string format, params object[] args)
        {
            logHandler.LogFormat(logType, null, format, args);
        }

        public void LogWarning(object message) => Log(LogType.Warning, message);
        public void LogError(object message) => Log(LogType.Error, message);

        #endregion

        #region ILogHandler Implementation

        private static readonly ConsoleColor[] logTypeToColor = {
            ConsoleColor.Red,    // Error
            ConsoleColor.Red,    // Assert
            ConsoleColor.Yellow, // Warning
            ConsoleColor.White,  // Log
            ConsoleColor.Cyan,   // Exception (Changed to Cyan to distinguish from Error)
        };

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            if (!IsLogTypeAllowed(logType)) return;

            Console.ForegroundColor = logTypeToColor[(int)logType];
            
            string message = (args != null && args.Length > 0) ? string.Format(format, args) : format;

            string contextPart = context != null ? $"({context.name}) " : "";

            Console.WriteLine($"{contextPart}{message}");
            Console.ResetColor();
        }

        public void LogException(Exception exception, Object context)
        {
            if (!logEnabled) return;

            Console.ForegroundColor = ConsoleColor.Red;
            string contextPart = context != null ? $" [Context: {context.name}]" : "";
            Console.WriteLine($"Exception: {exception.Message}{contextPart}");
            Console.WriteLine(exception.StackTrace);
            Console.ResetColor();
        }

        #endregion
    }
}
