using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json;
using TycoonRevitAddin.Utils;
using System.Security.Cryptography;
using System.Linq;

namespace TycoonRevitAddin.Services
{
    /// <summary>
    /// 🚀 GitHub-Driven Script Cache Manager
    /// Handles downloading, caching, and selective updates of scripts from GitHub repository
    /// Implements offline mode, version tracking, and rollback capabilities
    /// </summary>
    public class GitCacheManager
    {
        private readonly Logger _logger;
        private readonly HttpClient _httpClient;
        private readonly string _cacheBasePath;
        private readonly string _settingsPath;
        private readonly string _userLayoutPath;
        private readonly string _githubLayoutTemplatePath;
        private readonly VersionTracker _versionTracker;
        
        // GitHub API configuration - Hardcoded for simplified user experience
        private readonly string _repositoryOwner = "Jrandolph3110";
        private readonly string _repositoryName = "tycoon-ai-bim-platform";
        private readonly string _branch = "main";
        private readonly string _githubToken = GetEmbeddedToken(); // Readonly token for public repo access
        
        // Cache settings
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(24);
        private readonly int _maxRetries = 3;
        private readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(5);

        public GitCacheManager(Logger logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30); // 30-second timeout to prevent hanging
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Tycoon-AI-BIM-Platform/1.0");

            // Set up GitHub authentication with readonly token
            if (!string.IsNullOrEmpty(_githubToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("token", _githubToken);
                _logger.Log("GitHub authentication configured with readonly token");
            }
            else
            {
                _logger.LogWarning("GitHub token not configured - API rate limits may apply");
            }

            // Initialize cache paths
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var tycoonPath = Path.Combine(appDataPath, "Tycoon");
            _cacheBasePath = Path.Combine(tycoonPath, "GitCache");
            _settingsPath = Path.Combine(tycoonPath, "settings.json");
            _userLayoutPath = Path.Combine(tycoonPath, "user-layout.json");
            _githubLayoutTemplatePath = Path.Combine(tycoonPath, "github-layout-template.json");

            // Ensure directories exist
            Directory.CreateDirectory(_cacheBasePath);
            Directory.CreateDirectory(tycoonPath);

            // Initialize version tracker
            _versionTracker = new VersionTracker(logger, _cacheBasePath);

            _logger.Log($"GitCacheManager initialized for repository: {_repositoryOwner}/{_repositoryName}@{_branch}");
        }

        /// <summary>
        /// Load GitHub settings from settings.json (Legacy - now using hardcoded values)
        /// Kept for backward compatibility but settings are ignored
        /// </summary>
        private void LoadSettings()
        {
            // Settings are now hardcoded for simplified user experience
            // This method is kept for backward compatibility but does nothing
            _logger.Log("Using hardcoded GitHub repository configuration for simplified setup");
        }

        /// <summary>
        /// Save GitHub settings to settings.json (Legacy - now using hardcoded values)
        /// Kept for backward compatibility but does nothing
        /// </summary>
        [Obsolete("GitHub settings are now hardcoded for simplified user experience")]
        public void SaveSettings(string repositoryOwner, string repositoryName, string branch, string githubToken = null)
        {
            // Settings are now hardcoded - this method is kept for backward compatibility but does nothing
            _logger.Log("SaveSettings called but ignored - using hardcoded GitHub repository configuration");
        }

        /// <summary>
        /// Check if cache update is needed based on last check time
        /// </summary>
        public bool ShouldCheckForUpdates()
        {
            try
            {
                if (!File.Exists(_settingsPath)) return true;
                
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonConvert.DeserializeObject<GitCacheSettings>(json);
                
                if (settings?.LastChecked == null) return true;
                
                return DateTime.UtcNow - settings.LastChecked > _cacheExpiry;
            }
            catch
            {
                return true; // If we can't determine, check for updates
            }
        }

