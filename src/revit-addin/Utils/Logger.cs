using System;
using System.IO;

namespace TycoonRevitAddin.Utils
{
    /// <summary>
    /// Simple logging utility for Tycoon Revit Add-in
    /// </summary>
    [Serializable]
    public class Logger : ILogger
    {
        private readonly string _logName;
        private readonly bool _debugMode;
        private readonly string _logFilePath;

        public Logger(string logName, bool debugMode = false)
        {
            _logName = logName;
            _debugMode = debugMode;
            
            // Create log file in temp directory
            string tempPath = Path.GetTempPath();
            string logFileName = $"{logName}_{DateTime.Now:yyyyMMdd}.log";
            _logFilePath = Path.Combine(tempPath, logFileName);
        }

        /// <summary>
        /// Log an informational message
        /// </summary>
        public void Log(string message)
        {
            WriteLog("INFO", message);
        }

        /// <summary>
        /// Log a debug message (only if debug mode is enabled)
        /// </summary>
        public void LogDebug(string message)
        {
            if (_debugMode)
            {
                WriteLog("DEBUG", message);
            }
        }

        /// <summary>
        /// Log a warning message
        /// </summary>
        public void LogWarning(string message)
        {
            WriteLog("WARN", message);
        }

        /// <summary>
        /// Log an error message
        /// </summary>
        public void LogError(string message, Exception exception = null)
        {
            string fullMessage = message;
            if (exception != null)
            {
                fullMessage += $" | Exception: {exception.Message}";
                if (_debugMode && exception.StackTrace != null)
                {
                    fullMessage += $" | StackTrace: {exception.StackTrace}";
                }
            }
            WriteLog("ERROR", fullMessage);
        }

        /// <summary>
        /// Write log entry to file and console
        /// </summary>
        private void WriteLog(string level, string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string logEntry = $"[{timestamp}] [{level}] [{_logName}] {message}";

                // Write to console (visible in Revit's debug output)
                Console.WriteLine(logEntry);

                // Write to file
                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Ignore logging errors to prevent cascading failures
            }
        }

        /// <summary>
        /// Get the current log file path
        /// </summary>
        public string GetLogFilePath()
        {
            return _logFilePath;
        }
    }
}
