using System;
using System.IO;
using System.Linq;
using TycoonRevitAddin.Plugins;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.Services
{
    /// <summary>
    /// 🎯 ScriptParser - Centralized Script Metadata Parsing
    /// Extracts metadata from script headers using consistent logic
    /// Shared between local scripts, GitHub scripts, and bundled scripts
    /// </summary>
    public class ScriptParser
    {
        private readonly Logger _logger;

        public ScriptParser(Logger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Parse metadata from script content (string)
        /// </summary>
        public ScriptMetadata ParseMetadata(string scriptContent, string fileName = "Unknown")
        {
            var metadata = new ScriptMetadata
            {
                Name = fileName,
                FilePath = fileName,
                LastModified = DateTime.Now,
                CapabilityLevel = ScriptCapabilityLevel.P1_Deterministic, // Default to P1
                SchemaVersion = "1.0.0",
                Author = "Unknown"
            };

            try
            {
                // Read first 20 lines to extract metadata from comments
                var lines = scriptContent.Split('\n').Take(20).ToArray();

                foreach (var line in lines)
                {
                    var cleanLine = line.Trim().ToLower();

                    // Chat's capability detection patterns
                    if (cleanLine.Contains("@capability") || cleanLine.Contains("# capability"))
                    {
                        if (cleanLine.Contains("p1") || cleanLine.Contains("deterministic"))
                            metadata.CapabilityLevel = ScriptCapabilityLevel.P1_Deterministic;
                        else if (cleanLine.Contains("p2") || cleanLine.Contains("analytic"))
                            metadata.CapabilityLevel = ScriptCapabilityLevel.P2_Analytic;
                        else if (cleanLine.Contains("p3") || cleanLine.Contains("adaptive"))
                            metadata.CapabilityLevel = ScriptCapabilityLevel.P3_Adaptive;
                    }

                    // Extract other metadata
                    if (cleanLine.StartsWith("# description:") || cleanLine.StartsWith("// description:"))
                        metadata.Description = line.Substring(line.IndexOf(':') + 1).Trim();

                    if (cleanLine.StartsWith("# author:") || cleanLine.StartsWith("// author:"))
                        metadata.Author = line.Substring(line.IndexOf(':') + 1).Trim();

                    if (cleanLine.StartsWith("# version:") || cleanLine.StartsWith("// version:"))
                        metadata.SchemaVersion = line.Substring(line.IndexOf(':') + 1).Trim();

                    // 🎯 Chat's Layout Customization Headers
                    if (cleanLine.StartsWith("# @stack:") || cleanLine.StartsWith("// @stack:"))
                    {
                        metadata.StackName = line.Substring(line.IndexOf(':') + 1).Trim().Trim('"');
                        metadata.StackId = GenerateStackId(metadata.StackName);
                    }

                    if (cleanLine.StartsWith("# @stack_order:") || cleanLine.StartsWith("// @stack_order:"))
                    {
                        if (int.TryParse(line.Substring(line.IndexOf(':') + 1).Trim(), out int order))
                            metadata.StackOrder = order;
                    }

                    if (cleanLine.StartsWith("# @panel:") || cleanLine.StartsWith("// @panel:"))
                    {
                        metadata.PreferredPanel = line.Substring(line.IndexOf(':') + 1).Trim().Trim('"');
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to parse metadata for {fileName}: {ex.Message}");
            }

            return metadata;
        }

        /// <summary>
        /// Parse metadata from script file path
        /// </summary>
        public ScriptMetadata ParseMetadata(string scriptPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(scriptPath);
            var metadata = new ScriptMetadata
            {
                Name = fileName,
                FilePath = scriptPath,
                LastModified = File.GetLastWriteTime(scriptPath),
                CapabilityLevel = ScriptCapabilityLevel.P1_Deterministic, // Default to P1
                SchemaVersion = "1.0.0",
                Author = "Unknown"
            };

            try
            {
                // Read first 20 lines to extract metadata from comments
                var lines = File.ReadLines(scriptPath).Take(20).ToArray();

                foreach (var line in lines)
                {
                    var cleanLine = line.Trim().ToLower();

                    // Chat's capability detection patterns
                    if (cleanLine.Contains("@capability") || cleanLine.Contains("# capability"))
                    {
                        if (cleanLine.Contains("p1") || cleanLine.Contains("deterministic"))
                            metadata.CapabilityLevel = ScriptCapabilityLevel.P1_Deterministic;
                        else if (cleanLine.Contains("p2") || cleanLine.Contains("analytic"))
                            metadata.CapabilityLevel = ScriptCapabilityLevel.P2_Analytic;
                        else if (cleanLine.Contains("p3") || cleanLine.Contains("adaptive"))
                            metadata.CapabilityLevel = ScriptCapabilityLevel.P3_Adaptive;
                    }

                    // Extract other metadata
                    if (cleanLine.StartsWith("# description:") || cleanLine.StartsWith("// description:"))
                        metadata.Description = line.Substring(line.IndexOf(':') + 1).Trim();

                    if (cleanLine.StartsWith("# author:") || cleanLine.StartsWith("// author:"))
                        metadata.Author = line.Substring(line.IndexOf(':') + 1).Trim();

                    if (cleanLine.StartsWith("# version:") || cleanLine.StartsWith("// version:"))
                        metadata.SchemaVersion = line.Substring(line.IndexOf(':') + 1).Trim();

                    // 🎯 Chat's Layout Customization Headers
                    if (cleanLine.StartsWith("# @stack:") || cleanLine.StartsWith("// @stack:"))
                    {
                        metadata.StackName = line.Substring(line.IndexOf(':') + 1).Trim().Trim('"');
                        metadata.StackId = GenerateStackId(metadata.StackName);
                    }

                    if (cleanLine.StartsWith("# @stack_order:") || cleanLine.StartsWith("// @stack_order:"))
                    {
                        if (int.TryParse(line.Substring(line.IndexOf(':') + 1).Trim(), out int order))
                            metadata.StackOrder = order;
                    }

                    if (cleanLine.StartsWith("# @panel:") || cleanLine.StartsWith("// @panel:"))
                    {
                        metadata.PreferredPanel = line.Substring(line.IndexOf(':') + 1).Trim().Trim('"');
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to parse metadata for {scriptPath}: {ex.Message}");
            }

            return metadata;
        }

        /// <summary>
        /// Generate a consistent stack ID from stack name
        /// </summary>
        private string GenerateStackId(string stackName)
        {
            if (string.IsNullOrEmpty(stackName))
                return Guid.NewGuid().ToString();

            // Create a deterministic ID based on the stack name
            return stackName.Replace(" ", "").Replace("-", "").Replace("_", "").ToLower();
        }
    }

    /// <summary>
    /// Script definition container for bundled scripts
    /// </summary>
    public class ScriptDefinition
    {
        public ScriptMetadata Metadata { get; set; }
        public string Content { get; set; }
        public string FilePath { get; set; } // For bundled scripts, this is the resource name
    }
}
