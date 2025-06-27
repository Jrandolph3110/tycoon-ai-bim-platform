using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using TycoonRevitAddin.Communication;

namespace TycoonRevitAddin.UI
{
    /// <summary>
    /// Real-time connection progress dialog
    /// </summary>
    public partial class ConnectionProgressDialog : Window
    {
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isComplete = false;

        public bool WasSuccessful { get; private set; } = false;
        public string ErrorMessage { get; private set; }

        public ConnectionProgressDialog()
        {
            InitializeComponent();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Start the connection process with progress updates
        /// </summary>
        public async Task<bool> ConnectAsync(TycoonBridge bridge)
        {
            try
            {
                // Show initial progress
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus("🔍 Discovering MCP server...", ConnectionStatus.Connecting);
                    UpdateProgress(10);
                });
                await Task.Delay(500);

                // Attempt connection using existing method
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus("🔗 Establishing connection...", ConnectionStatus.Connecting);
                    UpdateProgress(50);
                });

                var success = await bridge.ConnectAsync();

                WasSuccessful = success;
                _isComplete = true;

                // Update final status
                Dispatcher.Invoke(() =>
                {
                    if (success)
                    {
                        UpdateStatus("✅ Connected successfully!", ConnectionStatus.Connected);
                        UpdateProgress(100);
                    }
                    else
                    {
                        UpdateStatus("❌ Connection failed", ConnectionStatus.Error);
                        ErrorMessage = "Failed to establish connection to MCP server";
                    }

                    // Show close button
                    CancelButton.Visibility = Visibility.Collapsed;
                    CloseButton.Visibility = Visibility.Visible;
                });

                if (success)
                {
                    await Task.Delay(1000); // Show success for a moment
                    Dispatcher.Invoke(() => { DialogResult = true; Close(); });
                }

                return success;
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus("⚠️ Connection cancelled by user", ConnectionStatus.Disconnected);
                    CancelButton.Visibility = Visibility.Collapsed;
                    CloseButton.Visibility = Visibility.Visible;
                });
                return false;
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus($"❌ Connection error: {ex.Message}", ConnectionStatus.Error);
                    ErrorMessage = ex.Message;

                    CancelButton.Visibility = Visibility.Collapsed;
                    CloseButton.Visibility = Visibility.Visible;
                });

                return false;
            }
            finally
            {
                _isComplete = true;
            }
        }

        // Event handlers removed for now - will be added when TycoonBridge supports them

        private void AddProgressStep(string message, ConnectionStatus status)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            
            var icon = new Ellipse
            {
                Width = 12,
                Height = 12,
                Margin = new Thickness(0, 0, 8, 0),
                Fill = GetStatusBrush(status)
            };

            var text = new TextBlock
            {
                Text = $"{DateTime.Now:HH:mm:ss} - {message}",
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33))
            };

            panel.Children.Add(icon);
            panel.Children.Add(text);
            ProgressSteps.Children.Add(panel);

            // Auto-scroll to bottom
            if (ProgressSteps.Parent is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToBottom();
            }
        }

        private void UpdateStatus(string message, ConnectionStatus status)
        {
            StatusText.Text = message;
            StatusIcon.Fill = GetStatusBrush(status);

            // Add animation for active status
            if (status == ConnectionStatus.Active)
            {
                StartFlashingAnimation();
            }
            else
            {
                StopFlashingAnimation();
            }
        }

        private void UpdateProgress(int percentage)
        {
            ProgressBar.Value = Math.Max(0, Math.Min(100, percentage));
        }

        private Brush GetStatusBrush(ConnectionStatus status)
        {
            return status switch
            {
                ConnectionStatus.Disconnected => new SolidColorBrush(Colors.Red),
                ConnectionStatus.Available => new SolidColorBrush(Colors.Orange),
                ConnectionStatus.Connecting => new SolidColorBrush(Colors.Blue),
                ConnectionStatus.Connected => new SolidColorBrush(Colors.Green),
                ConnectionStatus.Active => new SolidColorBrush(Colors.LimeGreen),
                ConnectionStatus.Error => new SolidColorBrush(Colors.Red),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }

        private void StartFlashingAnimation()
        {
            var animation = new ColorAnimation
            {
                From = Colors.Green,
                To = Colors.Blue,
                Duration = TimeSpan.FromMilliseconds(500),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            var brush = new SolidColorBrush();
            StatusIcon.Fill = brush;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        private void StopFlashingAnimation()
        {
            StatusIcon.Fill.BeginAnimation(SolidColorBrush.ColorProperty, null);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = WasSuccessful;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            base.OnClosed(e);
        }
    }
}
