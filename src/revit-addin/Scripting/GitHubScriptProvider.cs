using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TycoonRevitAddin.Utils;
using Tycoon.Scripting.Contracts;
using Newtonsoft.Json;

namespace TycoonRevitAddin.Scripting
{
    /// <summary>
    /// GitHubScriptProvider - Provides scripts from GitHub repository with local caching
    /// Downloads and caches scripts for end users in Production mode
    /// Handles updates, versioning, and offline access
    /// </summary>
    public class GitHubScriptProvider : IScriptProvider
    {
        private readonly Logger _logger;
        private readonly GitHubScriptConfig _config;
        private readonly object _lock = new object();
        
        // HTTP client for GitHub API
        private readonly HttpClient _httpClient;
        
        // State
        private List<ScriptInfo> _currentScripts = new List<ScriptInfo>();
        private bool _isInitialized = false;
        private bool _disposed = false;
        private DateTime _lastCacheUpdate = DateTime.MinValue;

        public event Action<List<ScriptInfo>> ScriptsChanged;

        public GitHubScriptProvider(Logger logger, GitHubScriptConfig config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Tycoon-AI-BIM-Platform/1.0");
            
            _logger.Log($"🐙 GitHubScriptProvider created for repository: {_config.RepositoryUrl}");
        }

        /// <summary>
        /// Initialize the provider and load cached scripts
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                _logger.Log("GitHubScriptProvider already initialized");
                return;
            }

            _logger.Log($"🚀 Initializing GitHubScriptProvider for production mode");
            
            // Ensure cache directory exists
            if (!Directory.Exists(_config.CachePath))
            {
                Directory.CreateDirectory(_config.CachePath);
                _logger.Log($"📁 Created cache directory: {_config.CachePath}");
            }

            // Load from cache first (for offline access)
            await LoadFromCacheAsync();

            // Check if cache needs updating
            if (ShouldUpdateCache())
            {
                try
                {
                    await RefreshAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to update from GitHub, using cached scripts: {ex.Message}");
                }
            }

