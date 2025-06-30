using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using TycoonRevitAddin.Services;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.UI
{
    /// <summary>
    /// ⚙️ GitHub Repository Settings Dialog
    /// Allows users to configure GitHub repository settings and manage cache
    /// </summary>
    public partial class GitHubSettingsDialog : Window
    {
        private readonly GitCacheManager _gitCacheManager;
        private readonly Logger _logger;

        public GitHubSettingsDialog(GitCacheManager gitCacheManager, Logger logger)
        {
            InitializeComponent();
            _gitCacheManager = gitCacheManager ?? throw new ArgumentNullException(nameof(gitCacheManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            LoadCurrentSettings();
            UpdateCacheStatus();
        }

        /// <summary>
        /// Load current GitHub settings into the form
        /// </summary>
        private void LoadCurrentSettings()
        {
            try
            {
                var settingsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Tycoon", "github-settings.json");

                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    var settings = Newtonsoft.Json.JsonConvert.DeserializeObject<GitHubSettings>(json);
                    
                    if (settings != null)
                    {
                        RepositoryUrlTextBox.Text = settings.RepositoryUrl ?? "";
                        BranchTextBox.Text = settings.Branch ?? "main";
                        
                        // Set update frequency
                        switch (settings.UpdateFrequency?.ToLower())
                        {
                            case "never": UpdateFrequencyComboBox.SelectedIndex = 0; break;
                            case "daily": UpdateFrequencyComboBox.SelectedIndex = 1; break;
                            case "weekly": UpdateFrequencyComboBox.SelectedIndex = 2; break;
                            case "monthly": UpdateFrequencyComboBox.SelectedIndex = 3; break;
                            default: UpdateFrequencyComboBox.SelectedIndex = 1; break;
                        }

                        // Show last updated time
                        if (settings.LastUpdated != default)
                        {
                            LastUpdatedTextBlock.Text = settings.LastUpdated.ToString("yyyy-MM-dd HH:mm:ss UTC");
                        }
                        else
                        {
                            LastUpdatedTextBlock.Text = "Never";
                        }
                    }
                }
                else
                {
                    // Default values for new setup
                    RepositoryUrlTextBox.Text = "https://github.com/your-username/tycoon-ai-bim-platform";
                    BranchTextBox.Text = "main";
                    UpdateFrequencyComboBox.SelectedIndex = 1; // Daily
                    LastUpdatedTextBlock.Text = "Never";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to load GitHub settings: {ex.Message}");
                LastUpdatedTextBlock.Text = "Error loading settings";
            }
        }

        /// <summary>
        /// Update cache status information
        /// </summary>
        private void UpdateCacheStatus()
        {
            try
            {
                var cachedScriptsPath = _gitCacheManager.GetCachedScriptsPath();
                
                if (!string.IsNullOrEmpty(cachedScriptsPath) && Directory.Exists(cachedScriptsPath))
                {
                    var scriptCount = Directory.GetFiles(cachedScriptsPath, "*.py", SearchOption.AllDirectories).Length;
                    CacheStatusTextBlock.Text = $"✅ {scriptCount} scripts cached locally";
                    CacheStatusTextBlock.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    CacheStatusTextBlock.Text = "❌ No cached scripts available";
                    CacheStatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                CacheStatusTextBlock.Text = $"❌ Error checking cache: {ex.Message}";
                CacheStatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                _logger.LogError($"Error updating cache status: {ex.Message}");
            }
        }

        /// <summary>
        /// Test GitHub connection with current settings
        /// </summary>
        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            TestConnectionButton.IsEnabled = false;
            ConnectionStatusTextBlock.Text = "🔄 Testing connection...";
            ConnectionStatusTextBlock.Foreground = System.Windows.Media.Brushes.Orange;

            try
            {
                // Update GitCacheManager settings temporarily for testing
                await UpdateGitCacheSettings();

                // Test connection
                var success = await _gitCacheManager.TestConnectionAsync();

                if (success)
                {
                    ConnectionStatusTextBlock.Text = "✅ Connection successful!";
                    ConnectionStatusTextBlock.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    ConnectionStatusTextBlock.Text = "❌ Connection failed. Check repository URL and credentials.";
                    ConnectionStatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                ConnectionStatusTextBlock.Text = $"❌ Error: {ex.Message}";
                ConnectionStatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                _logger.LogError($"Connection test failed: {ex.Message}");
            }
            finally
            {
                TestConnectionButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Refresh cache from GitHub
        /// </summary>
        private async void RefreshCache_Click(object sender, RoutedEventArgs e)
        {
            RefreshCacheButton.IsEnabled = false;
            ProgressBar.Visibility = Visibility.Visible;
            ProgressBar.IsIndeterminate = true;

            try
            {
                // Update GitCacheManager settings
                await UpdateGitCacheSettings();

                // Refresh cache
                var success = await _gitCacheManager.RefreshCacheAsync(forceRefresh: true);

                if (success)
                {
                    MessageBox.Show("✅ Cache refreshed successfully!", "Success", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateCacheStatus();
                }
                else
                {
                    MessageBox.Show("❌ Failed to refresh cache. Check your connection and settings.", "Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Error refreshing cache: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
                _logger.LogError($"Cache refresh failed: {ex.Message}");
            }
            finally
            {
                RefreshCacheButton.IsEnabled = true;
                ProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Clear local cache
        /// </summary>
        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear the local script cache?\n\n" +
                "This will remove all downloaded scripts and you'll need to download them again.",
                "Clear Cache",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Clear cache directory
                    var cacheBasePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Tycoon", "GitCache");

                    if (Directory.Exists(cacheBasePath))
                    {
                        Directory.Delete(cacheBasePath, recursive: true);
                        _logger.Log("🗑️ Cache cleared successfully");
                        
                        MessageBox.Show("✅ Cache cleared successfully!", "Success", 
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                        UpdateCacheStatus();
                    }
                    else
                    {
                        MessageBox.Show("ℹ️ No cache to clear.", "Information", 
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"❌ Error clearing cache: {ex.Message}", "Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                    _logger.LogError($"Cache clear failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Save settings and close dialog
        /// </summary>
        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(RepositoryUrlTextBox.Text))
                {
                    MessageBox.Show("Repository URL is required.", "Validation Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Update GitCacheManager settings
                await UpdateGitCacheSettings();

                // Save settings to file
                await SaveSettings();

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Error saving settings: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
                _logger.LogError($"Settings save failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Cancel and close dialog
        /// </summary>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Update GitCacheManager with current form settings
        /// </summary>
        private async Task UpdateGitCacheSettings()
        {
            var repositoryUrl = RepositoryUrlTextBox.Text?.Trim();
            var branch = BranchTextBox.Text?.Trim();
            var accessToken = AccessTokenPasswordBox.Password?.Trim();

            if (string.IsNullOrEmpty(repositoryUrl))
            {
                throw new ArgumentException("Repository URL is required");
            }

            if (string.IsNullOrEmpty(branch))
            {
                branch = "main";
            }

            await _gitCacheManager.UpdateConfigurationAsync(repositoryUrl, branch, accessToken);
        }

        /// <summary>
        /// Save GitHub settings to configuration file
        /// </summary>
        private async Task SaveSettings()
        {
            try
            {
                var settings = new GitHubSettings
                {
                    RepositoryUrl = RepositoryUrlTextBox.Text?.Trim(),
                    Branch = BranchTextBox.Text?.Trim() ?? "main",
                    UpdateFrequency = GetSelectedUpdateFrequency(),
                    LastUpdated = DateTime.UtcNow
                };

                var settingsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Tycoon");

                Directory.CreateDirectory(settingsDir);

                var settingsPath = Path.Combine(settingsDir, "github-settings.json");
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);
                
                File.WriteAllText(settingsPath, json);
                _logger.Log("GitHub settings saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to save settings: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get selected update frequency as string
        /// </summary>
        private string GetSelectedUpdateFrequency()
        {
            return UpdateFrequencyComboBox.SelectedIndex switch
            {
                0 => "never",
                1 => "daily",
                2 => "weekly",
                3 => "monthly",
                _ => "daily"
            };
        }
    }
}
