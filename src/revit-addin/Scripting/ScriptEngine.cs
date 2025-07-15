using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
        
        // Note: AppDomain isolation removed for full Revit API access
        
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
        /// Execute a script by name with full Revit API access
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

                // Execute with full Revit API access
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
            
            // Note: No AppDomain cleanup needed - scripts run in main AppDomain
            
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
            
            // Note: No AppDomain unloading needed - scripts run in main AppDomain
            
            // Notify listeners
            ScriptsChanged?.Invoke(scripts.ToList());
        }

        /// <summary>
        /// Execute script directly in main AppDomain with full Revit API access
        /// </summary>
        private async Task<ScriptExecutionResult> ExecuteScriptInAppDomain(ScriptInfo script, UIApplication uiApp = null, Document doc = null)
        {
            try
            {
                _logger.Log($"🚀 Executing script '{script.Manifest.Name}' in main AppDomain with full Revit API access");

                // Execute script directly in main AppDomain for full Revit functionality
                return await Task.Run(() => ExecuteScriptDirectly(script, uiApp, doc));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Script execution failed for script '{script.Manifest.Name}'", ex);
                return new ScriptExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"Execution failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Execute script directly in main AppDomain with full Revit API access
        /// </summary>
        private ScriptExecutionResult ExecuteScriptDirectly(ScriptInfo script, UIApplication uiApp, Document doc)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.Log($"🔧 Loading script assembly: {script.AssemblyPath}");

                // Load script assembly
                var assembly = Assembly.LoadFrom(script.AssemblyPath);
                var scriptType = assembly.GetType(script.Manifest.EntryType);

                if (scriptType == null)
                {
                    throw new InvalidOperationException($"Script type '{script.Manifest.EntryType}' not found in assembly");
                }

                // Create script instance
                var scriptInstance = Activator.CreateInstance(scriptType) as IScript;
                if (scriptInstance == null)
                {
                    throw new InvalidOperationException($"Script type '{script.Manifest.EntryType}' does not implement IScript");
                }

                // Create host with full Revit context
                var host = new DirectRevitHost(uiApp, doc, _logger);

                // Execute with transaction management
                ExecuteScriptWithTransaction(scriptInstance, host, script.Manifest.Name, doc);

                stopwatch.Stop();
                _logger.Log($"✅ Script '{script.Manifest.Name}' executed successfully in {stopwatch.ElapsedMilliseconds}ms");

                return new ScriptExecutionResult
                {
                    Success = true,
                    ExecutionTime = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError($"❌ Script '{script.Manifest.Name}' failed", ex);

                return new ScriptExecutionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ExecutionTime = stopwatch.Elapsed
                };
            }
        }

        /// <summary>
        /// Execute script with automatic transaction management in main AppDomain
        /// </summary>
        private void ExecuteScriptWithTransaction(IScript script, IRevitHost host, string scriptName, Document document)
        {
            // Check if we have Revit context
            if (document == null)
            {
                _logger.Log($"⚠️ No Revit document context - executing script without transaction management");
                script.Execute(host);
                return;
            }

            using (var transaction = new Transaction(document, $"Tycoon Script: {scriptName}"))
            {
                try
                {
                    transaction.Start();
                    _logger.Log($"🔄 Transaction started for script: {scriptName}");

                    // Execute script - any exceptions will cause rollback
                    script.Execute(host);

                    transaction.Commit();
                    _logger.Log($"✅ Transaction committed for script: {scriptName}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Script execution failed, rolling back transaction", ex);

                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        transaction.RollBack();
                        _logger.Log($"🔄 Transaction rolled back for script: {scriptName}");
                    }

                    throw; // Re-throw to be handled by caller
                }
            }
        }

        // Note: AppDomain methods removed - scripts now execute directly in main AppDomain for full Revit API access

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
            // Note: No AppDomain cleanup needed - scripts run in main AppDomain
            
            _logger.Log("🎯 ScriptEngine disposed");
        }
    }
}
