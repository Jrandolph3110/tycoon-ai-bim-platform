using System;
using System.IO;
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
            
            // Set window properties
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
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
}
