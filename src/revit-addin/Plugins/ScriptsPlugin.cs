using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.UI;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.Plugins
{
    /// <summary>
    /// Scripts Plugin - PyRevit-style script execution and management
    /// Provides hot-reload capabilities and dynamic script loading
    /// </summary>
    public class ScriptsPlugin : PluginBase
    {
        private readonly string _scriptsPath;
        private readonly Dictionary<string, DateTime> _scriptModificationTimes;
        private readonly List<PushButton> _scriptButtons;

        public override string Id => "scripts";
        public override string Name => "Scripts";
        public override string Description => "PyRevit-style script execution and management";
        public override string Version => "1.0.0";
        public override string IconPath => "Resources/ScriptsIcon.png";

        public ScriptsPlugin(Logger logger) : base(logger)
        {
            // Set up scripts directory
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _scriptsPath = Path.Combine(appDataPath, "Tycoon", "Scripts");
            
            _scriptModificationTimes = new Dictionary<string, DateTime>();
            _scriptButtons = new List<PushButton>();

            // Ensure scripts directory exists
            EnsureScriptsDirectory();
        }

        protected override void CreatePanels()
        {
            // Create Script Management panel
            var scriptManagementPanel = CreatePanel("Script Management");
            CreateScriptManagementButtons(scriptManagementPanel);

            // Create Dynamic Scripts panel
            var dynamicScriptsPanel = CreatePanel("Dynamic Scripts");
            CreateDynamicScriptButtons(dynamicScriptsPanel);

            // Create Development Tools panel
            var devToolsPanel = CreatePanel("Development Tools");
            CreateDevelopmentToolsButtons(devToolsPanel);
        }

        /// <summary>
        /// Create script management buttons
        /// </summary>
        private void CreateScriptManagementButtons(RibbonPanel panel)
        {
            // Reload Scripts button
            AddPushButton(
                panel,
                "ReloadScripts",
                "Reload\nScripts",
                "TycoonRevitAddin.Commands.ReloadScriptsCommand",
                "Reload all scripts from the scripts directory",
                "ReloadIcon.png"
            );

            // Open Scripts Folder button
            AddPushButton(
                panel,
                "OpenScriptsFolder",
                "Open Scripts\nFolder",
                "TycoonRevitAddin.Commands.OpenScriptsFolderCommand",
                "Open the scripts directory in Windows Explorer",
                "FolderIcon.png"
            );

            // Script Editor button
            AddPushButton(
                panel,
                "ScriptEditor",
                "Script\nEditor",
                "TycoonRevitAddin.Commands.ScriptEditorCommand",
                "Open the built-in script editor with syntax highlighting",
                "EditorIcon.png"
            );
        }

        /// <summary>
        /// Create dynamic script buttons based on scripts in the directory
        /// </summary>
        private void CreateDynamicScriptButtons(RibbonPanel panel)
        {
            // Scan scripts directory and create buttons
            LoadScriptButtons(panel);
        }

        /// <summary>
        /// Create development tools buttons
        /// </summary>
        private void CreateDevelopmentToolsButtons(RibbonPanel panel)
        {
            // Python Console button
            AddPushButton(
                panel,
                "PythonConsole",
                "Python\nConsole",
                "TycoonRevitAddin.Commands.PythonConsoleCommand",
                "Open interactive Python console for Revit API",
                "PythonIcon.png"
            );

            // API Explorer button
            AddPushButton(
                panel,
                "APIExplorer",
                "API\nExplorer",
                "TycoonRevitAddin.Commands.APIExplorerCommand",
                "Explore Revit API objects and properties",
                "APIIcon.png"
            );

            // Element Inspector button
            AddPushButton(
                panel,
                "ElementInspector",
                "Element\nInspector",
                "TycoonRevitAddin.Commands.ElementInspectorCommand",
                "Inspect selected elements and their properties",
                "InspectorIcon.png"
            );
        }

        /// <summary>
        /// Load script buttons from the scripts directory
        /// </summary>
        private void LoadScriptButtons(RibbonPanel panel)
        {
            try
            {
                if (!Directory.Exists(_scriptsPath))
                {
                    _logger.LogWarning($"Scripts directory not found: {_scriptsPath}");
                    return;
                }

                var scriptFiles = Directory.GetFiles(_scriptsPath, "*.py", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(_scriptsPath, "*.cs", SearchOption.AllDirectories))
                    .ToArray();

                foreach (var scriptFile in scriptFiles.Take(10)) // Limit to 10 scripts for ribbon space
                {
                    var scriptName = Path.GetFileNameWithoutExtension(scriptFile);
                    var displayName = FormatScriptName(scriptName);

                    var button = AddPushButton(
                        panel,
                        $"Script_{scriptName}",
                        displayName,
                        "TycoonRevitAddin.Commands.DynamicScriptCommand",
                        $"Execute script: {scriptName}",
                        "ScriptIcon.png"
                    );

                    // Store script path in button tooltip for later execution
                    button.ToolTip = $"Execute script: {scriptName}\nPath: {scriptFile}";
                    _scriptButtons.Add(button);

                    // Track modification time for hot-reload
                    _scriptModificationTimes[scriptFile] = File.GetLastWriteTime(scriptFile);
                }

                _logger.Log($"📜 Loaded {scriptFiles.Length} script buttons");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load script buttons", ex);
            }
        }

        /// <summary>
        /// Format script name for display
        /// </summary>
        private string FormatScriptName(string scriptName)
        {
            // Convert camelCase or snake_case to display format
            var formatted = scriptName.Replace("_", " ");
            
            // Add line break for long names
            if (formatted.Length > 8)
            {
                var words = formatted.Split(' ');
                if (words.Length > 1)
                {
                    var midPoint = words.Length / 2;
                    var firstLine = string.Join(" ", words.Take(midPoint));
                    var secondLine = string.Join(" ", words.Skip(midPoint));
                    return $"{firstLine}\n{secondLine}";
                }
                else if (formatted.Length > 12)
                {
                    var midPoint = formatted.Length / 2;
                    return $"{formatted.Substring(0, midPoint)}\n{formatted.Substring(midPoint)}";
                }
            }

            return formatted;
        }

        /// <summary>
        /// Ensure scripts directory exists and create sample scripts
        /// </summary>
        private void EnsureScriptsDirectory()
        {
            try
            {
                if (!Directory.Exists(_scriptsPath))
                {
                    Directory.CreateDirectory(_scriptsPath);
                    _logger.Log($"📁 Created scripts directory: {_scriptsPath}");

                    // Create sample scripts
                    CreateSampleScripts();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create scripts directory", ex);
            }
        }

        /// <summary>
        /// Create sample scripts for demonstration
        /// </summary>
        private void CreateSampleScripts()
        {
            try
            {
                // Sample Python script
                var pythonSample = @"# Sample Tycoon Script - Element Counter
# This script counts selected elements

import clr
clr.AddReference('RevitAPI')
from Autodesk.Revit.DB import *

# Get current selection
selection = __revit__.ActiveUIDocument.Selection
elements = [__revit__.ActiveUIDocument.Document.GetElement(id) for id in selection.GetElementIds()]

# Count by category
categories = {}
for element in elements:
    cat_name = element.Category.Name if element.Category else 'No Category'
    categories[cat_name] = categories.get(cat_name, 0) + 1

# Display results
result = f'Selected {len(elements)} elements:\n'
for cat, count in categories.items():
    result += f'  {cat}: {count}\n'

print(result)
";

                var pythonPath = Path.Combine(_scriptsPath, "ElementCounter.py");
                File.WriteAllText(pythonPath, pythonSample);

                // Sample C# script
                var csharpSample = @"// Sample Tycoon C# Script - Wall Analyzer
using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

public class WallAnalyzer
{
    public static void Execute(UIApplication uiApp)
    {
        var doc = uiApp.ActiveUIDocument.Document;
        var walls = new FilteredElementCollector(doc)
            .OfClass(typeof(Wall))
            .Cast<Wall>()
            .ToList();

        var totalLength = walls.Sum(w => w.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble());
        var totalArea = walls.Sum(w => w.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble());

        TaskDialog.Show(""Wall Analysis"", 
            $""Found {walls.Count} walls\n"" +
            $""Total Length: {totalLength:F2} ft\n"" +
            $""Total Area: {totalArea:F2} sq ft"");
    }
}
";

                var csharpPath = Path.Combine(_scriptsPath, "WallAnalyzer.cs");
                File.WriteAllText(csharpPath, csharpSample);

                _logger.Log("📝 Created sample scripts");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create sample scripts", ex);
            }
        }

        public override void OnUpdate()
        {
            // Check for script file changes and reload if necessary
            CheckForScriptChanges();
        }

        /// <summary>
        /// Check for changes in script files for hot-reload
        /// </summary>
        private void CheckForScriptChanges()
        {
            try
            {
                var changedScripts = new List<string>();

                foreach (var kvp in _scriptModificationTimes.ToList())
                {
                    var scriptPath = kvp.Key;
                    var lastModified = kvp.Value;

                    if (File.Exists(scriptPath))
                    {
                        var currentModified = File.GetLastWriteTime(scriptPath);
                        if (currentModified > lastModified)
                        {
                            changedScripts.Add(scriptPath);
                            _scriptModificationTimes[scriptPath] = currentModified;
                        }
                    }
                    else
                    {
                        // Script was deleted
                        _scriptModificationTimes.Remove(scriptPath);
                    }
                }

                if (changedScripts.Any())
                {
                    _logger.Log($"🔄 Detected changes in {changedScripts.Count} scripts - Hot reload available");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error checking script changes", ex);
            }
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            _logger.Log("📜 Scripts plugin activated - PyRevit-style development ready!");
        }

        protected override void OnDeactivated()
        {
            base.OnDeactivated();
            _logger.Log("📜 Scripts plugin deactivated");
        }

        protected override void OnCleanup()
        {
            base.OnCleanup();
            _scriptButtons.Clear();
            _scriptModificationTimes.Clear();
        }
    }
}