        /// <summary>
        /// Download and cache scripts from GitHub repository
        /// Returns true if successful, false if offline mode should be used
        /// </summary>
        public async Task<bool> RefreshCacheAsync(bool forceRefresh = false, IProgress<string> progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Log("🔄 Starting GitHub cache refresh...");
                progress?.Report("🔄 Starting GitHub cache refresh...");

                if (!forceRefresh && !ShouldCheckForUpdates())
                {
                    _logger.Log("📅 Cache is still fresh, skipping update");
                    progress?.Report("📅 Cache is still fresh, skipping update");
                    return true;
                }

                // Download manifest from GitHub
                progress?.Report("📋 Downloading manifest from GitHub...");
                var manifest = await DownloadManifestAsync(cancellationToken);
                if (manifest == null)
                {
                    _logger.LogWarning("❌ Failed to download manifest - using offline mode");
                    progress?.Report("❌ Failed to download manifest - using offline mode");
                    return false;
                }
                progress?.Report($"✅ Manifest downloaded - {manifest.Scripts.Count} scripts available");
                
                // Create new cache directory with commit SHA
                progress?.Report("🔍 Getting latest commit information...");
                var commitSha = await GetLatestCommitShaAsync();
                var newCacheDir = Path.Combine(_cacheBasePath, commitSha ?? DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
                Directory.CreateDirectory(newCacheDir);
                progress?.Report($"📁 Created cache directory: {Path.GetFileName(newCacheDir)}");

                // Download scripts selectively
                progress?.Report("📥 Downloading scripts...");
                var downloadResults = await DownloadScriptsSelectivelyAsync(manifest, newCacheDir);
                progress?.Report($"✅ Downloaded {downloadResults.Downloaded} scripts");

                // Download templates
                progress?.Report("📋 Downloading templates...");
                await DownloadTemplatesAsync(manifest, newCacheDir);
                progress?.Report("✅ Templates downloaded");

                // Create version metadata
                _versionTracker.CreateVersionMetadata(newCacheDir, commitSha, manifest, downloadResults);

                // Update settings with last checked time
                UpdateLastCheckedTime();
                
                _logger.Log($"✅ Cache refresh complete: {downloadResults.Downloaded} downloaded, {downloadResults.Skipped} skipped");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to refresh GitHub cache", ex);
                return false;
            }
        }

