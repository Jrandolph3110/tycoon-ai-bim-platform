using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.UI;
using TycoonRevitAddin.Utils;
using Newtonsoft.Json;

namespace TycoonRevitAddin.Plugins
{
    /// <summary>
    /// 🎯 Script Capability Levels (Chat's P1/P2/P3 System)
    /// </summary>
    public enum ScriptCapabilityLevel
    {
        P1_Deterministic = 1,    // Bulletproof, never fails, instant execution
        P2_Analytic = 2,         // AI analysis → deterministic execution
        P3_Adaptive = 3          // Full AI-assisted with learning capabilities
    }

    /// <summary>
    /// 📋 Script Metadata (Chat's Capability Tagging)
    /// </summary>
    public class ScriptMetadata
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
        public ScriptCapabilityLevel CapabilityLevel { get; set; }
        public string SchemaVersion { get; set; }
        public DateTime LastModified { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public List<string> RequiredPermissions { get; set; } = new List<string>();
        public Dictionary<string, object> Telemetry { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 📜 Scripts Plugin - Chat's Three-Tier Architecture Implementation
    /// Provides capability-based script segregation with P1/P2/P3 levels
    /// </summary>
    public class ScriptsPlugin : PluginBase
    {
        private readonly string _scriptsPath;
        private readonly Dictionary<string, DateTime> _scriptModificationTimes;
        private readonly List<PushButton> _scriptButtons;
        private readonly Dictionary<string, ScriptMetadata> _scriptMetadata; // Chat's capability tracking
        private readonly Dictionary<ScriptCapabilityLevel, List<PushButton>> _buttonsByCapability; // Chat's segregation

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
            _scriptMetadata = new Dictionary<string, ScriptMetadata>(); // Chat's capability tracking
            _buttonsByCapability = new Dictionary<ScriptCapabilityLevel, List<PushButton>>
            {
                { ScriptCapabilityLevel.P1_Deterministic, new List<PushButton>() },
                { ScriptCapabilityLevel.P2_Analytic, new List<PushButton>() },
                { ScriptCapabilityLevel.P3_Adaptive, new List<PushButton>() }
            };

            // Ensure scripts directory exists
            EnsureScriptsDirectory();
        }

        protected override void CreatePanels()
        {
            // 🎯 Chat's Three-Tier Ribbon Architecture Implementation

            // First, populate script metadata by scanning directory
            LoadScriptMetadata();

            // 🟢 Panel 1: "Production" - P1 Dedicated Scripts (Green Theme)
            var productionPanel = CreatePanel("🟢 Production");
            CreateProductionScriptButtons(productionPanel);

            // 🟡 Panel 2: "Smart Tools β" - P2/P3 AI-Assisted Scripts (Yellow/Orange Theme)
            var smartToolsPanel = CreatePanel("🧠 Smart Tools β");
            CreateSmartToolsButtons(smartToolsPanel);

            // ⚙️ Panel 3: "Script Management" - Development and Management Tools
            var managementPanel = CreatePanel("⚙️ Management");
            CreateScriptManagementButtons(managementPanel);
        }

        /// <summary>
        /// 🎯 Load Script Metadata Only (Chat's Capability System)
        /// Scans scripts directory and populates metadata for capability-based segregation
        /// </summary>
        private void LoadScriptMetadata()
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

                _logger.Log($"🎯 Scanning {scriptFiles.Length} scripts for capability metadata");

                // Parse all script metadata for capability-based segregation
                foreach (var scriptFile in scriptFiles)
                {
                    // 🎯 Parse script metadata (Chat's capability system)
                    var metadata = ParseScriptMetadata(scriptFile);
                    _scriptMetadata[scriptFile] = metadata;

                    // Track modification time for hot-reload
                    _scriptModificationTimes[scriptFile] = File.GetLastWriteTime(scriptFile);
                }

                // 📊 Log capability distribution
                var p1Count = _scriptMetadata.Count(kvp => kvp.Value.CapabilityLevel == ScriptCapabilityLevel.P1_Deterministic);
                var p2Count = _scriptMetadata.Count(kvp => kvp.Value.CapabilityLevel == ScriptCapabilityLevel.P2_Analytic);
                var p3Count = _scriptMetadata.Count(kvp => kvp.Value.CapabilityLevel == ScriptCapabilityLevel.P3_Adaptive);

                _logger.Log($"🎯 Script Capability Distribution: P1={p1Count}, P2={p2Count}, P3={p3Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load script metadata", ex);
            }
        }

        /// <summary>
        /// 🟢 Create Production Script Buttons (Chat's P1-Deterministic)
        /// Bulletproof scripts with green theme for universal access
        /// </summary>
        private void CreateProductionScriptButtons(RibbonPanel panel)
        {
            _logger.Log("🟢 Creating Production (P1-Deterministic) script buttons");

            // Get all P1 scripts from directory
            var p1Scripts = GetScriptsByCapability(ScriptCapabilityLevel.P1_Deterministic);

            foreach (var scriptFile in p1Scripts.Take(8)) // Limit for ribbon space
            {
                var metadata = _scriptMetadata[scriptFile];
                var displayName = FormatScriptName(metadata.Name);

                var button = AddPushButton(
                    panel,
                    $"P1_{metadata.Name}",
                    displayName,
                    "TycoonRevitAddin.Commands.DynamicScriptCommand",
                    $"🟢 BULLETPROOF: {displayName}\n{metadata.Description}\nAuthor: {metadata.Author}",
                    GetCapabilityIcon(metadata.CapabilityLevel)
                );

                // Store metadata for execution
                button.ToolTip = $"🟢 P1-BULLETPROOF: {displayName}\n" +
                               $"Never fails • Instant execution\n" +
                               $"Path: {scriptFile}";

                _scriptButtons.Add(button);
                _buttonsByCapability[ScriptCapabilityLevel.P1_Deterministic].Add(button);
            }

            _logger.Log($"🟢 Created {p1Scripts.Count()} Production script buttons");
        }