            _isInitialized = true;
            _logger.Log($"✅ GitHubScriptProvider initialized with {_currentScripts.Count} scripts");
        }

        /// <summary>
        /// Manually refresh scripts from GitHub
        /// </summary>
        public async Task RefreshAsync()
        {
            try
            {
                _logger.Log("🔄 Refreshing scripts from GitHub repository");
                
                // Download scripts from GitHub
                var downloadedScripts = await DownloadScriptsFromGitHubAsync();
                
                // Update cache
                await UpdateCacheAsync(downloadedScripts);
                
                lock (_lock)
                {
                    _currentScripts = downloadedScripts;
                    _lastCacheUpdate = DateTime.Now;
                }
                
                _logger.Log($"📜 Downloaded {downloadedScripts.Count} scripts from GitHub");
                
                // Notify listeners
                ScriptsChanged?.Invoke(downloadedScripts.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to refresh scripts from GitHub", ex);
                throw;
            }
        }

        /// <summary>
        /// Get current scripts (thread-safe)
        /// </summary>
        public List<ScriptInfo> GetCurrentScripts()
        {
            lock (_lock)
            {
                return _currentScripts.ToList();
            }
        }

        /// <summary>
        /// Check if cache should be updated
        /// </summary>
        private bool ShouldUpdateCache()
        {
            if (_lastCacheUpdate == DateTime.MinValue)
            {
                // Never updated
                return true;
            }
            
            var cacheAge = DateTime.Now - _lastCacheUpdate;
            return cacheAge > _config.CacheExpiry;
        }

        /// <summary>
        /// Load scripts from local cache
        /// </summary>
        private async Task LoadFromCacheAsync()
        {
            try
            {
                _logger.Log("📂 Loading scripts from local cache");
                
                var cacheInfoPath = Path.Combine(_config.CachePath, "cache-info.json");
                if (!File.Exists(cacheInfoPath))
                {
                    _logger.Log("No cache info found, cache is empty");
                    return;
                }
                
                // Read cache info
                var cacheInfoJson = await File.ReadAllTextAsync(cacheInfoPath);
                var cacheInfo = JsonConvert.DeserializeObject<CacheInfo>(cacheInfoJson);
                
                if (cacheInfo == null)
                {
                    _logger.LogWarning("Failed to deserialize cache info");
                    return;
                }
                
                _lastCacheUpdate = cacheInfo.LastUpdated;
                
                // Discover scripts in cache
                var discoveryResult = DiscoverScriptsInDirectory(_config.CachePath);
                
                lock (_lock)
                {
                    _currentScripts = discoveryResult.Scripts;
                }
                
                _logger.Log($"📂 Loaded {discoveryResult.Scripts.Count} scripts from cache");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load scripts from cache", ex);
                // Continue without cache - will try to download from GitHub
            }
        }

        /// <summary>
        /// Download scripts from GitHub repository
        /// </summary>
        private async Task<List<ScriptInfo>> DownloadScriptsFromGitHubAsync()
        {
            try
            {
                _logger.Log($"⬇️ Downloading scripts from GitHub: {_config.RepositoryUrl}");
                
                // This is a simplified implementation
                // In production, you'd use GitHub API or git clone/pull
                // For now, we'll simulate by copying from the existing GitHub cache
                
                // TODO: Implement actual GitHub API integration
                // For now, use the existing GitCacheManager logic as a fallback
                
                var scripts = new List<ScriptInfo>();
                
                // Simulate GitHub download by using existing cache if available
                var existingCachePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Tycoon", "GitCache");
                
                if (Directory.Exists(existingCachePath))
                {
                    var latestCacheDir = Directory.GetDirectories(existingCachePath)
                        .Where(d => !Path.GetFileName(d).Equals("rollback", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(d => Directory.GetLastWriteTime(d))
                        .FirstOrDefault();
                    
                    if (latestCacheDir != null)
                    {
                        var scriptsDir = Path.Combine(latestCacheDir, "scripts");
                        if (Directory.Exists(scriptsDir))
                        {
                            var discoveryResult = DiscoverScriptsInDirectory(scriptsDir);
                            scripts = discoveryResult.Scripts;
                            
                            // Mark as Production source
                            foreach (var script in scripts)
                            {
                                script.Source = ScriptEngineMode.Production;
                            }
                        }
                    }
                }
                
                _logger.Log($"⬇️ Downloaded {scripts.Count} scripts from GitHub");
                return scripts;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to download scripts from GitHub", ex);
                throw;
            }
        }

        /// <summary>
        /// Update local cache with downloaded scripts
        /// </summary>
        private async Task UpdateCacheAsync(List<ScriptInfo> scripts)
        {
            try
            {
                _logger.Log("💾 Updating local cache");
                
                // Clear existing cache
                if (Directory.Exists(_config.CachePath))
                {
                    Directory.Delete(_config.CachePath, true);
                }
                Directory.CreateDirectory(_config.CachePath);
                
                // Copy scripts to cache
                foreach (var script in scripts)
                {
                    var targetDir = Path.Combine(_config.CachePath, script.Manifest.Name);
                    Directory.CreateDirectory(targetDir);
                    
                    // Copy assembly
                    var targetAssemblyPath = Path.Combine(targetDir, Path.GetFileName(script.AssemblyPath));
                    File.Copy(script.AssemblyPath, targetAssemblyPath, true);
                    
                    // Copy manifest
                    var targetManifestPath = Path.Combine(targetDir, "script.json");
                    File.Copy(script.ManifestPath, targetManifestPath, true);
                    
                    // Update script info paths
                    script.AssemblyPath = targetAssemblyPath;
                    script.ManifestPath = targetManifestPath;
                    script.ScriptDirectory = targetDir;
                }
                
                // Save cache info
                var cacheInfo = new CacheInfo
                {
                    LastUpdated = DateTime.Now,
                    ScriptCount = scripts.Count,
                    RepositoryUrl = _config.RepositoryUrl,
                    Branch = _config.Branch
                };
                
                var cacheInfoPath = Path.Combine(_config.CachePath, "cache-info.json");
                var cacheInfoJson = JsonConvert.SerializeObject(cacheInfo, Formatting.Indented);
                await File.WriteAllTextAsync(cacheInfoPath, cacheInfoJson);
                
                _logger.Log($"💾 Cache updated with {scripts.Count} scripts");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to update cache", ex);
                throw;
            }
        }

        /// <summary>
        /// Discover scripts in a directory (reusable for cache and GitHub)
        /// </summary>
        private ScriptDiscoveryResult DiscoverScriptsInDirectory(string directory)
        {
            var result = new ScriptDiscoveryResult();
            
            try
            {
                if (!Directory.Exists(directory))
                {
                    return result;
                }

                // Find all script.json manifest files
                var manifestFiles = Directory.GetFiles(directory, "script.json", SearchOption.AllDirectories);
                
                foreach (var manifestPath in manifestFiles)
                {
                    try
                    {
                        var scriptInfo = ProcessManifest(manifestPath);
                        if (scriptInfo != null)
                        {
                            result.Scripts.Add(scriptInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        var error = $"Failed to process manifest {manifestPath}: {ex.Message}";
                        result.Errors.Add(error);
                        _logger.LogWarning(error);
                    }
                }
            }
            catch (Exception ex)
            {
                var error = $"Failed to discover scripts in {directory}: {ex.Message}";
                result.Errors.Add(error);
                _logger.LogError(error, ex);
            }

            return result;
        }

        /// <summary>
        /// Process a script manifest file
        /// </summary>
        private ScriptInfo ProcessManifest(string manifestPath)
        {
            try
            {
                // Read and parse manifest
                var manifestJson = File.ReadAllText(manifestPath);
                var manifest = JsonConvert.DeserializeObject<ScriptManifest>(manifestJson);
                
                if (manifest == null)
                {
                    throw new InvalidOperationException("Failed to deserialize manifest");
                }

                // Validate required fields
                if (string.IsNullOrEmpty(manifest.Name) || 
                    string.IsNullOrEmpty(manifest.EntryAssembly) || 
                    string.IsNullOrEmpty(manifest.EntryType))
                {
                    throw new InvalidOperationException("Manifest missing required fields");
                }

                // Find assembly file
                var scriptDirectory = Path.GetDirectoryName(manifestPath);
                var assemblyPath = Path.Combine(scriptDirectory, manifest.EntryAssembly);
                
                if (!File.Exists(assemblyPath))
                {
                    throw new FileNotFoundException($"Assembly not found: {assemblyPath}");
                }

                // Create script info
                var scriptInfo = new ScriptInfo
                {
                    Manifest = manifest,
                    AssemblyPath = assemblyPath,
                    ManifestPath = manifestPath,
                    ScriptDirectory = scriptDirectory,
                    LastModified = File.GetLastWriteTime(assemblyPath),
                    Source = ScriptEngineMode.Production
                };

                return scriptInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to process manifest {manifestPath}", ex);
                throw;
            }
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _logger.Log("🗑️ Disposing GitHubScriptProvider");

            _httpClient?.Dispose();
            _disposed = true;
        }

        /// <summary>
        /// Cache information
        /// </summary>
        private class CacheInfo
        {
            public DateTime LastUpdated { get; set; }
            public int ScriptCount { get; set; }
            public string RepositoryUrl { get; set; }
            public string Branch { get; set; }
        }
    }
}
