using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TycoonRevitAddin.Services;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.UI
{
    /// <summary>
    /// 🚀 First Run Wizard for GitHub Repository Setup
    /// Guides users through initial configuration and script download
    /// </summary>
    public partial class FirstRunWizard : Window
    {
        private readonly GitCacheManager _gitCacheManager;
        private readonly Logger _logger;
        private bool _downloadCompleted = false;

        public FirstRunWizard(GitCacheManager gitCacheManager, Logger logger)
        {
            InitializeComponent();
            _gitCacheManager = gitCacheManager ?? throw new ArgumentNullException(nameof(gitCacheManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Load existing settings if available
            LoadExistingSettings();
        }

        /// <summary>
        /// Load existing GitHub settings from configuration (Legacy - now using hardcoded values)
        /// </summary>
        private void LoadExistingSettings()
        {
            // Settings are now hardcoded - no need to load from configuration
            _logger.Log("Using hardcoded GitHub repository configuration - no setup required");
        }



        /// <summary>
        /// Download scripts from GitHub repository
        /// </summary>
        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            DownloadButton.IsEnabled = false;
            SkipButton.IsEnabled = false;
            DownloadProgressBar.Visibility = Visibility.Visible;
            LogScrollViewer.Visibility = Visibility.Visible;

            try
            {
                // Update GitCacheManager settings
                await UpdateGitCacheSettings();

                // Save settings for future use
                await SaveSettings();

                // Start download with progress tracking
                var progress = new Progress<string>(message =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        LogTextBlock.Text += $"{DateTime.Now:HH:mm:ss} - {message}\n";
                        LogScrollViewer.ScrollToEnd();
                    });
                });

                ProgressStatusTextBlock.Text = "📥 Downloading scripts from GitHub...";

                var success = await _gitCacheManager.RefreshCacheAsync(forceRefresh: true, progress: progress);

                if (success)
                {
                    ProgressStatusTextBlock.Text = "✅ Scripts downloaded successfully!";
                    ProgressStatusTextBlock.Foreground = System.Windows.Media.Brushes.Green;
                    DownloadProgressBar.Value = 100;
                    _downloadCompleted = true;
                    FinishButton.IsEnabled = true;

                    // Log success
                    await LogProgress("✅ First-run setup completed successfully!");
                }
                else
                {
                    ProgressStatusTextBlock.Text = "❌ Download failed. You can try again later.";
                    ProgressStatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                    await LogProgress("❌ Download failed - check connection and try again");
                }
            }
            catch (Exception ex)
            {
                ProgressStatusTextBlock.Text = $"❌ Error: {ex.Message}";
                ProgressStatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                _logger.LogError($"Download failed: {ex.Message}");
                await LogProgress($"❌ Error: {ex.Message}");
            }
            finally
            {
                DownloadButton.IsEnabled = true;
                SkipButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Skip setup and continue with offline mode
        /// </summary>
        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to skip the GitHub setup?\n\n" +
                "You can configure this later through the Settings menu, but you won't have access to the latest scripts until then.",
                "Skip GitHub Setup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                DialogResult = false;
                Close();
            }
        }

        /// <summary>
        /// Finish setup and close wizard
        /// </summary>
        private void Finish_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Update GitCacheManager with current form settings (Legacy - now using hardcoded values)
        /// </summary>
        private async Task UpdateGitCacheSettings()
        {
            // Configuration is now hardcoded - this method is kept for backward compatibility
            _logger.Log("Using hardcoded GitHub repository configuration");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Save GitHub settings to configuration file (Legacy - now using hardcoded values)
        /// </summary>
        private async Task SaveSettings()
        {
            // Settings are now hardcoded - no need to save configuration
            _logger.Log("Using hardcoded GitHub repository configuration - no settings to save");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Get selected update frequency as string (Legacy - now using hardcoded values)
        /// </summary>
        private string GetSelectedUpdateFrequency()
        {
            // Return default frequency since UI controls are removed
            return "daily";
        }

        /// <summary>
        /// Add progress message to log
        /// </summary>
        private async Task LogProgress(string message)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                LogTextBlock.Text += $"{DateTime.Now:HH:mm:ss} - {message}\n";
                LogScrollViewer.ScrollToEnd();
            });
        }
    }

    /// <summary>
    /// GitHub settings data model
    /// </summary>
    public class GitHubSettings
    {
        public string RepositoryUrl { get; set; }
        public string Branch { get; set; }
        public string UpdateFrequency { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