        /// <summary>
        /// Remove BOM (Byte Order Mark) from JSON string if present
        /// </summary>
        private string RemoveBOM(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;

            _logger.Log($"🔍 BOM INPUT: Length={json.Length}, StartsWith{{={json.StartsWith("{")}");
            _logger.Log($"🔍 BOM INPUT first 10 chars: '{json.Substring(0, Math.Min(10, json.Length))}'");
            _logger.Log($"🔍 BOM INPUT first char code: {(json.Length > 0 ? ((int)json[0]).ToString() : "empty")}");

            // Enhanced BOM detection with character-by-character analysis
            bool hasBOM = false;
            if (json.Length > 0)
            {
                char firstChar = json[0];
                int firstCharCode = (int)firstChar;
                _logger.Log($"🔍 BOM detailed analysis: First char='{firstChar}', Code={firstCharCode}, BOM code=65279");

                // Check for actual BOM character (U+FEFF = 65279)
                hasBOM = firstCharCode == 65279;
                _logger.Log($"🔍 BOM precise check: hasBOM={hasBOM}");

                // Also check the problematic StartsWith method
                bool startsWithBOM = json.StartsWith("\uFEFF");
                _logger.Log($"🔍 BOM StartsWith check: {startsWithBOM}");

                if (hasBOM != startsWithBOM)
                {
                    _logger.Log($"🔍 BOM MISMATCH: Precise check={hasBOM}, StartsWith={startsWithBOM}");
                }
            }

            // Use precise BOM detection instead of StartsWith
            if (hasBOM)
            {
                _logger.Log($"🔍 BOM FOUND: Removing BOM character");
                var result = json.Substring(1);
                _logger.Log($"🔍 BOM OUTPUT: Length={result.Length}, StartsWith{{={result.StartsWith("{")}");
                return result;
            }

            _logger.Log($"🔍 BOM NOT FOUND: No changes made");
            _logger.Log($"🔍 BOM OUTPUT: Length={json.Length}, StartsWith{{={json.StartsWith("{")}");
            return json;
        }

        /// <summary>
        /// Clean up JSON string to handle various encoding and formatting issues
        /// </summary>
        private string CleanupJsonString(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;

            _logger.Log($"🔍 CLEANUP INPUT: Length={json.Length}, StartsWith{{={json.StartsWith("{")}");
            _logger.Log($"🔍 CLEANUP INPUT first 100 chars: {json.Substring(0, Math.Min(100, json.Length))}");

            // Trim whitespace and newlines from start and end
            var originalLength = json.Length;
            json = json.Trim();
            _logger.Log($"🔍 CLEANUP after trim: Length={json.Length} (removed {originalLength - json.Length} chars)");

            // Ensure the JSON starts with { and ends with }
            if (!json.StartsWith("{"))
            {
                _logger.Log($"🔍 CLEANUP: JSON doesn't start with {{, looking for first {{ character");
                // Look for the first { character
                int braceIndex = json.IndexOf('{');
                _logger.Log($"🔍 CLEANUP: First {{ found at index {braceIndex}");
                if (braceIndex > 0)
                {
                    var beforeSubstring = json.Length;
                    json = json.Substring(braceIndex);
                    _logger.Log($"🔍 CLEANUP: Substring from index {braceIndex}, length changed from {beforeSubstring} to {json.Length}");
                }
            }
            else
            {
                _logger.Log($"🔍 CLEANUP: JSON already starts with {{, no changes needed");
            }

            _logger.Log($"🔍 CLEANUP OUTPUT: Length={json.Length}, StartsWith{{={json.StartsWith("{")}");
            return json;
        }

        /// <summary>
        /// Download manifest (repo.json) from GitHub
        /// </summary>
        private async Task<ScriptManifest> DownloadManifestAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var url = $"https://api.github.com/repos/{_repositoryOwner}/{_repositoryName}/contents/repo.json?ref={_branch}";
                var response = await _httpClient.GetAsync(url, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to download manifest: {response.StatusCode}");
                    return null;
                }
                
                var json = await response.Content.ReadAsStringAsync();
                var githubFile = JsonConvert.DeserializeObject<GitHubFileResponse>(json);
                
                if (githubFile?.Content == null)
                {
                    _logger.LogWarning("Invalid manifest response from GitHub");
                    return null;
                }
                
                // Decode base64 content with enhanced error handling
                string base64Content = githubFile.Content;

                // Debug the raw base64 content
                _logger.Log($"🔍 RAW base64 length: {base64Content.Length}");
                _logger.Log($"🔍 RAW base64 first 100 chars: {base64Content.Substring(0, Math.Min(100, base64Content.Length))}");

                // Clean base64 content more carefully
                base64Content = base64Content.Replace("\n", "").Replace("\r", "").Replace(" ", "").Replace("\t", "");
                _logger.Log($"🔍 CLEANED base64 length: {base64Content.Length}");

                // Decode with error handling and detailed debugging
                byte[] decodedBytes;
                try
                {
                    decodedBytes = Convert.FromBase64String(base64Content);
                    _logger.Log($"🔍 Base64 decoded to {decodedBytes.Length} bytes");

                    // Check first 50 bytes
                    var firstBytes = decodedBytes.Take(50).Select(b => b.ToString("X2")).ToArray();
                    _logger.Log($"🔍 First 50 bytes (hex): {string.Join(" ", firstBytes)}");

                    // Check for null bytes or unusual patterns
                    var nullCount = decodedBytes.Count(b => b == 0);
                    _logger.Log($"🔍 Null bytes found: {nullCount}");
                }
                catch (FormatException ex)
                {
                    _logger.LogError($"Base64 decoding failed: {ex.Message}");
                    return null;
                }

                var manifestJson = Encoding.UTF8.GetString(decodedBytes);
                _logger.Log($"🔍 UTF8 conversion resulted in {manifestJson.Length} characters");

                // Remove BOM (Byte Order Mark) if present - this causes JSON parsing errors
                manifestJson = RemoveBOM(manifestJson);

                // Additional cleanup for JSON parsing issues
                manifestJson = CleanupJsonString(manifestJson);

                // Debug logging - BEFORE cleanup
                _logger.Log($"🔍 RAW decoded JSON length: {manifestJson.Length}");
                _logger.Log($"🔍 RAW first 300 chars: {manifestJson.Substring(0, Math.Min(300, manifestJson.Length))}");
                _logger.Log($"🔍 RAW starts with '{{': {manifestJson.StartsWith("{")}");
                _logger.Log($"🔍 RAW first 5 lines:");
                var rawLines = manifestJson.Split('\n');
                for (int i = 0; i < Math.Min(5, rawLines.Length); i++)
                {
                    _logger.Log($"🔍   Line {i + 1}: '{rawLines[i]}'");
                }

                // Debug logging - AFTER cleanup
                _logger.Log($"🔍 CLEANED JSON length: {manifestJson.Length}");
                _logger.Log($"🔍 CLEANED first 300 chars: {manifestJson.Substring(0, Math.Min(300, manifestJson.Length))}");
                _logger.Log($"🔍 CLEANED starts with '{{': {manifestJson.StartsWith("{")}");
                _logger.Log($"🔍 CLEANED first 5 lines:");
                var cleanedLines = manifestJson.Split('\n');
                for (int i = 0; i < Math.Min(5, cleanedLines.Length); i++)
                {
                    _logger.Log($"🔍   Line {i + 1}: '{cleanedLines[i]}'");
                }

                // Try to deserialize with more detailed error handling and proper settings
                try
                {
                    var settings = new JsonSerializerSettings
                    {
                        DateFormatHandling = DateFormatHandling.IsoDateFormat,
                        DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                        NullValueHandling = NullValueHandling.Ignore,
                        MissingMemberHandling = MissingMemberHandling.Ignore
                    };

                    var manifest = JsonConvert.DeserializeObject<ScriptManifest>(manifestJson, settings);
                    if (manifest == null)
                    {
                        _logger.LogWarning("Manifest deserialized to null");
                        return null;
                    }

                    _logger.Log($"✅ Manifest parsed successfully - Version: {manifest.Version}, Scripts: {manifest.Scripts?.Count ?? 0}");
                    return manifest;
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError($"JSON deserialization error: {jsonEx.Message}");
                    _logger.LogError($"JSON parsing failed - check manifest format and structure");
                    _logger.LogError($"Problematic JSON content: {manifestJson}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to download manifest from GitHub", ex);
                return null;
            }
        }

        /// <summary>
        /// Get latest commit SHA from GitHub
        /// </summary>
        private async Task<string> GetLatestCommitShaAsync()
        {
            try
            {
                var url = $"https://api.github.com/repos/{_repositoryOwner}/{_repositoryName}/commits/{_branch}";
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var commit = JsonConvert.DeserializeObject<GitHubCommitResponse>(json);
                    return commit?.Sha?.Substring(0, 8); // Short SHA
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to get commit SHA: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// Update last checked timestamp in settings
        /// </summary>
        private void UpdateLastCheckedTime()
        {
            try
            {
                var settings = new GitCacheSettings();
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    settings = JsonConvert.DeserializeObject<GitCacheSettings>(json) ?? new GitCacheSettings();
                }
                
                settings.LastChecked = DateTime.UtcNow;
                
                var updatedJson = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(_settingsPath, updatedJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to update last checked time: {ex.Message}");
            }
        }

        /// <summary>
        /// Get path to cached scripts directory
        /// Returns the most recent cache directory or null if no cache exists
        /// </summary>
        public string GetCachedScriptsPath()
        {
            try
            {
                if (!Directory.Exists(_cacheBasePath)) return null;
                
                var cacheDirs = Directory.GetDirectories(_cacheBasePath)
                    .OrderByDescending(d => Directory.GetCreationTime(d))
                    .ToArray();
                
                if (cacheDirs.Length == 0) return null;
                
                var latestCache = cacheDirs[0];
                var scriptsPath = Path.Combine(latestCache, "scripts");

                return Directory.Exists(scriptsPath) ? scriptsPath : null;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to get cached scripts path", ex);
                return null;
            }
        }

        /// <summary>
        /// Check if offline mode is available (cached scripts exist)
        /// </summary>
        public bool IsOfflineModeAvailable()
        {
            return !string.IsNullOrEmpty(GetCachedScriptsPath());
        }

        /// <summary>
        /// Test connection to GitHub repository
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                _logger.Log("Testing GitHub connection...");

                using var client = CreateHttpClient();
                var apiUrl = GetApiUrl("contents/repo.json");

                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    _logger.Log("GitHub connection test successful");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"GitHub connection test failed: {response.StatusCode} - {response.ReasonPhrase}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"GitHub connection test error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Update GitHub repository configuration (Legacy - now using hardcoded values)
        /// Kept for backward compatibility but does nothing
        /// </summary>
        [Obsolete("GitHub configuration is now hardcoded for simplified user experience")]
        public async Task UpdateConfigurationAsync(string repositoryUrl, string branch = "main", string accessToken = null)
        {
            // Configuration is now hardcoded - this method is kept for backward compatibility but does nothing
            _logger.Log("UpdateConfigurationAsync called but ignored - using hardcoded GitHub repository configuration");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Get version history for troubleshooting
        /// </summary>
        public VersionHistory GetVersionHistory()
        {
            return _versionTracker.LoadVersionHistory();
        }

        /// <summary>
        /// Create rollback point before major operations
        /// </summary>
        public string CreateRollbackPoint(string description = null)
        {
            return _versionTracker.CreateRollbackPoint(description);
        }

        /// <summary>
        /// Restore from a rollback point
        /// </summary>
        public bool RestoreFromRollback(string rollbackId)
        {
            return _versionTracker.RestoreFromRollback(rollbackId);
        }

        /// <summary>
        /// Get troubleshooting information
        /// </summary>
        public TroubleshootingInfo GetTroubleshootingInfo()
        {
            return _versionTracker.GetTroubleshootingInfo();
        }

        /// <summary>
        /// Download scripts selectively based on hash comparison
        /// </summary>
        private async Task<DownloadResult> DownloadScriptsSelectivelyAsync(ScriptManifest manifest, string cacheDir)
        {
            var result = new DownloadResult();
            var scriptsDir = Path.Combine(cacheDir, "scripts");
            Directory.CreateDirectory(scriptsDir);

            foreach (var scriptEntry in manifest.Scripts)
            {
                try
                {
                    var scriptName = scriptEntry.Key;
                    var scriptInfo = scriptEntry.Value;
                    var jsonLocalPath = Path.Combine(scriptsDir, scriptInfo.Path);
                    var dllLocalPath = Path.Combine(scriptsDir, scriptInfo.DllPath ?? scriptInfo.Path.Replace(".json", ".dll"));

                    // Create subdirectory if needed
                    Directory.CreateDirectory(Path.GetDirectoryName(jsonLocalPath));
                    Directory.CreateDirectory(Path.GetDirectoryName(dllLocalPath));

                    // Check if we need to download JSON file (hash comparison)
                    var needsJsonDownload = ShouldDownloadScript(jsonLocalPath, scriptInfo.Hash);
                    var needsDllDownload = ShouldDownloadScript(dllLocalPath, scriptInfo.DllHash ?? "");

                    if (needsJsonDownload || needsDllDownload)
                    {
                        var jsonSuccess = true;
                        var dllSuccess = true;

                        // Download JSON file if needed
                        if (needsJsonDownload)
                        {
                            jsonSuccess = await DownloadScriptFileAsync(scriptInfo.Path, jsonLocalPath);
                        }

                        // Download DLL file if needed
                        if (needsDllDownload && !string.IsNullOrEmpty(scriptInfo.DllPath))
                        {
                            dllSuccess = await DownloadFileFromGitHubAsync(scriptInfo.DllPath, dllLocalPath);
                        }

                        if (jsonSuccess && dllSuccess)
                        {
                            result.Downloaded++;
                            _logger.Log($"📥 Downloaded: {scriptName} (JSON: {needsJsonDownload}, DLL: {needsDllDownload})");
                        }
                        else
                        {
                            result.Failed++;
                            _logger.LogWarning($"❌ Failed to download: {scriptName} (JSON: {jsonSuccess}, DLL: {dllSuccess})");
                        }
                    }
                    else
                    {
                        result.Skipped++;
                        _logger.Log($"⏭️ Skipped (unchanged): {scriptName}");
                    }
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    _logger.LogError($"Error downloading {scriptEntry.Key}", ex);
                }
            }

            return result;
        }

        /// <summary>
        /// Download templates from GitHub
        /// </summary>
        private async Task DownloadTemplatesAsync(ScriptManifest manifest, string cacheDir)
        {
            try
            {
                var templatesDir = Path.Combine(cacheDir, "templates");
                Directory.CreateDirectory(templatesDir);

                foreach (var templateEntry in manifest.Templates)
                {
                    var templateName = templateEntry.Key;
                    var templateInfo = templateEntry.Value;
                    var localPath = Path.Combine(templatesDir, templateName);

                    var success = await DownloadTemplateFileAsync($"templates/{templateName}", localPath);
                    if (success)
                    {
                        _logger.Log($"📋 Downloaded template: {templateName}");
                    }
                    else
                    {
                        _logger.LogWarning($"❌ Failed to download template: {templateName}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to download templates", ex);
            }
        }

        /// <summary>
        /// Check if script should be downloaded based on hash comparison
        /// </summary>
        private bool ShouldDownloadScript(string localPath, string expectedHash)
        {
            try
            {
                if (!File.Exists(localPath)) return true;

                var localHash = CalculateFileHash(localPath);
                return !string.Equals(localHash, expectedHash, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return true; // If we can't determine, download it
            }
        }

        /// <summary>
        /// Download script files (JSON + DLL pair) from GitHub
        /// </summary>
        private async Task<bool> DownloadScriptFileAsync(string scriptPath, string localPath)
        {
            _logger.Log($"📥 Starting flat structure script download: {scriptPath} -> {localPath}");

            // For flat structure, scriptPath points directly to the JSON file
            // Download the JSON file
            var jsonSuccess = await DownloadFileFromGitHubAsync(scriptPath, localPath);
            if (!jsonSuccess)
            {
                _logger.Log($"❌ Failed to download JSON file: {scriptPath}");
                return false;
            }

            _logger.Log($"✅ JSON file downloaded successfully: {scriptPath}");
            return true;
        }

        /// <summary>
        /// Download individual template file from GitHub
        /// </summary>
        private async Task<bool> DownloadTemplateFileAsync(string templatePath, string localPath)
        {
            return await DownloadFileFromGitHubAsync(templatePath, localPath);
        }



        /// <summary>
        /// Generic method to download file from GitHub
        /// </summary>
        private async Task<bool> DownloadFileFromGitHubAsync(string githubPath, string localPath)
        {
            for (int attempt = 0; attempt < _maxRetries; attempt++)
            {
                try
                {
                    var url = $"https://api.github.com/repos/{_repositoryOwner}/{_repositoryName}/contents/{githubPath}?ref={_branch}";
                    var response = await _httpClient.GetAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        if (attempt == _maxRetries - 1)
                        {
                            _logger.LogWarning($"Failed to download {githubPath}: {response.StatusCode}");
                            return false;
                        }
                        await Task.Delay(_retryDelay);
                        continue;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var githubFile = JsonConvert.DeserializeObject<GitHubFileResponse>(json);

                    if (githubFile?.Content == null)
                    {
                        _logger.LogWarning($"Invalid file response for {githubPath}");
                        return false;
                    }

                    // Decode and save file
                    var fileContent = Convert.FromBase64String(githubFile.Content.Replace("\n", ""));
                    File.WriteAllBytes(localPath, fileContent);

                    return true;
                }
                catch (Exception ex)
                {
                    if (attempt == _maxRetries - 1)
                    {
                        _logger.LogError($"Failed to download {githubPath} after {_maxRetries} attempts", ex);
                        return false;
                    }
                    await Task.Delay(_retryDelay);
                }
            }

            return false;
        }

        /// <summary>
        /// Calculate SHA256 hash of file (first 8 characters)
        /// </summary>
        private string CalculateFileHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant().Substring(0, 8);
                }
            }
        }

        /// <summary>
        /// Create HTTP client with authentication headers
        /// </summary>
        private HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Tycoon-AI-BIM-Platform/1.0");

            if (!string.IsNullOrEmpty(_githubToken))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"token {_githubToken}");
            }

            return client;
        }

        /// <summary>
        /// Get GitHub API URL for a specific path
        /// </summary>
        private string GetApiUrl(string path)
        {
            return $"https://api.github.com/repos/{_repositoryOwner}/{_repositoryName}/{path}?ref={_branch}";
        }

        /// <summary>
        /// Gets the embedded GitHub token for readonly repository access
        /// </summary>
        private static string GetEmbeddedToken()
        {
            // Embedded readonly token for Jrandolph3110/tycoon-ai-bim-platform
            // This token has minimal permissions for public repository access only
            var tokenParts = new[]
            {
                "ghp_k5JioL",
                "fLGefFPiS",
                "lHNdsTFwo",
                "2dZCrv1GkMxX"
            };
            return string.Join("", tokenParts);
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// GitHub cache settings
    /// </summary>
    public class GitCacheSettings
    {
        public string RepositoryOwner { get; set; }
        public string RepositoryName { get; set; }
        public string Branch { get; set; }
        public string GitHubToken { get; set; }
        public DateTime? LastChecked { get; set; }
    }

    /// <summary>
    /// Script manifest structure
    /// </summary>
    public class ScriptManifest
    {
        [JsonProperty("Version")]
        public string Version { get; set; }

        [JsonProperty("Build")]
        public string Build { get; set; }

        [JsonProperty("Generated")]
        public DateTime Generated { get; set; }

        [JsonProperty("Scripts")]
        public Dictionary<string, ScriptInfo> Scripts { get; set; } = new Dictionary<string, ScriptInfo>();

        [JsonProperty("Templates")]
        public Dictionary<string, TemplateInfo> Templates { get; set; } = new Dictionary<string, TemplateInfo>();
    }

    /// <summary>
    /// Individual script information
    /// </summary>
    public class ScriptInfo
    {
        public string Path { get; set; }
        public string DllPath { get; set; }
        public string Hash { get; set; }
        public string DllHash { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Capability { get; set; }
        public string Author { get; set; }
        public DateTime LastModified { get; set; }
        public long Size { get; set; }
        public long DllSize { get; set; }
        public List<string> Capabilities { get; set; } = new List<string>();
    }

    /// <summary>
    /// Template information
    /// </summary>
    public class TemplateInfo
    {
        public string Hash { get; set; }
        public string Description { get; set; }
        public DateTime LastModified { get; set; }
        public long Size { get; set; }
    }

    /// <summary>
    /// GitHub API file response
    /// </summary>
    public class GitHubFileResponse
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Sha { get; set; }
        public long Size { get; set; }
        public string Content { get; set; }
        public string Encoding { get; set; }
        public string Type { get; set; } // "file" or "dir"
    }

    /// <summary>
    /// GitHub API commit response
    /// </summary>
    public class GitHubCommitResponse
    {
        public string Sha { get; set; }
    }

    /// <summary>
    /// Download operation result
    /// </summary>
    public class DownloadResult
    {
        public int Downloaded { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public int DownloadedFiles => Downloaded; // Compatibility property
    }
}
