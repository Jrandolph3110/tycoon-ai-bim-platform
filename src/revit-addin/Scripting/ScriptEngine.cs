using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using TycoonRevitAddin.Utils;
using Tycoon.Scripting.Contracts;

namespace TycoonRevitAddin.Scripting
{
    /// <summary>
    /// Operating modes for the ScriptEngine
    /// </summary>
    public enum ScriptEngineMode
    {
        Production,   // Load scripts from GitHub repository
        Development   // Load scripts from local filesystem with hot-reload
    }

    /// <summary>
    /// Unified Script Engine - Single source of truth for all script management
    /// Supports both Production (GitHub) and Development (local hot-reload) modes
    /// </summary>
    public class ScriptEngine
    {
        private readonly Logger _logger;
        private readonly object _lock = new object();
        
        // Core state
        private ScriptEngineMode _mode = ScriptEngineMode.Production;
        private IScriptProvider _currentProvider;
        private List<ScriptInfo> _loadedScripts = new List<ScriptInfo>();
        
        // AppDomain management
        private AppDomain _scriptDomain;
        private ScriptProxy _scriptProxy;
        
        // Events
        public event Action<List<ScriptInfo>> ScriptsChanged;
        
        public ScriptEngine(Logger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.Log("🎯 ScriptEngine initializing with unified architecture");
        }

        /// <summary>
        /// Initialize the ScriptEngine with the specified mode
        /// </summary>
        public async Task InitializeAsync(ScriptEngineMode mode, ScriptEngineConfig config)
        {
            lock (_lock)
            {
                _mode = mode;
                _logger.Log($"🎯 ScriptEngine initializing in {mode} mode");
                
                // Create appropriate provider based on mode
                _currentProvider = mode switch
                {
                    ScriptEngineMode.Development => new LocalScriptProvider(_logger, config.DevelopmentPath),
                    ScriptEngineMode.Production => new GitHubScriptProvider(_logger, config.GitHubConfig),
                    _ => throw new ArgumentException($"Unsupported mode: {mode}")
                };
                
                // Subscribe to provider events
                _currentProvider.ScriptsChanged += OnProviderScriptsChanged;
            }
            
            // Initialize provider (async)
            await _currentProvider.InitializeAsync();
            
            _logger.Log($"✅ ScriptEngine initialized in {mode} mode");
        }

        /// <summary>
        /// Get all currently loaded scripts (thread-safe)
        /// </summary>
        public List<ScriptInfo> GetLoadedScripts()
        {
            lock (_lock)
            {
                return _loadedScripts.ToList(); // Return copy
            }
        }

        /// <summary>
        /// Execute a script by name in isolated AppDomain
        /// </summary>
        public async Task<ScriptExecutionResult> ExecuteScriptAsync(string scriptName, UIApplication uiApp = null, Document doc = null)
        {
            try
            {
                _logger.Log($"🚀 Executing script: {scriptName}");

                // Find script
                ScriptInfo script;
                lock (_lock)
                {
                    script = _loadedScripts.FirstOrDefault(s => s.Manifest.Name == scriptName);
                }

                if (script == null)
                {
                    return new ScriptExecutionResult
                    {
                        Success = false,
                        ErrorMessage = $"Script '{scriptName}' not found"
                    };
                }

                // Execute in isolated AppDomain with Revit context
                return await ExecuteScriptInAppDomain(script, uiApp, doc);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to execute script '{scriptName}'", ex);
                return new ScriptExecutionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Manually refresh scripts (useful for Development mode)
        /// </summary>
        public async Task RefreshScriptsAsync()
        {
            _logger.Log("🔄 Manual script refresh requested");
            await _currentProvider.RefreshAsync();
        }

        /// <summary>
        /// Switch between Development and Production modes
        /// </summary>
        public async Task SwitchModeAsync(ScriptEngineMode newMode, ScriptEngineConfig config)
        {
            if (_mode == newMode)
            {
                _logger.Log($"Already in {newMode} mode, skipping switch");
                return;
            }
            
            _logger.Log($"🔄 Switching from {_mode} to {newMode} mode");
            
            // Cleanup current provider
            if (_currentProvider != null)
            {
                _currentProvider.ScriptsChanged -= OnProviderScriptsChanged;
                _currentProvider.Dispose();
            }
            
            // Cleanup AppDomain
            UnloadScriptDomain();
            
            // Initialize with new mode
            await InitializeAsync(newMode, config);
        }

        /// <summary>
        /// Handle script changes from provider
        /// </summary>
        private void OnProviderScriptsChanged(List<ScriptInfo> scripts)
        {
            lock (_lock)
            {
                _loadedScripts = scripts.ToList();
                _logger.Log($"📜 Scripts updated: {scripts.Count} scripts loaded");
            }
            
            // Unload old AppDomain when scripts change (for hot-reload)
            UnloadScriptDomain();
            
            // Notify listeners
            ScriptsChanged?.Invoke(scripts.ToList());
        }

        /// <summary>
        /// Execute script in isolated AppDomain with transaction management
        /// </summary>
        private async Task<ScriptExecutionResult> ExecuteScriptInAppDomain(ScriptInfo script, UIApplication uiApp = null, Document doc = null)
        {
            try
            {
                // Ensure we have a fresh AppDomain and proxy
                EnsureScriptDomain();

                // Initialize proxy with Revit context if provided
                if (uiApp != null && doc != null)
                {
                    _scriptProxy.Initialize(uiApp, doc);
                }

                // Execute script through proxy (with automatic transaction management)
                return await Task.Run(() => _scriptProxy.ExecuteScript(script));
            }
            catch (Exception ex)
            {
                _logger.LogError($"AppDomain execution failed for script '{script.Manifest.Name}'", ex);
                return new ScriptExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"Execution failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Ensure we have a valid AppDomain and proxy
        /// </summary>
        private void EnsureScriptDomain()
        {
            if (_scriptDomain == null || _scriptProxy == null)
            {
                CreateScriptDomain();
            }
        }

        /// <summary>
        /// Create new isolated AppDomain for script execution
        /// </summary>
        private void CreateScriptDomain()
        {
            _logger.Log("🏗️ Creating new script AppDomain");
            
            // Create AppDomain with proper security and setup
            var domainSetup = new AppDomainSetup
            {
                ApplicationBase = AppDomain.CurrentDomain.BaseDirectory,
                ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile
            };
            
            _scriptDomain = AppDomain.CreateDomain("TycoonScriptDomain", null, domainSetup);
            
            // Create proxy in the new domain (no parameters to avoid serialization issues)
            var proxyType = typeof(ScriptProxy);
            _scriptProxy = (ScriptProxy)_scriptDomain.CreateInstanceAndUnwrap(
                proxyType.Assembly.FullName,
                proxyType.FullName);
            
            _logger.Log("✅ Script AppDomain created with proxy");
        }

        /// <summary>
        /// Unload the script AppDomain (for hot-reload)
        /// </summary>
        private void UnloadScriptDomain()
        {
            if (_scriptDomain != null)
            {
                _logger.Log("🗑️ Unloading script AppDomain for hot-reload");
                
                try
                {
                    AppDomain.Unload(_scriptDomain);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to unload script AppDomain", ex);
                }
                finally
                {
                    _scriptDomain = null;
                    _scriptProxy = null;
                }
            }
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (_currentProvider != null)
            {
                _currentProvider.ScriptsChanged -= OnProviderScriptsChanged;
                _currentProvider.Dispose();
            }
            UnloadScriptDomain();
            
            _logger.Log("🎯 ScriptEngine disposed");
        }
    }
}
