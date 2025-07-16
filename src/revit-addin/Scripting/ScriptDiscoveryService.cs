using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TycoonRevitAddin.Utils;
using TycoonRevitAddin.Services;

namespace TycoonRevitAddin.Scripting
{
    /// <summary>
    /// Enhanced script discovery service supporting dual sources:
    /// - Local scripts (for development/hot-reload)
    /// - GitHub scripts (for verified production distribution)
    /// </summary>
    public class ScriptDiscoveryService
    {
        private readonly ILogger _logger;
        private readonly GitCacheManager _gitCacheManager;

        public ScriptDiscoveryService(ILogger logger = null)
        {
            _logger = logger;
            _gitCacheManager = new GitCacheManager(logger as Logger ?? new Logger("ScriptDiscovery"));
        }

        /// <summary>
        /// Discover all scripts in the specified directory
        /// </summary>
        public List<ScriptDefinition> DiscoverScripts(string baseDirectory)
        {
            var scripts = new List<ScriptDefinition>();

            try
            {
                _logger?.Log($"🔍 Discovering scripts in: {baseDirectory}");

                // Check if directory exists
                if (!Directory.Exists(baseDirectory))
                {
                    _logger?.Log($"⚠️ Script directory does not exist: {baseDirectory}");
                    return scripts;
                }

                // Look for *.json files in subdirectories, excluding build output directories
                var scriptJsonFiles = Directory.GetFiles(baseDirectory, "*.json", SearchOption.AllDirectories)
                    .Where(file => !file.Contains("\\bin\\") && !file.Contains("\\obj\\") && !file.Contains("\\Debug\\") && !file.Contains("\\Release\\"))
                    .ToArray();

                _logger?.Log($"🔍 Found {scriptJsonFiles.Length} JSON script files");

                foreach (var jsonFile in scriptJsonFiles)
                {
                    try
                    {
                        var script = ScriptDefinition.FromJsonFile(jsonFile);
                        scripts.Add(script);
                        _logger?.Log($"✅ Loaded script: {script.Name} (Panel: {script.Panel}, Stack: {script.Stack ?? "None"})");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"❌ Failed to load script from {jsonFile}", ex);
                    }
                }
                
                _logger?.Log($"🎯 Script discovery complete: {scripts.Count} scripts loaded");
                return scripts;
            }
            catch (Exception ex)
            {
                _logger?.Log($"⚠️ Script discovery failed in {baseDirectory}: {ex.Message}");
                return new List<ScriptDefinition>();
            }
        }

        /// <summary>
        /// Get the default script directory for the current mode
        /// </summary>
        public static string GetDefaultScriptDirectory()
        {
            // Try multiple locations for test-scripts directory
            var searchPaths = new[]
            {
                // Direct platform path
                @"C:\RevitAI\tycoon-ai-bim-platform\test-scripts",
                // Relative to assembly location
                Path.GetFullPath(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "..", "..", "..", "..", "test-scripts")),
                // Current directory
                Path.Combine(Directory.GetCurrentDirectory(), "test-scripts"),
                // User documents
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Tycoon", "Scripts")
            };

            foreach (var path in searchPaths)
            {
                if (Directory.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }

            // Create roaming directory as fallback
            var roamingPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tycoon", "Scripts");
            try
            {
                Directory.CreateDirectory(roamingPath);
            }
            catch
            {
                // Ignore creation errors
            }
            return roamingPath;
        }

        /// <summary>
        /// Discover scripts from both local and GitHub sources
        /// </summary>
        public List<ScriptDefinition> DiscoverAllScripts()
        {
            var allScripts = new List<ScriptDefinition>();

            try
            {
                _logger?.Log("🔍 STARTUP: Discovering scripts from all sources...");

                // 1. Discover local scripts
                var localScripts = DiscoverLocalScripts();
                allScripts.AddRange(localScripts);
                _logger?.Log($"🔍 STARTUP: Found {localScripts.Count} local scripts");

                // 2. Discover GitHub scripts
                var githubScripts = DiscoverGitHubScripts();
                allScripts.AddRange(githubScripts);
                _logger?.Log($"🔍 STARTUP: Found {githubScripts.Count} GitHub scripts");

                _logger?.Log($"✅ STARTUP: Total scripts discovered: {allScripts.Count} (Local: {localScripts.Count}, GitHub: {githubScripts.Count})");
            }
            catch (Exception ex)
            {
                _logger?.LogError("STARTUP: Failed to discover all scripts", ex);
            }

            return allScripts;
        }

        /// <summary>
        /// Discover local scripts for development/hot-reload
        /// </summary>
        public List<ScriptDefinition> DiscoverLocalScripts()
        {
            var localDirectory = GetDefaultScriptDirectory();
            var scripts = DiscoverScripts(localDirectory);

            // Mark as local source
            foreach (var script in scripts)
            {
                script.Source = "Local";
                script.SourcePath = localDirectory;
            }

            _logger?.Log($"🔍 Local scripts: {scripts.Count} from {localDirectory}");
            return scripts;
        }

        /// <summary>
        /// Discover GitHub scripts from cache
        /// </summary>
        public List<ScriptDefinition> DiscoverGitHubScripts()
        {
            var scripts = new List<ScriptDefinition>();

            try
            {
                _logger?.Log("🔍 STARTUP: Attempting to discover GitHub scripts from cache...");
                var cachedScriptsPath = _gitCacheManager.GetCachedScriptsPath();
                _logger?.Log($"🔍 STARTUP: Cached scripts path: {cachedScriptsPath ?? "NULL"}");

                if (!string.IsNullOrEmpty(cachedScriptsPath) && Directory.Exists(cachedScriptsPath))
                {
                    _logger?.Log($"🔍 STARTUP: Cache directory exists, discovering scripts...");
                    scripts = DiscoverScripts(cachedScriptsPath);

                    // Mark as GitHub source
                    foreach (var script in scripts)
                    {
                        script.Source = "GitHub";
                        script.SourcePath = cachedScriptsPath;
                        _logger?.Log($"🔍 STARTUP: Marked script as GitHub source: {script.Name}");
                    }

                    _logger?.Log($"🔍 STARTUP: GitHub scripts: {scripts.Count} from cache");
                }
                else
                {
                    _logger?.Log("🔍 STARTUP: No GitHub scripts cached - download required");
                    if (string.IsNullOrEmpty(cachedScriptsPath))
                    {
                        _logger?.Log("🔍 STARTUP: Cached scripts path is null or empty");
                    }
                    else if (!Directory.Exists(cachedScriptsPath))
                    {
                        _logger?.Log($"🔍 STARTUP: Cache directory does not exist: {cachedScriptsPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("STARTUP: Failed to discover GitHub scripts", ex);
            }

            return scripts;
        }

        /// <summary>
        /// Refresh GitHub scripts cache
        /// </summary>
        public async System.Threading.Tasks.Task<bool> RefreshGitHubScriptsAsync()
        {
            try
            {
                _logger?.Log("🔄 Refreshing GitHub scripts cache...");
                var success = await _gitCacheManager.RefreshCacheAsync(forceRefresh: true);

                if (success)
                {
                    _logger?.Log("✅ GitHub scripts cache refreshed successfully");
                }
                else
                {
                    _logger?.Log("❌ Failed to refresh GitHub scripts cache");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger?.LogError("Failed to refresh GitHub scripts", ex);
                return false;
            }
        }

        /// <summary>
        /// Get GitHub cache manager for advanced operations
        /// </summary>
        public GitCacheManager GetGitCacheManager()
        {
            return _gitCacheManager;
        }
    }
}
