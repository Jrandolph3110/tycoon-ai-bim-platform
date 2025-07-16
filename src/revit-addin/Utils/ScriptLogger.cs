using System;
using System.IO;
using TycoonRevitAddin.UI;

namespace TycoonRevitAddin.Utils
{
    /// <summary>
    /// Dedicated logger for script execution output, separate from platform logs
    /// Provides clean separation between script output and system logs
    /// </summary>
    public class ScriptLogger : ILogger
    {
        private readonly string _scriptLogFilePath;
        private readonly string _scriptName;
        private readonly bool _debugMode;

        public ScriptLogger(string scriptName, bool debugMode = false)
        {
            _scriptName = scriptName;
            _debugMode = debugMode;
            
            // Create dedicated script output directory
            var scriptOutputDirectory = Path.Combine(Path.GetTempPath(), "TycoonScriptOutput");
            if (!Directory.Exists(scriptOutputDirectory))
            {
                Directory.CreateDirectory(scriptOutputDirectory);
            }
            
            // Create script-specific log file
            string logFileName = $"ScriptOutput_{DateTime.Now:yyyyMMdd}.log";
            _scriptLogFilePath = Path.Combine(scriptOutputDirectory, logFileName);
        }

        /// <summary>
        /// Log an informational message from script execution
        /// </summary>
        public void Log(string message)
        {
            WriteScriptLog("INFO", message);
            
            // Also send to console if Script Outputs is selected
            TycoonConsoleManager.AppendScriptLog(message, LogLevel.Info);
        }

        /// <summary>
        /// Log a debug message (only if debug mode is enabled)
        /// </summary>
        public void LogDebug(string message)
        {
            if (_debugMode)
            {
                WriteScriptLog("DEBUG", message);
                TycoonConsoleManager.AppendScriptLog($"[DEBUG] {message}", LogLevel.Info);
            }
        }

        /// <summary>
        /// Log a warning message from script execution
        /// </summary>
        public void LogWarning(string message)
        {
            WriteScriptLog("WARNING", message);
            TycoonConsoleManager.AppendScriptLog(message, LogLevel.Warning);
        }

        /// <summary>
        /// Log an error message from script execution
        /// </summary>
        public void LogError(string message, Exception ex = null)
        {
            var fullMessage = ex != null ? $"{message}: {ex.Message}" : message;
            WriteScriptLog("ERROR", fullMessage);
            TycoonConsoleManager.AppendScriptLog(fullMessage, LogLevel.Error);
        }

        /// <summary>
        /// Log a success message from script execution
        /// </summary>
        public void LogSuccess(string message)
        {
            WriteScriptLog("SUCCESS", message);
            TycoonConsoleManager.AppendScriptLog(message, LogLevel.Success);
        }

        /// <summary>
        /// Write log entry to script output file
        /// </summary>
        private void WriteScriptLog(string level, string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] [{level}] [{_scriptName}] {message}";

                // Write to dedicated script output file
                File.AppendAllText(_scriptLogFilePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Ignore logging errors to prevent cascading failures
            }
        }

        /// <summary>
        /// Get the current script log file path
        /// </summary>
        public string GetLogFilePath()
        {
            return _scriptLogFilePath;
        }
    }
}