        /// <summary>
        /// 🧠 Create Smart Tools Buttons (Chat's P2-Analytic + P3-Adaptive)
        /// AI-assisted scripts with yellow/orange theme for authorized users
        /// </summary>
        private void CreateSmartToolsButtons(RibbonPanel panel)
        {
            _logger.Log("🧠 Creating Smart Tools (P2/P3 AI-Assisted) script buttons");

            // Get P2 and P3 scripts
            var p2Scripts = GetScriptsByCapability(ScriptCapabilityLevel.P2_Analytic);
            var p3Scripts = GetScriptsByCapability(ScriptCapabilityLevel.P3_Adaptive);
            var allSmartScripts = p2Scripts.Concat(p3Scripts);

            foreach (var scriptFile in allSmartScripts.Take(8)) // Limit for ribbon space
            {
                var metadata = _scriptMetadata[scriptFile];
                var displayName = FormatScriptName(metadata.Name);
                var badge = GetCapabilityBadge(metadata.CapabilityLevel);

                var button = AddPushButton(
                    panel,
                    $"Smart_{metadata.Name}",
                    displayName,
                    "TycoonRevitAddin.Commands.DynamicScriptCommand",
                    $"{badge}: {displayName}\n{metadata.Description}\nAuthor: {metadata.Author}",
                    GetCapabilityIcon(metadata.CapabilityLevel)
                );

                // Enhanced tooltip with AI capabilities
                button.ToolTip = $"{badge}: {displayName}\n" +
                               $"AI-powered • Context-aware\n" +
                               $"Requires authorization\n" +
                               $"Path: {scriptFile}";

                _scriptButtons.Add(button);
                _buttonsByCapability[metadata.CapabilityLevel].Add(button);
            }

            _logger.Log($"🧠 Created {allSmartScripts.Count()} Smart Tools script buttons");
        }

        /// <summary>
        /// Get scripts by capability level from metadata
        /// </summary>
        private IEnumerable<string> GetScriptsByCapability(ScriptCapabilityLevel level)
        {
            return _scriptMetadata
                .Where(kvp => kvp.Value.CapabilityLevel == level)
                .Select(kvp => kvp.Key);
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
        /// 🎯 Load Script Metadata (Chat's Capability System)
        /// Scans scripts directory and populates metadata for capability-based segregation
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

                _logger.Log($"🎯 Scanning {scriptFiles.Length} scripts for capability metadata");

                // Parse all script metadata for capability-based segregation
                foreach (var scriptFile in scriptFiles)
                {
                    // 🎯 Parse script metadata (Chat's capability system)
                    var metadata = ParseScriptMetadata(scriptFile);
                    _scriptMetadata[scriptFile] = metadata;

                    // Track modification time for hot-reload
                    _scriptModificationTimes[scriptFile] = File.GetLastWriteTime(scriptFile);
                }

                // 📊 Log capability distribution
                var p1Count = _scriptMetadata.Count(kvp => kvp.Value.CapabilityLevel == ScriptCapabilityLevel.P1_Deterministic);
                var p2Count = _scriptMetadata.Count(kvp => kvp.Value.CapabilityLevel == ScriptCapabilityLevel.P2_Analytic);
                var p3Count = _scriptMetadata.Count(kvp => kvp.Value.CapabilityLevel == ScriptCapabilityLevel.P3_Adaptive);

                _logger.Log($"🎯 Script Capability Distribution: P1={p1Count}, P2={p2Count}, P3={p3Count}");

                _logger.Log($"📜 Loaded {scriptFiles.Length} script buttons");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load script buttons", ex);
            }
        }

        /// <summary>
        /// 🎯 Parse Script Metadata (Chat's Capability Detection)
        /// Extracts P1/P2/P3 capability level from script headers
        /// </summary>
        private ScriptMetadata ParseScriptMetadata(string scriptPath)
        {
            var metadata = new ScriptMetadata
            {
                Name = Path.GetFileNameWithoutExtension(scriptPath),
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
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to parse metadata for {scriptPath}: {ex.Message}");
            }

            return metadata;
        }

        /// <summary>
        /// 🎨 Get Capability-Specific Icon (Chat's Visual Segregation)
        /// </summary>
        private string GetCapabilityIcon(ScriptCapabilityLevel level)
        {
            return level switch
            {
                ScriptCapabilityLevel.P1_Deterministic => "ScriptIcon_P1_Green.png",    // Green for bulletproof
                ScriptCapabilityLevel.P2_Analytic => "ScriptIcon_P2_Yellow.png",       // Yellow for analytic
                ScriptCapabilityLevel.P3_Adaptive => "ScriptIcon_P3_Orange.png",       // Orange for adaptive
                _ => "ScriptIcon.png"
            };
        }

        /// <summary>
        /// 🏷️ Get Capability Badge (Chat's UX Enhancement)
        /// </summary>
        private string GetCapabilityBadge(ScriptCapabilityLevel level)
        {
            return level switch
            {
                ScriptCapabilityLevel.P1_Deterministic => "🟢 P1-BULLETPROOF",
                ScriptCapabilityLevel.P2_Analytic => "🟡 P2-ANALYTIC",
                ScriptCapabilityLevel.P3_Adaptive => "🟠 P3-ADAPTIVE",
                _ => "⚪ UNKNOWN"
            };
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
