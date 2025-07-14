using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tycoon.Scripting.Contracts;

namespace TycoonRevitAddin.Scripting
{
    /// <summary>
    /// Configuration for ScriptEngine initialization
    /// </summary>
    public class ScriptEngineConfig
    {
        /// <summary>
        /// Path to local scripts directory (for Development mode)
        /// </summary>
        public string DevelopmentPath { get; set; }
        
        /// <summary>
        /// GitHub configuration (for Production mode)
        /// </summary>
        public GitHubScriptConfig GitHubConfig { get; set; }
    }

    /// <summary>
    /// GitHub script provider configuration
    /// </summary>
    public class GitHubScriptConfig
    {
        public string RepositoryUrl { get; set; }
        public string Branch { get; set; } = "main";
        public string CachePath { get; set; }
        public TimeSpan CacheExpiry { get; set; } = TimeSpan.FromHours(24);
    }

    /// <summary>
    /// Information about a discovered script
    /// </summary>
    public class ScriptInfo
    {
        /// <summary>
        /// Script metadata from manifest
        /// </summary>
        public ScriptManifest Manifest { get; set; }
        
        /// <summary>
        /// Full path to the script assembly
        /// </summary>
        public string AssemblyPath { get; set; }
        
        /// <summary>
        /// Full path to the manifest file
        /// </summary>
        public string ManifestPath { get; set; }
        
        /// <summary>
        /// Directory containing the script
        /// </summary>
        public string ScriptDirectory { get; set; }
        
        /// <summary>
        /// Last modified time (for change detection)
        /// </summary>
        public DateTime LastModified { get; set; }
        
        /// <summary>
        /// Whether this script is from Development or Production source
        /// </summary>
        public ScriptEngineMode Source { get; set; }
    }

    /// <summary>
    /// Result of script execution
    /// </summary>
    public class ScriptExecutionResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public Dictionary<string, object> OutputData { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Interface for script providers (Development/Production)
    /// </summary>
    public interface IScriptProvider : IDisposable
    {
        /// <summary>
        /// Event fired when scripts change
        /// </summary>
        event Action<List<ScriptInfo>> ScriptsChanged;
        
        /// <summary>
        /// Initialize the provider
        /// </summary>
        Task InitializeAsync();
        
        /// <summary>
        /// Manually refresh scripts
        /// </summary>
        Task RefreshAsync();
        
        /// <summary>
        /// Get current scripts
        /// </summary>
        List<ScriptInfo> GetCurrentScripts();
    }

    /// <summary>
    /// Script discovery result
    /// </summary>
    public class ScriptDiscoveryResult
    {
        public List<ScriptInfo> Scripts { get; set; } = new List<ScriptInfo>();
        public List<string> Errors { get; set; } = new List<string>();
        public DateTime DiscoveryTime { get; set; } = DateTime.Now;
    }
}
