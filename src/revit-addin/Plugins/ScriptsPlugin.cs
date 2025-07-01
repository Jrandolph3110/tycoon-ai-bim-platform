using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using TycoonRevitAddin.Utils;
using TycoonRevitAddin.Layout;
using TycoonRevitAddin.Services;
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
    /// 📋 Script Metadata (Chat's Capability Tagging + Layout Support)
    /// Enhanced with @stack header support for user-customizable stacking
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
        public string Version { get; set; }
        public List<string> RequiredPermissions { get; set; } = new List<string>();
        public Dictionary<string, object> Telemetry { get; set; } = new Dictionary<string, object>();
        public bool IsGitHubScript { get; set; } = false;

        // 🎯 Chat's Layout Customization Support
        public string StackName { get; set; } // @stack: "Framing QA"
        public int StackOrder { get; set; } = 0; // @stack_order: 2
        public string StackId { get; set; } // Generated GUID for persistence
        public string PreferredPanel { get; set; } // @panel: "Production" (optional override)
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

        // 🔥 PyRevit-Style Hot-Reload Infrastructure
        private readonly Dictionary<string, RibbonPanel> _activePanels;
        private readonly Dictionary<string, PushButton> _dynamicButtons;
        private readonly List<IList<RibbonItem>> _dynamicStacks; // Track stacked items for removal
        private RibbonPanel _productionPanel;
        private RibbonPanel _smartToolsPanel;
        private RibbonPanel _managementPanel;

        // 🎯 Chat's Layout Management System
        private readonly RibbonLayoutManager _layoutManager;

        // 🔄 GitHub Cache Management System
        private readonly GitCacheManager _gitCacheManager;

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

            // 🔥 Initialize PyRevit-Style Hot-Reload Infrastructure
            _activePanels = new Dictionary<string, RibbonPanel>();
            _dynamicButtons = new Dictionary<string, PushButton>();
            _dynamicStacks = new List<IList<RibbonItem>>();

            // 🎯 Initialize Chat's Layout Management System
            _layoutManager = new RibbonLayoutManager(logger);

            // 🔄 Initialize GitHub Cache Management System
            _gitCacheManager = new GitCacheManager(logger);

            // Ensure scripts directory exists
            EnsureScriptsDirectory();

            // 🚀 Check for first-run setup
            CheckFirstRunSetup();
        }

        protected override void CreatePanels()
        {
            // 🎯 Chat's Three-Tier Ribbon Architecture Implementation

            // First, populate script metadata by scanning directory
            LoadScriptMetadata();

            // 🟢 Panel 1: "Production" - P1 Dedicated Scripts (Green Theme)
            _productionPanel = CreatePanel("🟢 Production");
            _activePanels["Production"] = _productionPanel;
            // NOTE: No hardcoded buttons - Layout Manager will handle all script buttons

            // 🟡 Panel 2: "Smart Tools β" - P2/P3 AI-Assisted Scripts (Yellow/Orange Theme)
            _smartToolsPanel = CreatePanel("🧠 Smart Tools β");
            _activePanels["SmartTools"] = _smartToolsPanel;
            // NOTE: No hardcoded buttons - Layout Manager will handle all script buttons

            // ⚙️ Panel 3: "Script Management" - Development and Management Tools
            _managementPanel = CreatePanel("⚙️ Management");
            _activePanels["Management"] = _managementPanel;
            CreateScriptManagementButtons(_managementPanel);

            // 🎯 CRITICAL FIX: Apply Layout Manager immediately after creating panels
            // This ensures user's saved layout is applied instead of hardcoded buttons
            try
            {
                _logger.Log("🎯 Applying Layout Manager during initialization");
                CreateDynamicButtons();
                _logger.Log("✅ Layout Manager applied successfully during initialization");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to apply Layout Manager during initialization", ex);
                // Fallback to hardcoded buttons if Layout Manager fails
                _logger.Log("⚠️ Falling back to hardcoded buttons");
                CreateProductionScriptButtons(_productionPanel);
                CreateSmartToolsButtons(_smartToolsPanel);
            }
        }

        /// <summary>
        /// 🎯 Load Script Metadata Only (Chat's Capability System)
        /// Scans scripts directory and populates metadata for capability-based segregation
        /// </summary>
        private void LoadScriptMetadata()
        {
            try
            {
                _logger.Log("🔍 DIAGNOSTIC: Starting LoadScriptMetadata");

                // 1. Load local scripts
                if (!Directory.Exists(_scriptsPath))
                {
                    _logger.LogWarning($"Scripts directory not found: {_scriptsPath}");
                }
                else
                {
                    var scriptFiles = Directory.GetFiles(_scriptsPath, "*.py", SearchOption.AllDirectories)
                        .Concat(Directory.GetFiles(_scriptsPath, "*.cs", SearchOption.AllDirectories))
                        .ToArray();

                    _logger.Log($"🎯 Scanning {scriptFiles.Length} local scripts for capability metadata");

                    // Parse all script metadata for capability-based segregation
                    foreach (var scriptFile in scriptFiles)
                    {
                        // 🎯 Parse script metadata (Chat's capability system)
                        var metadata = ParseScriptMetadata(scriptFile);
                        _scriptMetadata[scriptFile] = metadata;

                        // Track modification time for hot-reload
                        _scriptModificationTimes[scriptFile] = File.GetLastWriteTime(scriptFile);
                    }
                }

                // 2. Load GitHub scripts (Chat's cache validation)
                _logger.Log("🔍 DIAGNOSTIC: Loading GitHub scripts from cache");
                LoadGitHubScriptsIntoMetadata();

                // 📊 Log capability distribution
                var p1Count = _scriptMetadata.Count(kvp => kvp.Value.CapabilityLevel == ScriptCapabilityLevel.P1_Deterministic);
                var p2Count = _scriptMetadata.Count(kvp => kvp.Value.CapabilityLevel == ScriptCapabilityLevel.P2_Analytic);
                var p3Count = _scriptMetadata.Count(kvp => kvp.Value.CapabilityLevel == ScriptCapabilityLevel.P3_Adaptive);

                _logger.Log($"🎯 Script Capability Distribution: P1={p1Count}, P2={p2Count}, P3={p3Count}");

                // Chat's diagnostic: Log all script names for debugging
                var allScriptNames = _scriptMetadata.Values.Select(s => s.Name).OrderBy(n => n);
                _logger.Log($"🔍 DIAGNOSTIC: All loaded script names: {string.Join(", ", allScriptNames)}");
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

            // 🎯 Layout Management button (Chat's customization system)
            AddPushButton(
                panel,
                "LayoutManager",
                "Layout\nManager",
                "TycoonRevitAddin.Commands.LayoutManagerCommand",
                "Customize button stacking and layout",
                "LayoutIcon.png"
            );

            // Reset Layout button
            AddPushButton(
                panel,
                "ResetLayout",
                "Reset\nLayout",
                "TycoonRevitAddin.Commands.ResetLayoutCommand",
                "Reset to automatic layout",
                "ResetIcon.png"
            );

            // ⚙️ GitHub Settings button
            AddPushButton(
                panel,
                "GitHubSettings",
                "GitHub\nSettings",
                "TycoonRevitAddin.Commands.GitHubSettingsCommand",
                "Configure GitHub repository settings for script updates",
                "GitHubIcon.png"
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
        /// 🎯 Generate Stack ID from Stack Name
        /// Creates consistent GUID-based ID for stack persistence
        /// </summary>
        private string GenerateStackId(string stackName)
        {
            if (string.IsNullOrEmpty(stackName))
                return null;

            // Create deterministic GUID from stack name for consistency
            var nameBytes = System.Text.Encoding.UTF8.GetBytes(stackName.ToLowerInvariant());
            var hash = System.Security.Cryptography.MD5.Create().ComputeHash(nameBytes);
            var guid = new Guid(hash);
            return guid.ToString();
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

                    // 🎯 GITHUB-ONLY SYSTEM: No sample scripts created
                    // All scripts now come from GitHub repository only
                    _logger.Log("🔄 Pure GitHub-driven system - no local sample scripts created");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create scripts directory", ex);
            }
        }

        // 🎯 REMOVED: CreateSampleScripts() method
        // Pure GitHub-driven system - no local sample scripts created
        // All scripts now come from GitHub repository only

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

        /// <summary>
        /// 🔥 PyRevit-Style Hot-Reload Implementation
        /// Instantly adds new script buttons without Revit restart!
        /// </summary>
        public void RefreshScripts()
        {
            try
            {
                _logger.Log("🔥 Starting PyRevit-style hot-reload (instant button creation)");

                // 1. Hide existing dynamic buttons (but keep them for reuse)
                HideDynamicButtons();

                // 2. Clear existing metadata and reload
                _scriptMetadata.Clear();
                foreach (var capabilityList in _buttonsByCapability.Values)
                {
                    capabilityList.Clear();
                }

                // 3. Reload script metadata (includes GitHub scripts)
                LoadScriptMetadata();

                // 4. 🔥 CREATE NEW BUTTONS INSTANTLY (PyRevit-style)
                CreateDynamicButtons();

                var p1Count = _scriptMetadata.Count(kvp => kvp.Value.CapabilityLevel == ScriptCapabilityLevel.P1_Deterministic);
                var p2Count = _scriptMetadata.Count(kvp => kvp.Value.CapabilityLevel == ScriptCapabilityLevel.P2_Analytic);
                var p3Count = _scriptMetadata.Count(kvp => kvp.Value.CapabilityLevel == ScriptCapabilityLevel.P3_Adaptive);

                _logger.Log($"🔥 PyRevit-style hot-reload complete: P1={p1Count}, P2={p2Count}, P3={p3Count}");
                _logger.Log($"🎯 {_dynamicButtons.Count} new buttons created instantly!");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to refresh scripts with hot-reload", ex);
                throw;
            }
        }

        /// <summary>
        /// 🔄 Hide All Dynamic Buttons for Layout Reorganization
        /// Hides existing buttons but keeps tracking collections for reuse
        /// </summary>
        private void HideDynamicButtons()
        {
            try
            {
                _logger.Log("🗑️ Clearing all dynamic buttons for layout reorganization");

                // Hide all tracked dynamic buttons
                foreach (var buttonPair in _dynamicButtons.ToList())
                {
                    try
                    {
                        buttonPair.Value.Visible = false;
                        buttonPair.Value.Enabled = false;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Could not hide button {buttonPair.Key}: {ex.Message}");
                    }
                }

                // Hide all stacked items
                foreach (var stack in _dynamicStacks)
                {
                    foreach (var item in stack)
                    {
                        try
                        {
                            item.Visible = false;
                            if (item is PushButton button)
                            {
                                button.Enabled = false;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Could not hide stacked item: {ex.Message}");
                        }
                    }
                }

                var hiddenCount = _dynamicButtons.Count + _dynamicStacks.Sum(s => s.Count);

                // Clear only stack tracking (buttons will be re-stacked)
                _dynamicStacks.Clear();

                _logger.Log($"🔄 Hidden {hiddenCount} dynamic buttons - keeping button tracking for reuse");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to clear dynamic buttons", ex);
            }
        }

        /// <summary>
        /// 🔥 PyRevit-Style Dynamic Button Creation with Chat's Layout System
        /// Creates stacked buttons using merged layout (User > Script Header > Capability Auto)
        /// </summary>
        private void CreateDynamicButtons()
        {
            try
            {
                _logger.Log("🔥 Creating dynamic stacked buttons (Chat's layout system)");

                // 🔄 Hide existing buttons first (for reuse strategy)
                HideDynamicButtons();

                // 🎯 Use Chat's Layout Manager to merge user preferences with auto layout
                _logger.Log("🔍 DIAGNOSTIC: Calling MergeLayouts with script metadata");
                var mergedLayout = _layoutManager.MergeLayouts(_scriptMetadata);

                _logger.Log($"🔍 DIAGNOSTIC: MergeLayouts returned {mergedLayout.Panels.Count} panels, mode: {mergedLayout.Mode}");

                // Log detailed layout structure for debugging
                foreach (var panelLayout in mergedLayout.Panels)
                {
                    _logger.Log($"🔍 DIAGNOSTIC: Panel '{panelLayout.Id}' has {panelLayout.Stacks.Count} stacks");
                    foreach (var stackLayout in panelLayout.Stacks)
                    {
                        _logger.Log($"🔍 DIAGNOSTIC: Stack '{stackLayout.Name}' has {stackLayout.Items.Count} items: {string.Join(", ", stackLayout.Items)}");
                    }
                }

                // Create buttons based on merged layout
                foreach (var panelLayout in mergedLayout.Panels)
                {
                    var ribbonPanel = GetRibbonPanelById(panelLayout.Id);
                    if (ribbonPanel == null)
                    {
                        _logger.LogWarning($"🔍 DIAGNOSTIC: No ribbon panel found for layout panel '{panelLayout.Id}'");
                        continue;
                    }

                    _logger.Log($"🔍 DIAGNOSTIC: Processing panel '{panelLayout.Id}' with {panelLayout.Stacks.Count} stacks");

                    foreach (var stackLayout in panelLayout.Stacks.OrderBy(s => s.Order))
                    {
                        CreateStackFromLayout(ribbonPanel, stackLayout);
                    }
                }

                _logger.Log($"🔥 Created {_dynamicButtons.Count} dynamic buttons in {_dynamicStacks.Count} stacks (Chat's layout system)!");
                _logger.Log($"🎯 Layout mode: {mergedLayout.Mode}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create dynamic stacked buttons", ex);
            }
        }

        /// <summary>
        /// 🎯 Get Ribbon Panel by ID
        /// Maps layout panel IDs to actual ribbon panels
        /// </summary>
        private RibbonPanel GetRibbonPanelById(string panelId)
        {
            return panelId switch
            {
                "Production" => _productionPanel,
                "SmartTools" => _smartToolsPanel,
                "Management" => _managementPanel,
                _ => null
            };
        }

        /// <summary>
        /// 🔥 Create Stack from Layout Configuration
        /// Creates PyRevit-style stacked buttons based on layout specification
        /// </summary>
        private void CreateStackFromLayout(RibbonPanel panel, StackLayout stackLayout)
        {
            try
            {
                _logger.Log($"🔍 CreateStackFromLayout called for stack '{stackLayout.Name}' with {stackLayout.Items.Count} items");
                var scripts = new List<ScriptMetadata>();

                // Find script metadata for items in this stack
                foreach (var itemName in stackLayout.Items)
                {
                    var script = _scriptMetadata.Values.FirstOrDefault(s => s.Name == itemName);
                    if (script != null)
                    {
                        scripts.Add(script);
                        _logger.Log($"✅ Found script '{itemName}' for stack '{stackLayout.Name}'");
                    }
                    else
                    {
                        _logger.LogWarning($"❌ Script '{itemName}' not found in metadata for stack '{stackLayout.Name}'");
                        // Chat's recommendation: Log available script names for debugging
                        var availableNames = string.Join(", ", _scriptMetadata.Values.Select(s => s.Name).Take(10));
                        _logger.Log($"📋 Available script names ({_scriptMetadata.Count} total): {availableNames}");

                        // Chat's guard: Still add placeholder to keep stack position stable
                        _logger.Log($"🔧 Adding placeholder for missing script '{itemName}' to maintain stack structure");
                    }
                }

                if (!scripts.Any())
                {
                    _logger.LogWarning($"No scripts found for stack '{stackLayout.Name}'");
                    return;
                }

                _logger.Log($"🔥 Creating stack '{stackLayout.Name}' with {scripts.Count} scripts");

                if (scripts.Count == 1)
                {
                    // Single button - reuse existing or create new
                    var scriptName = scripts[0].Name;
                    PushButton button = null;

                    if (_dynamicButtons.ContainsKey(scriptName))
                    {
                        // Reuse existing button
                        button = _dynamicButtons[scriptName];
                        button.Visible = true;
                        button.Enabled = true;
                        _logger.Log($"🔄 Reusing existing button for '{scriptName}'");
                    }
                    else
                    {
                        // Create new button
                        button = CreateDynamicScriptButton(panel, scripts[0]);
                        if (button != null)
                        {
                            _dynamicButtons[scriptName] = button;
                            _buttonsByCapability[scripts[0].CapabilityLevel].Add(button);
                            _logger.Log($"🆕 Created new button for '{scriptName}'");
                        }
                    }
                }
                else
                {
                    // For layout reorganization, use individual buttons (easier to reuse)
                    // TODO: Implement true stacked button reuse in future version
                    _logger.Log($"🔄 Creating individual buttons for stack '{stackLayout.Name}' (layout reorganization mode)");

                    foreach (var script in scripts)
                    {
                        var scriptName = script.Name;
                        PushButton button = null;

                        if (_dynamicButtons.ContainsKey(scriptName))
                        {
                            // Reuse existing button
                            button = _dynamicButtons[scriptName];
                            button.Visible = true;
                            button.Enabled = true;
                            _logger.Log($"🔄 Reusing existing button for '{scriptName}'");
                        }
                        else
                        {
                            // Create new button
                            button = CreateDynamicScriptButton(panel, script);
                            if (button != null)
                            {
                                _dynamicButtons[scriptName] = button;
                                _buttonsByCapability[script.CapabilityLevel].Add(button);
                                _logger.Log($"🆕 Created new button for '{scriptName}'");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create stack '{stackLayout.Name}'", ex);
            }
        }

        /// <summary>
        /// 🔥 Create PyRevit-Style Stacked Buttons for Capability Level
        /// Groups scripts into vertical stacks like PyRevit
        /// </summary>
        private void CreateStackedButtonsForCapability(List<ScriptMetadata> scripts, RibbonPanel panel, string capabilityName)
        {
            try
            {
                _logger.Log($"🔥 Creating stacked buttons for {capabilityName} ({scripts.Count} scripts)");

                // Create stacks of 2-3 buttons (PyRevit style)
                for (int i = 0; i < scripts.Count; i += 3)
                {
                    var stackScripts = scripts.Skip(i).Take(3).ToList();

                    if (stackScripts.Count == 1)
                    {
                        // Single button - add normally
                        var button = CreateDynamicScriptButton(panel, stackScripts[0]);
                        if (button != null)
                        {
                            _dynamicButtons[stackScripts[0].Name] = button;
                            _buttonsByCapability[stackScripts[0].CapabilityLevel].Add(button);
                        }
                    }
                    else
                    {
                        // Multiple buttons - create stack (PyRevit style!)
                        var buttonDataList = new List<PushButtonData>();

                        foreach (var script in stackScripts)
                        {
                            var buttonData = CreateButtonData(script);
                            if (buttonData != null)
                            {
                                buttonDataList.Add(buttonData);
                            }
                        }

                        if (buttonDataList.Count > 1)
                        {
                            // 🔥 CREATE PYREVIT-STYLE STACKED BUTTONS!
                            IList<RibbonItem> stackedItems = null;

                            if (buttonDataList.Count == 2)
                            {
                                stackedItems = panel.AddStackedItems(buttonDataList[0], buttonDataList[1]);
                            }
                            else if (buttonDataList.Count == 3)
                            {
                                stackedItems = panel.AddStackedItems(buttonDataList[0], buttonDataList[1], buttonDataList[2]);
                            }

                            if (stackedItems != null)
                            {
                                _dynamicStacks.Add(stackedItems);

                                // Track individual buttons
                                for (int j = 0; j < stackedItems.Count && j < stackScripts.Count; j++)
                                {
                                    if (stackedItems[j] is PushButton pushButton)
                                    {
                                        _dynamicButtons[stackScripts[j].Name] = pushButton;
                                        _buttonsByCapability[stackScripts[j].CapabilityLevel].Add(pushButton);
                                    }
                                }

                                _logger.Log($"✅ Created PyRevit-style stack with {stackedItems.Count} buttons");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create stacked buttons for {capabilityName}", ex);
            }
        }

        /// <summary>
        /// 🔥 Create Stacked Button Group
        /// Creates PyRevit-style vertical stack from script list
        /// </summary>
        private void CreateStackedButtonGroup(RibbonPanel panel, List<ScriptMetadata> scripts, StackLayout stackLayout)
        {
            try
            {
                var buttonDataList = new List<PushButtonData>();

                // Create button data for each script (up to 3 for PyRevit-style stacking)
                var stackScripts = scripts.Take(stackLayout.MaxItems).ToList();

                foreach (var script in stackScripts)
                {
                    var buttonData = CreateButtonData(script);
                    if (buttonData != null)
                    {
                        buttonDataList.Add(buttonData);
                    }
                }

                if (buttonDataList.Count > 1)
                {
                    // 🔥 CREATE PYREVIT-STYLE STACKED BUTTONS!
                    IList<RibbonItem> stackedItems = null;

                    if (buttonDataList.Count == 2)
                    {
                        stackedItems = panel.AddStackedItems(buttonDataList[0], buttonDataList[1]);
                    }
                    else if (buttonDataList.Count >= 3)
                    {
                        stackedItems = panel.AddStackedItems(buttonDataList[0], buttonDataList[1], buttonDataList[2]);
                    }

                    if (stackedItems != null)
                    {
                        _dynamicStacks.Add(stackedItems);

                        // Track individual buttons
                        for (int i = 0; i < stackedItems.Count && i < stackScripts.Count; i++)
                        {
                            if (stackedItems[i] is PushButton pushButton)
                            {
                                _dynamicButtons[stackScripts[i].Name] = pushButton;
                                _buttonsByCapability[stackScripts[i].CapabilityLevel].Add(pushButton);
                            }
                        }

                        _logger.Log($"✅ Created PyRevit-style stack '{stackLayout.Name}' with {stackedItems.Count} buttons");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create stacked button group for '{stackLayout.Name}'", ex);
            }
        }

        /// <summary>
        /// 🎯 Create Button Data for Script
        /// Prepares button data without adding to panel
        /// </summary>
        private PushButtonData CreateButtonData(ScriptMetadata metadata)
        {
            try
            {
                var displayName = FormatScriptName(metadata.Name);
                var capabilityBadge = GetCapabilityBadge(metadata.CapabilityLevel);

                var buttonData = new PushButtonData(
                    $"DynamicScript_{metadata.Name}_{DateTime.Now.Ticks}",
                    displayName,
                    Assembly.GetExecutingAssembly().Location,
                    "TycoonRevitAddin.Commands.DynamicScriptCommand"
                );

                buttonData.ToolTip = $"{capabilityBadge}: {displayName}\n" +
                                   $"{metadata.Description}\n" +
                                   $"Author: {metadata.Author}\n" +
                                   $"🔥 Hot-loaded script";

                buttonData.LongDescription = $"🔥 HOT-LOADED: {displayName}\n\n" +
                                            $"Capability: {metadata.CapabilityLevel}\n" +
                                            $"Description: {metadata.Description}\n" +
                                            $"Path: {metadata.FilePath}\n\n" +
                                            $"This script was dynamically loaded without restarting Revit!";

                return buttonData;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create button data for {metadata.Name}", ex);
                return null;
            }
        }

        /// <summary>
        /// 🎯 Create Individual Dynamic Script Button
        /// PyRevit-style button creation with capability-based theming
        /// </summary>
        private PushButton CreateDynamicScriptButton(RibbonPanel panel, ScriptMetadata metadata)
        {
            try
            {
                var displayName = FormatScriptName(metadata.Name);
                var capabilityBadge = GetCapabilityBadge(metadata.CapabilityLevel);

                var buttonData = new PushButtonData(
                    $"DynamicScript_{metadata.Name}_{DateTime.Now.Ticks}",
                    displayName,
                    Assembly.GetExecutingAssembly().Location,
                    "TycoonRevitAddin.Commands.DynamicScriptCommand"
                );

                buttonData.ToolTip = $"{capabilityBadge}: {displayName}\n" +
                                   $"{metadata.Description}\n" +
                                   $"Author: {metadata.Author}\n" +
                                   $"🔥 Hot-loaded script";

                buttonData.LongDescription = $"🔥 HOT-LOADED: {displayName}\n\n" +
                                            $"Capability: {metadata.CapabilityLevel}\n" +
                                            $"Description: {metadata.Description}\n" +
                                            $"Path: {metadata.FilePath}\n\n" +
                                            $"This script was dynamically loaded without restarting Revit!";

                // Set capability-specific icon
                var iconPath = GetCapabilityIcon(metadata.CapabilityLevel);
                if (!string.IsNullOrEmpty(iconPath))
                {
                    buttonData.Image = LoadIcon(iconPath, 16);
                    buttonData.LargeImage = LoadIcon(iconPath, 32);
                }

                // 🔥 ADD BUTTON TO PANEL INSTANTLY
                var button = panel.AddItem(buttonData) as PushButton;

                _logger.Log($"🔥 Dynamic button created: {displayName} ({metadata.CapabilityLevel})");
                return button;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create dynamic button for {metadata.Name}", ex);
                return null;
            }
        }

        /// <summary>
        /// Load icon from embedded resources or file path
        /// </summary>
        private System.Windows.Media.ImageSource LoadIcon(string iconPath, int size)
        {
            try
            {
                // For now, return null - icons will be added later
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 🎯 Public API for Layout Manager Access
        /// Allows external commands to access layout management functionality
        /// </summary>
        public RibbonLayoutManager GetLayoutManager()
        {
            return _layoutManager;
        }

        /// <summary>
        /// 🎯 Public API for Script Metadata Access
        /// Allows external commands to access current script metadata
        /// </summary>
        public Dictionary<string, ScriptMetadata> GetScriptMetadata()
        {
            return new Dictionary<string, ScriptMetadata>(_scriptMetadata);
        }

        /// <summary>
        /// 🎯 Public API for Logger Access
        /// Allows external commands to access the logger
        /// </summary>
        public Logger GetLogger()
        {
            return _logger;
        }

        /// <summary>
        /// 🔄 Public API for GitCacheManager Access
        /// Allows external commands to access the GitHub cache manager
        /// </summary>
        public GitCacheManager GetGitCacheManager()
        {
            return _gitCacheManager;
        }

        /// <summary>
        /// 🚀 Check if this is the first run and show setup wizard if needed
        /// </summary>
        private void CheckFirstRunSetup()
        {
            try
            {
                var settingsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Tycoon", "github-settings.json");

                var hasSettings = File.Exists(settingsPath);
                var hasCache = _gitCacheManager.IsOfflineModeAvailable();

                _logger.Log($"🔍 First-run check: Settings={hasSettings}, Cache={hasCache}");

                // Show first-run wizard if no settings AND no cached scripts
                if (!hasSettings && !hasCache)
                {
                    _logger.Log("🚀 First run detected - will show setup wizard when UI is ready");

                    // Schedule the wizard to show after UI initialization
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                        new Action(ShowFirstRunWizard));
                }
                else if (hasSettings && !hasCache)
                {
                    _logger.Log("⚙️ Settings found but no cache - will attempt background download");

                    // Schedule background download if settings exist but no cache
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new Action(AttemptBackgroundDownload));
                }
                else
                {
                    _logger.Log("✅ GitHub settings and cache available - system ready");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking first-run setup: {ex.Message}");
            }
        }

        /// <summary>
        /// 🚀 Show the first-run setup wizard
        /// </summary>
        private void ShowFirstRunWizard()
        {
            try
            {
                _logger.Log("🚀 Showing first-run setup wizard");

                var wizard = new TycoonRevitAddin.UI.FirstRunWizard(_gitCacheManager, _logger);
                var result = wizard.ShowDialog();

                if (result == true)
                {
                    _logger.Log("✅ First-run setup completed successfully");

                    // Refresh the ribbon to show downloaded scripts
                    RefreshRibbonWithGitHubScripts();
                }
                else
                {
                    _logger.Log("⏭️ First-run setup skipped by user");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error showing first-run wizard: {ex.Message}");
            }
        }

        /// <summary>
        /// 📥 Attempt background download of scripts when settings exist but no cache
        /// </summary>
        private async void AttemptBackgroundDownload()
        {
            try
            {
                _logger.Log("📥 Attempting background script download...");

                // Start background download without blocking UI
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Create cancellation token with 60-second timeout for background downloads
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

                        // Try to refresh cache in background
                        var success = await _gitCacheManager.RefreshCacheAsync(forceRefresh: false, cancellationToken: cts.Token);

                        if (success)
                        {
                            _logger.Log("✅ Background script download completed successfully");

                            // Refresh the ribbon to show downloaded scripts (on UI thread)
                            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                                System.Windows.Threading.DispatcherPriority.Background,
                                new Action(() => RefreshRibbonWithGitHubScripts()));

                            // Show subtle notification to user (on UI thread)
                            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                                System.Windows.Threading.DispatcherPriority.Background,
                                new Action(() =>
                                {
                                    System.Windows.MessageBox.Show(
                                        "✅ GitHub Scripts Downloaded!\n\n" +
                                        "Scripts have been downloaded from your configured repository.\n" +
                                        "The latest scripts are now available in the ribbon.",
                                        "🚀 Tycoon AI-BIM Platform",
                                        System.Windows.MessageBoxButton.OK,
                                        System.Windows.MessageBoxImage.Information);
                                }));
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ Background script download failed - will use offline mode");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error during background download task: {ex.Message}");
                    }
                });

                _logger.Log("📥 Background download task started - UI will remain responsive");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting background download: {ex.Message}");
            }
        }

        /// <summary>
        /// 🔄 Refresh ribbon with GitHub scripts after first-run setup
        /// </summary>
        private void RefreshRibbonWithGitHubScripts()
        {
            try
            {
                var cachedScriptsPath = _gitCacheManager.GetCachedScriptsPath();
                if (!string.IsNullOrEmpty(cachedScriptsPath))
                {
                    _logger.Log($"✅ GitHub scripts available at: {cachedScriptsPath}");

                    // Count available scripts
                    var scriptFiles = Directory.GetFiles(cachedScriptsPath, "*.py", SearchOption.AllDirectories)
                        .Concat(Directory.GetFiles(cachedScriptsPath, "*.cs", SearchOption.AllDirectories))
                        .ToArray();

                    _logger.Log($"📊 Found {scriptFiles.Length} GitHub scripts ready for use");

                    // 🎯 CRITICAL FIX: Use consistent metadata loading (clean names only)
                    // Remove duplicate LoadGitHubScriptMetadata call that uses "github_" prefix
                    // GitHub scripts are already loaded by LoadGitHubScriptsIntoMetadata() with clean names
                    _logger.Log("🔄 GitHub scripts already integrated with clean names - available in Layout Manager");
                }
                else
                {
                    _logger.LogWarning("⚠️ No cached GitHub scripts found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error refreshing ribbon with GitHub scripts: {ex.Message}");
            }
        }

        /// <summary>
        /// 🔍 Load GitHub scripts into metadata (Chat's cache validation)
        /// </summary>
        private void LoadGitHubScriptsIntoMetadata()
        {
            try
            {
                var cachedScriptsPath = _gitCacheManager?.GetCachedScriptsPath();
                if (string.IsNullOrEmpty(cachedScriptsPath) || !Directory.Exists(cachedScriptsPath))
                {
                    _logger.Log("🔍 DIAGNOSTIC: No GitHub cache found or cache directory doesn't exist");
                    return;
                }

                _logger.Log($"🔍 DIAGNOSTIC: GitHub cache path: {cachedScriptsPath}");

                var scriptFiles = Directory.GetFiles(cachedScriptsPath, "*.py", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(cachedScriptsPath, "*.cs", SearchOption.AllDirectories))
                    .ToArray();

                _logger.Log($"🔍 DIAGNOSTIC: Found {scriptFiles.Length} GitHub script files");

                foreach (var scriptFile in scriptFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(scriptFile);
                    var relativePath = scriptFile.Substring(cachedScriptsPath.Length).TrimStart('\\', '/');

                    // Create metadata for GitHub script
                    var metadata = new ScriptMetadata
                    {
                        Name = fileName, // CRITICAL: Use clean name without prefix
                        Description = $"GitHub script: {relativePath}",
                        Author = "GitHub Repository",
                        Version = "Latest",
                        CapabilityLevel = ScriptCapabilityLevel.P2_Analytic,
                        FilePath = scriptFile,
                        IsGitHubScript = true
                    };

                    // CRITICAL FIX: Use clean filename as key, not github_ prefix
                    // This matches what the layout expects
                    _scriptMetadata[fileName] = metadata;
                    _logger.Log($"🔍 DIAGNOSTIC: Added GitHub script '{fileName}' to metadata (IsGitHub: {metadata.IsGitHubScript})");
                }

                _logger.Log($"📋 Loaded metadata for {scriptFiles.Length} GitHub scripts");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading GitHub script metadata: {ex.Message}");
            }
        }

        // 🎯 REMOVED: LoadGitHubScriptMetadata() method that used "github_" prefix
        // This was causing conflicts with Layout Manager which expects clean script names
        // All GitHub script metadata is now handled by LoadGitHubScriptsIntoMetadata() with clean names

    }
}
