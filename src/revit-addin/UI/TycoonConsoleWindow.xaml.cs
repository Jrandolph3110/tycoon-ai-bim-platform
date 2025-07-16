using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace TycoonRevitAddin.UI
{
    /// <summary>
    /// PyRevit-style console window for Tycoon AI-BIM Platform
    /// Provides real-time script execution monitoring and debugging capabilities
    /// </summary>
    public partial class TycoonConsoleWindow : Window
    {
        private readonly DispatcherTimer _updateTimer;
        private bool _isPaused = false;
        private int _lineCount = 0;
        private readonly object _lockObject = new object();
        private LogSource _currentLogSource = LogSource.ScriptOutputs;

        public TycoonConsoleWindow()
        {
            InitializeComponent();
            InitializeConsole();
            SetupKeyboardShortcuts();
            
            // Setup auto-update timer
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500) // Update every 500ms
            };
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
        }

        private void InitializeConsole()
        {
            // Set initial state
            StatusText.Text = "Console initialized";
            LastUpdateText.Text = DateTime.Now.ToString("HH:mm:ss");

            // Wire up events
            TopMostButton.Checked += (s, e) => Topmost = true;
            TopMostButton.Unchecked += (s, e) => Topmost = false;

            FilterComboBox.SelectionChanged += FilterComboBox_SelectionChanged;
            LogSourceComboBox.SelectionChanged += LogSourceComboBox_SelectionChanged;

            // Set window properties
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;

            // Add initial welcome content programmatically to avoid character-by-character display
            AddInitialWelcomeContent();
        }

        private void AddInitialWelcomeContent()
        {
            // Clear any existing content
            ConsoleParagraph.Inlines.Clear();

            // Add welcome message
            var welcomeRun = new Run("🔥 Tycoon Script Console Ready")
            {
                Foreground = new SolidColorBrush(Color.FromRgb(68, 170, 68)),
                FontWeight = FontWeights.Bold
            };
            ConsoleParagraph.Inlines.Add(welcomeRun);
            ConsoleParagraph.Inlines.Add(new LineBreak());

            // Add tip message
            var tipRun = new Run("💡 Tip: Use Shift+Click on script buttons to show console output")
            {
                Foreground = new SolidColorBrush(Color.FromRgb(68, 136, 204))
            };
            ConsoleParagraph.Inlines.Add(tipRun);
            ConsoleParagraph.Inlines.Add(new LineBreak());

            // Add shortcuts message
            var shortcutsRun = new Run("⌨️ Shortcuts: Ctrl+L (Clear), Ctrl+C (Copy), Ctrl+S (Save)")
            {
                Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136))
            };
            ConsoleParagraph.Inlines.Add(shortcutsRun);
            ConsoleParagraph.Inlines.Add(new LineBreak());
            ConsoleParagraph.Inlines.Add(new LineBreak());

            _lineCount = 3;
            LineCountText.Text = _lineCount.ToString();
        }

        private void SetupKeyboardShortcuts()
        {
            // Ctrl+L - Clear
            var clearGesture = new KeyGesture(Key.L, ModifierKeys.Control);
            var clearCommand = new RoutedCommand();
            CommandBindings.Add(new CommandBinding(clearCommand, (s, e) => ClearConsole()));
            InputBindings.Add(new InputBinding(clearCommand, clearGesture));

            // Ctrl+C - Copy
            var copyGesture = new KeyGesture(Key.C, ModifierKeys.Control);
            var copyCommand = new RoutedCommand();
            CommandBindings.Add(new CommandBinding(copyCommand, (s, e) => CopyAllText()));
            InputBindings.Add(new InputBinding(copyCommand, copyGesture));

            // Ctrl+S - Save
            var saveGesture = new KeyGesture(Key.S, ModifierKeys.Control);
            var saveCommand = new RoutedCommand();
            CommandBindings.Add(new CommandBinding(saveCommand, (s, e) => SaveToFile()));
            InputBindings.Add(new InputBinding(saveCommand, saveGesture));
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            if (_isPaused) return;

            try
            {
                // This will be enhanced to read from multiple log sources
                UpdateFromLogFiles();
            }
            catch (Exception ex)
            {
                // Silent fail - don't break the console for logging issues
                StatusText.Text = $"Update error: {ex.Message}";
            }
        }

        private void UpdateFromLogFiles()
        {
            // Placeholder for log file monitoring
            // This will be implemented in Phase 3
            StatusText.Text = "Monitoring logs...";
            LastUpdateText.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        public void AppendLogEntry(string message, LogLevel level = LogLevel.Info)
        {
            if (_isPaused) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                lock (_lockObject)
                {
                    var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    var paragraph = ConsoleParagraph;

                    // Add timestamp
                    var timestampRun = new Run($"[{timestamp}] ")
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                        FontSize = 10
                    };
                    paragraph.Inlines.Add(timestampRun);

                    // Add message with appropriate styling
                    var messageRun = new Run(message);
                    ApplyLogLevelStyling(messageRun, level);
                    paragraph.Inlines.Add(messageRun);
                    paragraph.Inlines.Add(new LineBreak());

                    _lineCount++;
                    LineCountText.Text = _lineCount.ToString();
                    LastUpdateText.Text = DateTime.Now.ToString("HH:mm:ss");

                    // Auto-scroll if enabled
                    if (AutoScrollButton.IsChecked == true)
                    {
                        ConsoleScrollViewer.ScrollToEnd();
                    }
                }
            }));
        }

        private void ApplyLogLevelStyling(Run run, LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Error:
                    run.Foreground = new SolidColorBrush(Color.FromRgb(255, 68, 68));
                    run.FontWeight = FontWeights.Bold;
                    break;
                case LogLevel.Success:
                    run.Foreground = new SolidColorBrush(Color.FromRgb(68, 170, 68));
                    run.FontWeight = FontWeights.SemiBold;
                    break;
                case LogLevel.Warning:
                    run.Foreground = new SolidColorBrush(Color.FromRgb(255, 170, 68));
                    run.FontWeight = FontWeights.SemiBold;
                    break;
                case LogLevel.Info:
                    run.Foreground = new SolidColorBrush(Color.FromRgb(68, 136, 204));
                    break;
                default:
                    run.Foreground = new SolidColorBrush(Colors.White);
                    break;
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e) => ClearConsole();
        private void CopyButton_Click(object sender, RoutedEventArgs e) => CopyAllText();
        private void SaveButton_Click(object sender, RoutedEventArgs e) => SaveToFile();

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear console and reload current source
            ClearConsole();
            StatusText.Text = $"Refreshing {GetLogSourceDisplayName(_currentLogSource)}...";
            LoadLogSourceContent(_currentLogSource);
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = !_isPaused;
            PauseButton.Content = _isPaused ? "▶️ Resume" : "⏸️ Pause";
            StatusText.Text = _isPaused ? "Paused" : "Monitoring logs...";
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Filter implementation will be added in Phase 3
            var selectedFilter = ((ComboBoxItem)FilterComboBox.SelectedItem)?.Content?.ToString();
            StatusText.Text = $"Filter: {selectedFilter}";
        }

        private void LogSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LogSourceComboBox.SelectedIndex < 0) return;

            var newSource = (LogSource)LogSourceComboBox.SelectedIndex;
            if (newSource == _currentLogSource) return;

            _currentLogSource = newSource;

            // Clear console and load new source
            ClearConsole();

            // Update status
            StatusText.Text = $"Switched to: {GetLogSourceDisplayName(newSource)}";

            // Load content for new source
            LoadLogSourceContent(newSource);
        }

        private void ClearConsole()
        {
            lock (_lockObject)
            {
                ConsoleParagraph.Inlines.Clear();
                _lineCount = 0;
                LineCountText.Text = "0";
                
                // Add welcome message back
                var welcomeRun = new Run("🔥 Console cleared - Ready for new output")
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(68, 170, 68)),
                    FontWeight = FontWeights.Bold
                };
                ConsoleParagraph.Inlines.Add(welcomeRun);
                ConsoleParagraph.Inlines.Add(new LineBreak());
                ConsoleParagraph.Inlines.Add(new LineBreak());
                
                _lineCount = 1;
                LineCountText.Text = "1";
            }
        }

        private void CopyAllText()
        {
            try
            {
                var textRange = new TextRange(ConsoleDocument.ContentStart, ConsoleDocument.ContentEnd);
                Clipboard.SetText(textRange.Text);
                StatusText.Text = "Console content copied to clipboard";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Copy failed: {ex.Message}";
            }
        }

        private void SaveToFile()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt|Log files (*.log)|*.log|All files (*.*)|*.*",
                    DefaultExt = "txt",
                    FileName = $"TycoonConsole_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var textRange = new TextRange(ConsoleDocument.ContentStart, ConsoleDocument.ContentEnd);
                    File.WriteAllText(saveDialog.FileName, textRange.Text);
                    StatusText.Text = $"Console saved to: {Path.GetFileName(saveDialog.FileName)}";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Save failed: {ex.Message}";
            }
        }

        private string GetLogSourceDisplayName(LogSource source)
        {
            return source switch
            {
                LogSource.TycoonLog => "Tycoon Log",
                LogSource.RevitJournal => "Revit Journal",
                LogSource.ScriptOutputs => "Script Outputs",
                _ => "Unknown"
            };
        }

        private void LoadLogSourceContent(LogSource source)
        {
            try
            {
                switch (source)
                {
                    case LogSource.TycoonLog:
                        LoadTycoonLogContent();
                        break;
                    case LogSource.RevitJournal:
                        LoadRevitJournalContent();
                        break;
                    case LogSource.ScriptOutputs:
                        LoadScriptOutputsContent();
                        break;
                }
            }
            catch (Exception ex)
            {
                AppendLogEntry($"❌ Error loading {GetLogSourceDisplayName(source)}: {ex.Message}", LogLevel.Error);
            }
        }

        private void LoadTycoonLogContent()
        {
            var logDirectory = Path.GetTempPath();
            var todayLogPattern = $"Tycoon_{DateTime.Now:yyyyMMdd}.log";
            var todayLogPath = Path.Combine(logDirectory, todayLogPattern);

            if (File.Exists(todayLogPath))
            {
                var content = File.ReadAllText(todayLogPath);
                var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                AppendLogEntry($"📁 Loading Tycoon log: {todayLogPath}", LogLevel.Info);

                // Load last 100 lines
                var startIndex = Math.Max(0, lines.Length - 100);
                for (int i = startIndex; i < lines.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(lines[i]))
                    {
                        AppendLogEntry(lines[i], LogLevel.Info);
                    }
                }
            }
            else
            {
                AppendLogEntry($"📁 No Tycoon log found for today: {todayLogPath}", LogLevel.Warning);
            }
        }

        private void LoadRevitJournalContent()
        {
            var journalDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Autodesk", "Revit", "Autodesk Revit 2024", "Journals");

            if (Directory.Exists(journalDirectory))
            {
                var journalFiles = Directory.GetFiles(journalDirectory, "journal.*.txt")
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .FirstOrDefault();

                if (journalFiles != null)
                {
                    try
                    {
                        // Use FileStream with proper sharing to handle locked files
                        string content;
                        using (var fileStream = new FileStream(journalFiles, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var reader = new StreamReader(fileStream))
                        {
                            content = reader.ReadToEnd();
                        }

                        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                        AppendLogEntry($"📁 Loading latest Revit journal: {Path.GetFileName(journalFiles)}", LogLevel.Info);

                        // Load last 50 lines
                        var startIndex = Math.Max(0, lines.Length - 50);
                        for (int i = startIndex; i < lines.Length; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(lines[i]))
                            {
                                AppendLogEntry(lines[i], LogLevel.Info);
                            }
                        }
                    }
                    catch (IOException ex)
                    {
                        AppendLogEntry($"📁 Cannot access Revit journal file (may be locked by Revit): {ex.Message}", LogLevel.Warning);
                        AppendLogEntry("💡 Try closing Revit or use the refresh button to retry", LogLevel.Info);
                    }
                    catch (Exception ex)
                    {
                        AppendLogEntry($"📁 Error reading Revit journal: {ex.Message}", LogLevel.Error);
                    }
                }
                else
                {
                    AppendLogEntry("📁 No Revit journal files found", LogLevel.Warning);
                }
            }
            else
            {
                AppendLogEntry($"📁 Revit journal directory not found: {journalDirectory}", LogLevel.Warning);
            }
        }

        private void LoadScriptOutputsContent()
        {
            AppendLogEntry("🔥 Script Outputs Console Ready", LogLevel.Success);
            AppendLogEntry("💡 This view shows real-time script execution output", LogLevel.Info);
            AppendLogEntry("⚡ Execute scripts to see output here", LogLevel.Info);

            // Load existing script outputs from today
            LoadExistingScriptOutputs();
        }

        private void LoadExistingScriptOutputs()
        {
            try
            {
                var scriptOutputDirectory = Path.Combine(Path.GetTempPath(), "TycoonScriptOutput");
                var todayLogPattern = $"ScriptOutput_{DateTime.Now:yyyyMMdd}.log";
                var todayLogPath = Path.Combine(scriptOutputDirectory, todayLogPattern);

                if (File.Exists(todayLogPath))
                {
                    using (var fileStream = new FileStream(todayLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(fileStream))
                    {
                        var content = reader.ReadToEnd();
                        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                        if (lines.Length > 0)
                        {
                            AppendLogEntry($"📜 Loading existing script outputs ({lines.Length} entries)...", LogLevel.Info);

                            // Load last 30 lines to avoid overwhelming
                            var startIndex = Math.Max(0, lines.Length - 30);
                            for (int i = startIndex; i < lines.Length; i++)
                            {
                                if (!string.IsNullOrWhiteSpace(lines[i]))
                                {
                                    AppendLogEntry(lines[i], LogLevel.Info);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLogEntry($"❌ Error loading existing script outputs: {ex.Message}", LogLevel.Warning);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _updateTimer?.Stop();
            base.OnClosed(e);
        }
    }

    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    public enum LogSource
    {
        TycoonLog = 0,
        RevitJournal = 1,
        ScriptOutputs = 2
    }
}
