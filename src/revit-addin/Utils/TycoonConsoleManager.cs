using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using TycoonRevitAddin.UI;

namespace TycoonRevitAddin.Utils
{
    /// <summary>
    /// Manages the Tycoon Console window singleton and log streaming
    /// Provides PyRevit-style console experience for script development
    /// </summary>
    public static class TycoonConsoleManager
    {
        private static TycoonConsoleWindow _consoleWindow;
        private static readonly object _lockObject = new object();
        private static FileSystemWatcher _logWatcher;
        private static string _lastLogContent = string.Empty;

        /// <summary>
        /// Show or bring to front the console window
        /// </summary>
        public static void ShowConsole()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_consoleWindow == null)
                    {
                        _consoleWindow = new TycoonConsoleWindow();
                        _consoleWindow.Closed += (s, e) => _consoleWindow = null;

                        // Start monitoring log files
                        StartLogMonitoring();
                    }

                    if (_consoleWindow.WindowState == WindowState.Minimized)
                    {
                        _consoleWindow.WindowState = WindowState.Normal;
                    }

                    _consoleWindow.Show();
                    _consoleWindow.Activate();
                    _consoleWindow.Focus();
                }
            }
            catch (Exception ex)
            {
                // Fallback to simple message if console fails
                System.Windows.MessageBox.Show($"Console initialization failed: {ex.Message}\n\nCheck log files manually at: %APPDATA%\\Tycoon\\",
                    "Console Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Hide the console window
        /// </summary>
        public static void HideConsole()
        {
            lock (_lockObject)
            {
                _consoleWindow?.Hide();
            }
        }

        /// <summary>
        /// Check if console is currently visible
        /// </summary>
        public static bool IsConsoleVisible()
        {
            lock (_lockObject)
            {
                return _consoleWindow?.IsVisible == true;
            }
        }

        /// <summary>
        /// Append a log entry to the console
        /// </summary>
        public static void AppendLog(string message, LogLevel level = LogLevel.Info)
        {
            lock (_lockObject)
            {
                _consoleWindow?.AppendLogEntry(message, level);
            }
        }

        /// <summary>
        /// Start monitoring log files for real-time updates
        /// </summary>
        private static void StartLogMonitoring()
        {
            try
            {
                var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tycoon");
                
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                // Monitor the Tycoon directory for log file changes
                _logWatcher = new FileSystemWatcher(logDirectory)
                {
                    Filter = "*.log",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                _logWatcher.Changed += LogWatcher_Changed;
                
                // Load existing log content
                LoadExistingLogs();
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Failed to start log monitoring: {ex.Message}", LogLevel.Error);
            }
        }

        private static void LogWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            // Debounce rapid file changes
            Task.Delay(100).ContinueWith(_ => ProcessLogFileChange(e.FullPath));
        }

        private static void ProcessLogFileChange(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return;

                // Read new content from the log file
                var currentContent = File.ReadAllText(filePath);
                
                if (currentContent.Length > _lastLogContent.Length)
                {
                    var newContent = currentContent.Substring(_lastLogContent.Length);
                    var lines = newContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            var level = DetermineLogLevel(line);
                            AppendLog(line, level);
                        }
                    }

                    _lastLogContent = currentContent;
                }
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Error processing log file {Path.GetFileName(filePath)}: {ex.Message}", LogLevel.Error);
            }
        }

        private static void LoadExistingLogs()
        {
            try
            {
                var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tycoon");
                var reNumberLogPath = Path.Combine(logDirectory, "ReNumber_Debug.log");

                if (File.Exists(reNumberLogPath))
                {
                    var content = File.ReadAllText(reNumberLogPath);
                    var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    // Load last 50 lines to avoid overwhelming the console
                    var startIndex = Math.Max(0, lines.Length - 50);
                    
                    if (startIndex > 0)
                    {
                        AppendLog($"📜 Showing last {lines.Length - startIndex} lines from existing log...", LogLevel.Info);
                    }

                    for (int i = startIndex; i < lines.Length; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(lines[i]))
                        {
                            var level = DetermineLogLevel(lines[i]);
                            AppendLog(lines[i], level);
                        }
                    }

                    _lastLogContent = content;
                }
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Error loading existing logs: {ex.Message}", LogLevel.Error);
            }
        }

        private static LogLevel DetermineLogLevel(string logLine)
        {
            if (logLine.Contains("❌") || logLine.Contains("ERROR") || logLine.Contains("Failed"))
                return LogLevel.Error;
            
            if (logLine.Contains("⚠️") || logLine.Contains("WARN") || logLine.Contains("Warning"))
                return LogLevel.Warning;
            
            if (logLine.Contains("✅") || logLine.Contains("SUCCESS") || logLine.Contains("completed") || 
                logLine.Contains("🔥") || logLine.Contains("💾"))
                return LogLevel.Success;
            
            return LogLevel.Info;
        }

        /// <summary>
        /// Cleanup resources when shutting down
        /// </summary>
        public static void Cleanup()
        {
            lock (_lockObject)
            {
                _logWatcher?.Dispose();
                _consoleWindow?.Close();
                _consoleWindow = null;
            }
        }
    }
}
