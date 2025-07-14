using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using TycoonRevitAddin.Utils;
using Tycoon.Scripting.Contracts;
using Newtonsoft.Json;

namespace TycoonRevitAddin.Scripting
{
    /// <summary>
    /// LocalScriptProvider - Provides scripts from local filesystem with hot-reload capability
    /// Uses FileSystemWatcher to detect changes and automatically reload scripts
    /// Perfect for development workflow: edit → save → auto-reload in Revit
    /// </summary>
    public class LocalScriptProvider : IScriptProvider
    {
        private readonly Logger _logger;
        private readonly string _scriptsPath;
        private readonly object _lock = new object();
        
        // File watching
        private FileSystemWatcher _fileWatcher;
        private Timer _debounceTimer;
        private readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(500);
        
        // State
        private List<ScriptInfo> _currentScripts = new List<ScriptInfo>();
        private bool _isInitialized = false;
        private bool _disposed = false;

        public event Action<List<ScriptInfo>> ScriptsChanged;

        public LocalScriptProvider(Logger logger, string scriptsPath)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scriptsPath = scriptsPath ?? throw new ArgumentNullException(nameof(scriptsPath));
            
            _logger.Log($"📁 LocalScriptProvider created for path: {_scriptsPath}");
        }

        /// <summary>
        /// Initialize the provider and start watching for changes
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                _logger.Log("LocalScriptProvider already initialized");
                return;
            }

            _logger.Log($"🚀 Initializing LocalScriptProvider for development mode");
            
            // Ensure directory exists
            if (!Directory.Exists(_scriptsPath))
            {
                Directory.CreateDirectory(_scriptsPath);
                _logger.Log($"📁 Created scripts directory: {_scriptsPath}");
            }

            // Initial script discovery
            await RefreshAsync();

            // Set up file system watcher
            SetupFileWatcher();

            _isInitialized = true;
            _logger.Log($"✅ LocalScriptProvider initialized - watching {_scriptsPath}");
        }

        /// <summary>
        /// Manually refresh scripts from filesystem
        /// </summary>
        public async Task RefreshAsync()
        {
            try
            {
                _logger.Log("🔄 Refreshing scripts from local filesystem");
                
                var discoveryResult = await Task.Run(() => DiscoverScripts());
                
                lock (_lock)
                {
                    _currentScripts = discoveryResult.Scripts;
                }
                
                _logger.Log($"📜 Discovered {discoveryResult.Scripts.Count} local scripts");
                
                if (discoveryResult.Errors.Any())
                {
                    foreach (var error in discoveryResult.Errors)
                    {
                        _logger.LogWarning($"Script discovery error: {error}");
                    }
                }
                
                // Notify listeners
                ScriptsChanged?.Invoke(discoveryResult.Scripts.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to refresh local scripts", ex);
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
        /// Set up FileSystemWatcher for hot-reload
        /// </summary>
        private void SetupFileWatcher()
        {
            try
            {
                _fileWatcher = new FileSystemWatcher(_scriptsPath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
                };

                // Watch for relevant file changes
                _fileWatcher.Changed += OnFileChanged;
                _fileWatcher.Created += OnFileChanged;
                _fileWatcher.Deleted += OnFileChanged;
                _fileWatcher.Renamed += OnFileRenamed;

                // Set up debounce timer
                _debounceTimer = new Timer(_debounceDelay.TotalMilliseconds)
                {
                    AutoReset = false
                };
                _debounceTimer.Elapsed += OnDebounceTimerElapsed;

                _fileWatcher.EnableRaisingEvents = true;
                _logger.Log("👁️ FileSystemWatcher enabled for hot-reload");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to setup FileSystemWatcher", ex);
                throw;
            }
        }

        /// <summary>
        /// Handle file system changes (debounced)
        /// </summary>
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (ShouldProcessFile(e.FullPath))
            {
                _logger.Log($"📝 File change detected: {e.FullPath} ({e.ChangeType})");
                RestartDebounceTimer();
            }
        }

        /// <summary>
        /// Handle file renames
        /// </summary>
        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (ShouldProcessFile(e.FullPath) || ShouldProcessFile(e.OldFullPath))
            {
                _logger.Log($"📝 File renamed: {e.OldFullPath} → {e.FullPath}");
                RestartDebounceTimer();
            }
        }

        /// <summary>
        /// Check if we should process this file change
        /// </summary>
        private bool ShouldProcessFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension == ".dll" || extension == ".json";
        }

        /// <summary>
        /// Restart the debounce timer
        /// </summary>
        private void RestartDebounceTimer()
        {
            _debounceTimer?.Stop();
            _debounceTimer?.Start();
        }

        /// <summary>
        /// Handle debounced file changes
        /// </summary>
        private async void OnDebounceTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                _logger.Log("⏰ Debounce timer elapsed - refreshing scripts for hot-reload");
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to refresh scripts after file change", ex);
            }
        }

        /// <summary>
        /// Discover scripts in the local directory
        /// </summary>
        private ScriptDiscoveryResult DiscoverScripts()
        {
            var result = new ScriptDiscoveryResult();
            
            try
            {
                if (!Directory.Exists(_scriptsPath))
                {
                    _logger.Log($"Scripts directory does not exist: {_scriptsPath}");
                    return result;
                }

                // Find all script.json manifest files
                var manifestFiles = Directory.GetFiles(_scriptsPath, "script.json", SearchOption.AllDirectories);
                
                _logger.Log($"🔍 Found {manifestFiles.Length} manifest files");

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
                var error = $"Failed to discover scripts in {_scriptsPath}: {ex.Message}";
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
                    throw new InvalidOperationException("Manifest missing required fields (name, entryAssembly, entryType)");
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
                    Source = ScriptEngineMode.Development
                };

                _logger.Log($"✅ Processed script: {manifest.Name} ({manifest.Version})");
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

            _logger.Log("🗑️ Disposing LocalScriptProvider");

            _fileWatcher?.Dispose();
            _debounceTimer?.Dispose();

            _disposed = true;
        }
    }
}
