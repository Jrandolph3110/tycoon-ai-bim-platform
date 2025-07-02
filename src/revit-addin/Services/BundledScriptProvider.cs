using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TycoonRevitAddin.Services
{
    /// <summary>
    /// 🎯 BundledScriptProvider - Embedded Assembly Resource Scripts
    /// Provides offline resilience by bundling essential scripts as embedded resources
    /// Ensures immediate functionality even without internet connection
    /// </summary>
    public class BundledScriptProvider
    {
        private readonly ScriptParser _scriptParser;
        private readonly Assembly _assembly;
        private readonly string _resourcePath;

        public BundledScriptProvider(ScriptParser scriptParser)
        {
            _scriptParser = scriptParser;
            _assembly = Assembly.GetExecutingAssembly();
            
            // Adjust the resource path to match your project's namespace and folder structure
            _resourcePath = $"{_assembly.GetName().Name}.Resources.BundledScripts.";
        }

        /// <summary>
        /// Get all bundled scripts from embedded resources
        /// </summary>
        public IEnumerable<ScriptDefinition> GetBundledScripts()
        {
            var scripts = new List<ScriptDefinition>();

            try
            {
                var resourceNames = _assembly.GetManifestResourceNames()
                    .Where(name => name.StartsWith(_resourcePath) && name.EndsWith(".py"))
                    .ToArray();

                foreach (var resourceName in resourceNames)
                {
                    try
                    {
                        using (var stream = _assembly.GetManifestResourceStream(resourceName))
                        {
                            if (stream == null)
                            {
                                continue; // Skip if resource not found
                            }

                            using (var reader = new StreamReader(stream))
                            {
                                string content = reader.ReadToEnd();
                                
                                // Extract script name from resource name
                                var scriptName = ExtractScriptNameFromResourceName(resourceName);
                                
                                // Parse metadata from content
                                var metadata = _scriptParser.ParseMetadata(content, scriptName);
                                
                                // Override some properties for bundled scripts
                                metadata.Name = scriptName;
                                metadata.Author = "Tycoon AI-BIM Platform";
                                metadata.IsGitHubScript = false; // These are bundled, not GitHub scripts
                                
                                // Create script definition
                                var scriptDef = new ScriptDefinition
                                {
                                    Metadata = metadata,
                                    Content = content,
                                    FilePath = resourceName // Use resource name as file path for bundled scripts
                                };

                                scripts.Add(scriptDef);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with other scripts
                        System.Diagnostics.Debug.WriteLine($"Failed to load bundled script {resourceName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to enumerate bundled scripts: {ex.Message}");
            }

            return scripts;
        }

        /// <summary>
        /// Get a specific bundled script by name
        /// </summary>
        public ScriptDefinition GetBundledScript(string scriptName)
        {
            return GetBundledScripts().FirstOrDefault(s => 
                string.Equals(s.Metadata.Name, scriptName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Check if a bundled script exists
        /// </summary>
        public bool HasBundledScript(string scriptName)
        {
            return GetBundledScript(scriptName) != null;
        }

        /// <summary>
        /// Get the content of a bundled script
        /// </summary>
        public string GetBundledScriptContent(string scriptName)
        {
            var script = GetBundledScript(scriptName);
            return script?.Content;
        }

        /// <summary>
        /// Extract script name from resource name
        /// Example: "TycoonRevitAddin.Resources.BundledScripts.ElementCounter.py" -> "ElementCounter"
        /// </summary>
        private string ExtractScriptNameFromResourceName(string resourceName)
        {
            try
            {
                // Remove the resource path prefix
                var nameWithExtension = resourceName.Substring(_resourcePath.Length);
                
                // Remove the file extension
                var lastDotIndex = nameWithExtension.LastIndexOf('.');
                if (lastDotIndex > 0)
                {
                    return nameWithExtension.Substring(0, lastDotIndex);
                }
                
                return nameWithExtension;
            }
            catch
            {
                // Fallback to using the full resource name
                return resourceName;
            }
        }

        /// <summary>
        /// Get list of available bundled script names
        /// </summary>
        public IEnumerable<string> GetBundledScriptNames()
        {
            return GetBundledScripts().Select(s => s.Metadata.Name);
        }

        /// <summary>
        /// Get count of available bundled scripts
        /// </summary>
        public int GetBundledScriptCount()
        {
            try
            {
                return _assembly.GetManifestResourceNames()
                    .Count(name => name.StartsWith(_resourcePath) && name.EndsWith(".py"));
            }
            catch
            {
                return 0;
            }
        }
    }
}
