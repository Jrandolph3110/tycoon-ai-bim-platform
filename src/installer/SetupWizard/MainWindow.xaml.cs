using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;

namespace TycoonSetupWizard
{
    public partial class MainWindow : Window
    {
        private string mcpServerPath;
        private bool existingInstallation;
        private string existingVersion;

        public MainWindow()
        {
            InitializeComponent();
            mcpServerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                                        "Tycoon", "mcp-server");
            CheckExistingInstallation();
        }

        private void CheckExistingInstallation()
        {
            existingInstallation = Directory.Exists(mcpServerPath);
            
            if (existingInstallation)
            {
                // Check for existing version
                string packageJsonPath = Path.Combine(mcpServerPath, "package.json");
                if (File.Exists(packageJsonPath))
                {
                    try
                    {
                        string json = File.ReadAllText(packageJsonPath);
                        var package = JsonConvert.DeserializeObject<dynamic>(json);
                        existingVersion = package?.version?.ToString() ?? "Unknown";
                    }
                    catch
                    {
                        existingVersion = "Unknown";
                    }
                }

                StatusText.Text = "✅ Existing MCP Server Found";
                StatusDetails.Text = $"Version: {existingVersion}\nLocation: {mcpServerPath}\n\n" +
                                   "Your existing installation and data will be preserved.";
                
                KeepExistingOption.IsChecked = true;
                DownloadLatestOption.IsChecked = false;
            }
            else
            {
                StatusText.Text = "📥 MCP Server Setup Required";
                StatusDetails.Text = "No existing MCP server found. Choose an option below to complete your setup.";
                
                KeepExistingOption.IsEnabled = false;
                KeepExistingOption.Content = "Keep Existing Installation (Not Available)";
            }
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadLatestOption.IsChecked == true)
            {
                await DownloadAndInstallMCP();
            }
            else if (KeepExistingOption.IsChecked == true)
            {
                CompleteSetup("Existing installation preserved successfully!");
            }
            else if (SkipSetupOption.IsChecked == true)
            {
                CompleteSetup("Setup skipped. Use 'Copy MCP Config' in Revit when ready to configure manually.");
            }
        }

        private async Task DownloadAndInstallMCP()
        {
            try
            {
                ShowProgress("Preparing MCP Server installation...");
                
                // Create backup if existing installation
                if (existingInstallation)
                {
                    UpdateProgress("Creating backup of existing installation...", 10);
                    await CreateBackup();
                }

                // Download latest MCP server
                UpdateProgress("Downloading latest MCP server from GitHub...", 30);
                string tempZipPath = await DownloadLatestMCP();

                // Install MCP server
                UpdateProgress("Installing MCP server...", 60);
                await InstallMCP(tempZipPath);

                // Install dependencies
                UpdateProgress("Installing Node.js dependencies...", 80);
                await InstallDependencies();

                UpdateProgress("Installation completed successfully!", 100);
                await Task.Delay(1000);

                CompleteSetup("MCP Server installed successfully! Your Tycoon AI-BIM Platform is ready to use.");
            }
            catch (Exception ex)
            {
                HideProgress();
                MessageBox.Show($"Installation failed: {ex.Message}\n\nYou can set up the MCP server manually later using the 'Copy MCP Config' button in Revit.", 
                               "Installation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                CompleteSetup("Installation failed. Manual setup will be required.");
            }
        }

        private async Task CreateBackup()
        {
            string backupPath = mcpServerPath + "_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            await Task.Run(() => CopyDirectory(mcpServerPath, backupPath));
        }

        private async Task<string> DownloadLatestMCP()
        {
            string tempZipPath = Path.Combine(Path.GetTempPath(), "tycoon-mcp-server.zip");
            string downloadUrl = "https://github.com/Jrandolph3110/tycoon-ai-bim-platform/releases/latest/download/mcp-server.zip";
            
            using (var client = new WebClient())
            {
                await client.DownloadFileTaskAsync(downloadUrl, tempZipPath);
            }
            
            return tempZipPath;
        }

        private async Task InstallMCP(string zipPath)
        {
            // Create directory
            Directory.CreateDirectory(mcpServerPath);
            
            // Extract files
            await Task.Run(() => System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, mcpServerPath));
            
            // Clean up
            File.Delete(zipPath);
        }

        private async Task InstallDependencies()
        {
            // Try to install npm dependencies
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "npm",
                    Arguments = "install",
                    WorkingDirectory = mcpServerPath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    process.WaitForExit();
                }
            }
            catch
            {
                // npm install failed, but continue
            }
        }

        private void ShowProgress(string message)
        {
            OptionsPanel.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;
            ProgressText.Text = message;
            ProgressBar.Value = 0;
            NextButton.IsEnabled = false;
        }

        private void UpdateProgress(string message, int percentage)
        {
            ProgressText.Text = message;
            ProgressBar.Value = percentage;
        }

        private void HideProgress()
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            OptionsPanel.Visibility = Visibility.Visible;
            NextButton.IsEnabled = true;
        }

        private void CompleteSetup(string message)
        {
            MessageBox.Show(message, "Setup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            
            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = file.Substring(sourceDir.Length + 1);
                string targetFile = Path.Combine(targetDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                File.Copy(file, targetFile, true);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Implementation for back navigation if needed
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
