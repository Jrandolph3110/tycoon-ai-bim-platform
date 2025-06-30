using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.Services
{
    /// <summary>
    /// 📋 Version Tracking System for GitHub Scripts
    /// Maintains detailed version history, rollback capabilities, and troubleshooting metadata
    /// Creates .meta files for each cached version with commit SHA, timestamps, and change tracking
    /// </summary>
    public class VersionTracker
    {
        private readonly Logger _logger;
        private readonly string _cacheBasePath;
        private readonly string _versionHistoryPath;
        private readonly string _rollbackPath;

        public VersionTracker(Logger logger, string cacheBasePath)
        {
            _logger = logger;
            _cacheBasePath = cacheBasePath;
            _versionHistoryPath = Path.Combine(_cacheBasePath, "version-history.json");
            _rollbackPath = Path.Combine(_cacheBasePath, "rollback");
            
            Directory.CreateDirectory(_rollbackPath);
        }

        /// <summary>
        /// Create version metadata for a cache directory
        /// </summary>
        public void CreateVersionMetadata(string cacheDir, string commitSha, ScriptManifest manifest, DownloadResult downloadResult)
        {
            try
            {
                var versionInfo = new VersionMetadata
                {
                    CommitSha = commitSha,
                    CacheDirectory = Path.GetFileName(cacheDir),
                    Created = DateTime.UtcNow,
                    ManifestVersion = manifest.Version,
                    ManifestBuild = manifest.Build,
                    ScriptCount = manifest.Scripts?.Count ?? 0,
                    TemplateCount = manifest.Templates?.Count ?? 0,
                    DownloadStats = downloadResult,
                    ScriptVersions = manifest.Scripts?.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new ScriptVersionInfo
                        {
                            Hash = kvp.Value.Hash,
                            Version = kvp.Value.Version,
                            LastModified = kvp.Value.LastModified,
                            Size = kvp.Value.Size,
                            Path = kvp.Value.Path
                        }
                    ) ?? new Dictionary<string, ScriptVersionInfo>()
                };

                // Save version metadata file
                var metaPath = Path.Combine(cacheDir, ".meta");
                var json = JsonConvert.SerializeObject(versionInfo, Formatting.Indented);
                File.WriteAllText(metaPath, json);

                // Update version history
                UpdateVersionHistory(versionInfo);

                _logger.Log($"📋 Version metadata created: {commitSha} ({versionInfo.ScriptCount} scripts)");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create version metadata", ex);
            }
        }

        /// <summary>
        /// Update the global version history file
        /// </summary>
        private void UpdateVersionHistory(VersionMetadata versionInfo)
        {
            try
            {
                var history = LoadVersionHistory();
                
                // Add new version to history
                history.Versions.Add(versionInfo);
                
                // Keep only last 10 versions to prevent bloat
                if (history.Versions.Count > 10)
                {
                    var oldVersions = history.Versions.OrderBy(v => v.Created).Take(history.Versions.Count - 10).ToList();
                    foreach (var oldVersion in oldVersions)
                    {
                        history.Versions.Remove(oldVersion);
                        
                        // Clean up old cache directories
                        var oldCacheDir = Path.Combine(_cacheBasePath, oldVersion.CacheDirectory);
                        if (Directory.Exists(oldCacheDir))
                        {
                            try
                            {
                                Directory.Delete(oldCacheDir, true);
                                _logger.Log($"🗑️ Cleaned up old cache: {oldVersion.CacheDirectory}");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning($"Failed to clean up old cache {oldVersion.CacheDirectory}: {ex.Message}");
                            }
                        }
                    }
                }
                
                // Update current version pointer
                history.CurrentVersion = versionInfo.CommitSha;
                history.LastUpdated = DateTime.UtcNow;
                
                // Save updated history
                var json = JsonConvert.SerializeObject(history, Formatting.Indented);
                File.WriteAllText(_versionHistoryPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to update version history", ex);
            }
        }

        /// <summary>
        /// Load version history from disk
        /// </summary>
        public VersionHistory LoadVersionHistory()
        {
            try
            {
                if (!File.Exists(_versionHistoryPath))
                {
                    return new VersionHistory();
                }

                var json = File.ReadAllText(_versionHistoryPath);
                return JsonConvert.DeserializeObject<VersionHistory>(json) ?? new VersionHistory();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to load version history: {ex.Message}");
                return new VersionHistory();
            }
        }

        /// <summary>
        /// Get metadata for a specific cache directory
        /// </summary>
        public VersionMetadata GetVersionMetadata(string cacheDir)
        {
            try
            {
                var metaPath = Path.Combine(cacheDir, ".meta");
                if (!File.Exists(metaPath)) return null;

                var json = File.ReadAllText(metaPath);
                return JsonConvert.DeserializeObject<VersionMetadata>(json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to load version metadata for {cacheDir}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Create rollback point before major changes
        /// </summary>
        public string CreateRollbackPoint(string description = null)
        {
            try
            {
                var rollbackId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                var rollbackDir = Path.Combine(_rollbackPath, rollbackId);
                Directory.CreateDirectory(rollbackDir);

                // Copy current cache state
                var currentCacheDir = GetCurrentCacheDirectory();
                if (!string.IsNullOrEmpty(currentCacheDir) && Directory.Exists(currentCacheDir))
                {
                    CopyDirectory(currentCacheDir, rollbackDir);
                }

                // Create rollback metadata
                var rollbackInfo = new RollbackPoint
                {
                    Id = rollbackId,
                    Created = DateTime.UtcNow,
                    Description = description ?? "Automatic rollback point",
                    SourceCacheDirectory = Path.GetFileName(currentCacheDir)
                };

                var rollbackMetaPath = Path.Combine(rollbackDir, ".rollback");
                var json = JsonConvert.SerializeObject(rollbackInfo, Formatting.Indented);
                File.WriteAllText(rollbackMetaPath, json);

                _logger.Log($"💾 Rollback point created: {rollbackId}");
                return rollbackId;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create rollback point", ex);
                return null;
            }
        }

        /// <summary>
        /// Restore from rollback point
        /// </summary>
        public bool RestoreFromRollback(string rollbackId)
        {
            try
            {
                var rollbackDir = Path.Combine(_rollbackPath, rollbackId);
                if (!Directory.Exists(rollbackDir))
                {
                    _logger.LogWarning($"Rollback point not found: {rollbackId}");
                    return false;
                }

                // Create new cache directory for restored state
                var restoreDir = Path.Combine(_cacheBasePath, $"restored-{rollbackId}");
                if (Directory.Exists(restoreDir))
                {
                    Directory.Delete(restoreDir, true);
                }

                CopyDirectory(rollbackDir, restoreDir);

                _logger.Log($"🔄 Restored from rollback: {rollbackId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to restore from rollback {rollbackId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Get current cache directory (most recent)
        /// </summary>
        private string GetCurrentCacheDirectory()
        {
            try
            {
                if (!Directory.Exists(_cacheBasePath)) return null;

                var cacheDirs = Directory.GetDirectories(_cacheBasePath)
                    .Where(d => !Path.GetFileName(d).Equals("rollback", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(d => Directory.GetCreationTime(d))
                    .ToArray();

                return cacheDirs.Length > 0 ? cacheDirs[0] : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Copy directory recursively
        /// </summary>
        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            // Copy files
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            // Copy subdirectories
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }

        /// <summary>
        /// Get troubleshooting information for current cache state
        /// </summary>
        public TroubleshootingInfo GetTroubleshootingInfo()
        {
            try
            {
                var info = new TroubleshootingInfo
                {
                    CacheBasePath = _cacheBasePath,
                    CurrentTime = DateTime.UtcNow,
                    VersionHistory = LoadVersionHistory(),
                    CacheDirectories = new List<CacheDirectoryInfo>()
                };

                if (Directory.Exists(_cacheBasePath))
                {
                    foreach (var cacheDir in Directory.GetDirectories(_cacheBasePath))
                    {
                        if (Path.GetFileName(cacheDir).Equals("rollback", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var dirInfo = new CacheDirectoryInfo
                        {
                            Name = Path.GetFileName(cacheDir),
                            Created = Directory.GetCreationTime(cacheDir),
                            Size = GetDirectorySize(cacheDir),
                            Metadata = GetVersionMetadata(cacheDir)
                        };

                        info.CacheDirectories.Add(dirInfo);
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to get troubleshooting info", ex);
                return new TroubleshootingInfo { Error = ex.Message };
            }
        }

        /// <summary>
        /// Get directory size in bytes
        /// </summary>
        private long GetDirectorySize(string dirPath)
        {
            try
            {
                return Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories)
                    .Sum(file => new FileInfo(file).Length);
            }
            catch
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Version metadata for a cache directory
    /// </summary>
    public class VersionMetadata
    {
        public string CommitSha { get; set; }
        public string CacheDirectory { get; set; }
        public DateTime Created { get; set; }
        public string ManifestVersion { get; set; }
        public string ManifestBuild { get; set; }
        public int ScriptCount { get; set; }
        public int TemplateCount { get; set; }
        public DownloadResult DownloadStats { get; set; }
        public Dictionary<string, ScriptVersionInfo> ScriptVersions { get; set; } = new Dictionary<string, ScriptVersionInfo>();
    }

    /// <summary>
    /// Individual script version information
    /// </summary>
    public class ScriptVersionInfo
    {
        public string Hash { get; set; }
        public string Version { get; set; }
        public DateTime LastModified { get; set; }
        public long Size { get; set; }
        public string Path { get; set; }
    }

    /// <summary>
    /// Version history tracking
    /// </summary>
    public class VersionHistory
    {
        public string CurrentVersion { get; set; }
        public DateTime LastUpdated { get; set; }
        public List<VersionMetadata> Versions { get; set; } = new List<VersionMetadata>();
    }

    /// <summary>
    /// Rollback point information
    /// </summary>
    public class RollbackPoint
    {
        public string Id { get; set; }
        public DateTime Created { get; set; }
        public string Description { get; set; }
        public string SourceCacheDirectory { get; set; }
    }

    /// <summary>
    /// Troubleshooting information
    /// </summary>
    public class TroubleshootingInfo
    {
        public string CacheBasePath { get; set; }
        public DateTime CurrentTime { get; set; }
        public VersionHistory VersionHistory { get; set; }
        public List<CacheDirectoryInfo> CacheDirectories { get; set; } = new List<CacheDirectoryInfo>();
        public string Error { get; set; }
    }

    /// <summary>
    /// Cache directory information
    /// </summary>
    public class CacheDirectoryInfo
    {
        public string Name { get; set; }
        public DateTime Created { get; set; }
        public long Size { get; set; }
        public VersionMetadata Metadata { get; set; }
    }
}
