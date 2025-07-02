using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Threading;
using TycoonRevitAddin.Models;
using TycoonRevitAddin.Plugins;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.Services
{
    /// <summary>
    /// 🎯 ScriptService - Single Source of Truth for All Scripts
    /// Async-first architecture with event-based pub/sub pattern for UI notifications
    /// Replaces synchronous blocking approach with proper Revit API best practices
    /// </summary>
    public class ScriptService
    {
        #region Singleton Pattern
        private static readonly Lazy<ScriptService> _instance = new Lazy<ScriptService>(() => new ScriptService());
        public static ScriptService Instance => _instance.Value;
        #endregion

        #region Private Fields
        private Logger _logger;
        private GitCacheManager _gitCacheManager;

        private readonly ScriptParser _scriptParser;
        
        private List<ScriptViewModel> _localScripts = new List<ScriptViewModel>();
        private List<ScriptViewModel> _githubScripts = new List<ScriptViewModel>();
        
        private bool _isUpdating = false;
        private string _updateStatus = "Ready";
        private DateTime? _lastUpdated = null;
        #endregion

        #region Public Events
        /// <summary>
        /// Fired when local scripts are loaded or updated
        /// </summary>
        public event Action<IEnumerable<ScriptViewModel>> LocalScriptsUpdated;
        
        /// <summary>
        /// Fired when GitHub scripts are loaded or updated
        /// </summary>
        public event Action<IEnumerable<ScriptViewModel>> GitHubScriptsUpdated;
        
        /// <summary>
        /// Fired when update status changes (loading, success, error)
        /// </summary>
        public event Action<string> UpdateStatusChanged;
        
        /// <summary>
        /// Fired when updating state changes (true = updating, false = complete)
        /// </summary>
        public event Action<bool> IsUpdatingChanged;
        #endregion

        #region Public Properties
        /// <summary>
        /// Whether scripts are currently being updated from GitHub
        /// </summary>
        public bool IsUpdating
        {
            get => _isUpdating;
            private set
            {
                if (_isUpdating != value)
                {
                    _isUpdating = value;
                    NotifyIsUpdatingChanged(value);
                }
            }
        }

        /// <summary>
        /// Current update status message
        /// </summary>
        public string UpdateStatus
        {
            get => _updateStatus;
            private set
            {
                if (_updateStatus != value)
                {
                    _updateStatus = value;
                    NotifyUpdateStatusChanged(value);
                }
            }
        }

        /// <summary>
        /// Last successful update time
        /// </summary>
        public DateTime? LastUpdated => _lastUpdated;
        #endregion

        #region Constructor
        private ScriptService()
        {
            _logger = new Logger("ScriptService"); // Temporary logger until proper injection
            _scriptParser = new ScriptParser(_logger);

            // GitCacheManager will be injected during initialization
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Initialize the service with dependencies and start async loading
        /// This is the ONLY method called from OnStartup - it returns immediately
        /// </summary>
        public void Initialize(GitCacheManager gitCacheManager, Logger logger, string localScriptsPath)
        {
            _gitCacheManager = gitCacheManager;
            _logger = logger;
            
            _logger.Log("🎯 ScriptService initializing with async-first architecture");
            
            // Fire-and-forget async initialization - DO NOT await this
            _ = Task.Run(async () => await InitializeAsync(localScriptsPath));
        }

        /// <summary>
        /// Get current local scripts (thread-safe)
        /// </summary>
        public IEnumerable<ScriptViewModel> GetCurrentLocalScripts()
        {
            lock (_localScripts)
            {
                return _localScripts.ToList(); // Return copy to avoid threading issues
            }
        }

        /// <summary>
        /// Get current GitHub scripts (thread-safe)
        /// </summary>
        public IEnumerable<ScriptViewModel> GetCurrentGitHubScripts()
        {
            lock (_githubScripts)
            {
                return _githubScripts.ToList(); // Return copy to avoid threading issues
            }
        }

        /// <summary>
        /// Get the GitCacheManager instance for legacy compatibility
        /// </summary>
        public GitCacheManager GetGitCacheManager()
        {
            return _gitCacheManager;
        }

        /// <summary>
        /// Manually refresh GitHub scripts (for retry functionality)
        /// </summary>
        public async Task RefreshGitHubScriptsAsync()
        {
            if (_isUpdating)
            {
                _logger.Log("🔄 GitHub refresh already in progress, skipping");
                return;
            }

            _logger.Log("🔄 Manual GitHub scripts refresh requested");
            await RefreshGitHubScriptsInternalAsync();
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Internal async initialization - runs in background thread
        /// </summary>
        private async Task InitializeAsync(string localScriptsPath)
        {
            try
            {
                _logger.Log("🎯 ScriptService async initialization starting");

                // 1. Load local scripts first (fast, synchronous)
                LoadLocalScripts(localScriptsPath);

                // 2. Load initial GitHub scripts (cached or bundled, also fast)
                LoadInitialGitHubScripts();

                // 3. Fire the first update so the UI is populated instantly
                IsUpdating = true;
                NotifyLocalScriptsUpdated();
                NotifyGitHubScriptsUpdated();

                // 4. Asynchronously fetch latest from GitHub in the background
                await RefreshGitHubScriptsInternalAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("ScriptService initialization failed", ex);
                UpdateStatus = "Initialization failed";
                IsUpdating = false;
            }
        }

        /// <summary>
        /// Load local scripts from disk (synchronous, fast)
        /// </summary>
        private void LoadLocalScripts(string localScriptsPath)
        {
            try
            {
                var localScripts = new List<ScriptViewModel>();
                
                if (!string.IsNullOrEmpty(localScriptsPath) && System.IO.Directory.Exists(localScriptsPath))
                {
                    var scriptFiles = System.IO.Directory.GetFiles(localScriptsPath, "*.py", System.IO.SearchOption.AllDirectories)
                        .Concat(System.IO.Directory.GetFiles(localScriptsPath, "*.cs", System.IO.SearchOption.AllDirectories))
                        .ToArray();

                    foreach (var scriptFile in scriptFiles)
                    {
                        var metadata = _scriptParser.ParseMetadata(scriptFile);
                        localScripts.Add(new ScriptViewModel
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = metadata.Name,
                            Description = metadata.Description,
                            Command = scriptFile
                        });
                    }
                }

                lock (_localScripts)
                {
                    _localScripts = localScripts;
                }

                _logger.Log($"🎯 Loaded {localScripts.Count} local scripts");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load local scripts", ex);
            }
        }

        /// <summary>
        /// Load initial GitHub scripts from cache or bundled fallback (synchronous, fast)
        /// </summary>
        private void LoadInitialGitHubScripts()
        {
            try
            {
                var githubScripts = new List<ScriptViewModel>();

                // Try to load from cache first
                var cachedScriptsPath = _gitCacheManager?.GetCachedScriptsPath();
                if (!string.IsNullOrEmpty(cachedScriptsPath) && System.IO.Directory.Exists(cachedScriptsPath))
                {
                    githubScripts = LoadGitHubScriptsFromPath(cachedScriptsPath);
                    _logger.Log($"🎯 Loaded {githubScripts.Count} GitHub scripts from cache");
                }
                else
                {
                    // No cached scripts available - will need to download from GitHub
                    _logger.Log("🎯 No cached scripts found - GitHub download required");
                }

                lock (_githubScripts)
                {
                    _githubScripts = githubScripts;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load initial GitHub scripts", ex);
                
                // No fallback scripts available - user will need to download from GitHub
                _logger.Log("🎯 No scripts available - GitHub download required");
            }
        }

        /// <summary>
        /// Load GitHub scripts from a specific path
        /// </summary>
        private List<ScriptViewModel> LoadGitHubScriptsFromPath(string scriptsPath)
        {
            var scripts = new List<ScriptViewModel>();
            
            var scriptFiles = System.IO.Directory.GetFiles(scriptsPath, "*.py", System.IO.SearchOption.AllDirectories)
                .Concat(System.IO.Directory.GetFiles(scriptsPath, "*.cs", System.IO.SearchOption.AllDirectories))
                .ToArray();

            foreach (var scriptFile in scriptFiles)
            {
                var fileName = System.IO.Path.GetFileNameWithoutExtension(scriptFile);
                var relativePath = scriptFile.Substring(scriptsPath.Length).TrimStart('\\', '/');

                scripts.Add(new ScriptViewModel
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = fileName,
                    Description = $"GitHub script: {relativePath}",
                    Command = scriptFile
                });
            }

            return scripts;
        }



        /// <summary>
        /// Refresh GitHub scripts from remote (async, background operation)
        /// </summary>
        private async Task RefreshGitHubScriptsInternalAsync()
        {
            try
            {
                IsUpdating = true;
                UpdateStatus = "Checking for updates...";

                if (_gitCacheManager == null)
                {
                    UpdateStatus = "GitHub integration not available";
                    return;
                }

                // Attempt to refresh cache from GitHub
                var success = await _gitCacheManager.RefreshCacheAsync(forceRefresh: false);

                if (success)
                {
                    // Reload scripts from updated cache
                    var cachedScriptsPath = _gitCacheManager.GetCachedScriptsPath();
                    if (!string.IsNullOrEmpty(cachedScriptsPath) && System.IO.Directory.Exists(cachedScriptsPath))
                    {
                        var freshScripts = LoadGitHubScriptsFromPath(cachedScriptsPath);

                        lock (_githubScripts)
                        {
                            _githubScripts = freshScripts;
                        }

                        _lastUpdated = DateTime.Now;
                        UpdateStatus = $"Updated successfully at {_lastUpdated:HH:mm}";
                        _logger.Log($"🎯 GitHub scripts refreshed: {freshScripts.Count} scripts loaded");
                    }
                    else
                    {
                        UpdateStatus = "Update completed but no scripts found";
                        _logger.LogWarning("GitHub cache refresh succeeded but no scripts path found");
                    }
                }
                else
                {
                    UpdateStatus = "Update failed - using cached scripts";
                    _logger.LogWarning("GitHub cache refresh failed, keeping existing scripts");
                }

                // Notify UI of updated scripts
                NotifyGitHubScriptsUpdated();
            }
            catch (Exception ex)
            {
                _logger.LogError("GitHub scripts refresh failed", ex);
                UpdateStatus = "Update failed - check connection";
            }
            finally
            {
                IsUpdating = false;
            }
        }

        /// <summary>
        /// Notify UI thread of local scripts update (thread-safe)
        /// </summary>
        private void NotifyLocalScriptsUpdated()
        {
            try
            {
                var scripts = GetCurrentLocalScripts();

                // Marshal to UI thread if needed
                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        LocalScriptsUpdated?.Invoke(scripts);
                    }));
                }
                else
                {
                    LocalScriptsUpdated?.Invoke(scripts);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to notify local scripts updated", ex);
            }
        }

        /// <summary>
        /// Notify UI thread of GitHub scripts update (thread-safe)
        /// </summary>
        private void NotifyGitHubScriptsUpdated()
        {
            try
            {
                var scripts = GetCurrentGitHubScripts();

                // Marshal to UI thread if needed
                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        GitHubScriptsUpdated?.Invoke(scripts);
                    }));
                }
                else
                {
                    GitHubScriptsUpdated?.Invoke(scripts);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to notify GitHub scripts updated", ex);
            }
        }

        /// <summary>
        /// Notify UI thread of update status change (thread-safe)
        /// </summary>
        private void NotifyUpdateStatusChanged(string status)
        {
            try
            {
                // Marshal to UI thread if needed
                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateStatusChanged?.Invoke(status);
                    }));
                }
                else
                {
                    UpdateStatusChanged?.Invoke(status);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to notify update status changed", ex);
            }
        }

        /// <summary>
        /// Notify UI thread of updating state change (thread-safe)
        /// </summary>
        private void NotifyIsUpdatingChanged(bool isUpdating)
        {
            try
            {
                // Marshal to UI thread if needed
                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        IsUpdatingChanged?.Invoke(isUpdating);
                    }));
                }
                else
                {
                    IsUpdatingChanged?.Invoke(isUpdating);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to notify updating state changed", ex);
            }
        }
        #endregion
    }
}
