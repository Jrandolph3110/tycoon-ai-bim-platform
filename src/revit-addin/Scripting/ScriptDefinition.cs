using System;

namespace TycoonRevitAddin.Scripting
{
    /// <summary>
    /// Simple, clean data model for script configuration
    /// Replaces the complex ScriptInfo/ScriptManifest system
    /// </summary>
    public class ScriptDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        public string AssemblyPath { get; set; }
        public string ClassName { get; set; }
        public string Panel { get; set; } = "Production";
        public string Stack { get; set; } // Null if not stacked
        public int StackOrder { get; set; } = 0;
        public string[] Tags { get; set; } = new string[0];
        public bool RequiresSelection { get; set; } = false;
        public string ToolTip { get; set; }
        public string IconPath { get; set; }

        // Dual-source support
        public string Source { get; set; } = "Local"; // "Local" or "GitHub"
        public string SourcePath { get; set; }

        // Stacking configuration
        public string StackType { get; set; } = "stacked"; // "stacked" (vertical) or "dropdown" (pulldown menu)

        /// <summary>
        /// Create from JSON file path
        /// </summary>
        public static ScriptDefinition FromJsonFile(string jsonPath)
        {
            try
            {
                var json = System.IO.File.ReadAllText(jsonPath);
                var definition = ParseSimpleJson(json);
                
                // Set computed properties
                var scriptDir = System.IO.Path.GetDirectoryName(jsonPath);
                if (!string.IsNullOrEmpty(definition.AssemblyPath) && !System.IO.Path.IsPathRooted(definition.AssemblyPath))
                {
                    definition.AssemblyPath = System.IO.Path.Combine(scriptDir, definition.AssemblyPath);
                }
                
                // Set default tooltip
                if (string.IsNullOrEmpty(definition.ToolTip))
                {
                    definition.ToolTip = $"🎯 {definition.Name}\n{definition.Description}\nAuthor: {definition.Author}";
                }
                
                return definition;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to parse script definition from {jsonPath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Simple JSON parser for basic ScriptDefinition properties (avoids Newtonsoft.Json dependency)
        /// </summary>
        private static ScriptDefinition ParseSimpleJson(string json)
        {
            var definition = new ScriptDefinition();

            // Simple regex-based parsing for basic properties
            var nameMatch = System.Text.RegularExpressions.Regex.Match(json, @"""name""\s*:\s*""([^""]+)""");
            if (nameMatch.Success) definition.Name = nameMatch.Groups[1].Value;

            var descMatch = System.Text.RegularExpressions.Regex.Match(json, @"""description""\s*:\s*""([^""]+)""");
            if (descMatch.Success) definition.Description = descMatch.Groups[1].Value;

            var authorMatch = System.Text.RegularExpressions.Regex.Match(json, @"""author""\s*:\s*""([^""]+)""");
            if (authorMatch.Success) definition.Author = authorMatch.Groups[1].Value;

            var versionMatch = System.Text.RegularExpressions.Regex.Match(json, @"""version""\s*:\s*""([^""]+)""");
            if (versionMatch.Success) definition.Version = versionMatch.Groups[1].Value;

            var assemblyMatch = System.Text.RegularExpressions.Regex.Match(json, @"""assemblyPath""\s*:\s*""([^""]+)""");
            if (assemblyMatch.Success) definition.AssemblyPath = assemblyMatch.Groups[1].Value;

            var classMatch = System.Text.RegularExpressions.Regex.Match(json, @"""className""\s*:\s*""([^""]+)""");
            if (classMatch.Success) definition.ClassName = classMatch.Groups[1].Value;

            var panelMatch = System.Text.RegularExpressions.Regex.Match(json, @"""panel""\s*:\s*""([^""]+)""");
            if (panelMatch.Success) definition.Panel = panelMatch.Groups[1].Value;

            var stackMatch = System.Text.RegularExpressions.Regex.Match(json, @"""stack""\s*:\s*""([^""]+)""");
            if (stackMatch.Success) definition.Stack = stackMatch.Groups[1].Value;

            var stackOrderMatch = System.Text.RegularExpressions.Regex.Match(json, @"""stackOrder""\s*:\s*(\d+)");
            if (stackOrderMatch.Success && int.TryParse(stackOrderMatch.Groups[1].Value, out int stackOrder))
            {
                definition.StackOrder = stackOrder;
            }

            var stackTypeMatch = System.Text.RegularExpressions.Regex.Match(json, @"""stackType""\s*:\s*""([^""]+)""");
            if (stackTypeMatch.Success)
            {
                definition.StackType = stackTypeMatch.Groups[1].Value;
            }

            return definition;
        }
    }
}
