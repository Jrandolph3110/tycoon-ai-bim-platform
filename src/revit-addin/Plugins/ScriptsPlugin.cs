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
using TycoonRevitAddin.Events;
using TycoonRevitAddin.Models;
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
        private RibbonPanel _scriptsControlPanel;

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

            // 🎯 Initialize ScriptService with async-first architecture
            ScriptService.Instance.Initialize(_gitCacheManager, logger, _scriptsPath);

            // 📡 Subscribe to ScriptService events for UI updates
            ScriptService.Instance.LocalScriptsUpdated += OnLocalScriptsUpdated;
            ScriptService.Instance.GitHubScriptsUpdated += OnGitHubScriptsUpdated;
            ScriptService.Instance.UpdateStatusChanged += OnUpdateStatusChanged;
            ScriptService.Instance.IsUpdatingChanged += OnIsUpdatingChanged;

            // 📡 Subscribe to Layout Manager events (Chat's event-driven architecture)
            EventBus.Instance.Subscribe<LayoutChangedEvent>(OnLayoutChanged);
            _logger.Log("📡 Subscribed to LayoutChanged events from Layout Manager");

            // Ensure scripts directory exists
            EnsureScriptsDirectory();

            _logger.Log("🎯 ScriptsPlugin initialized with async-first ScriptService architecture");
        }

        protected override void CreatePanels()
        {
            // 🎯 Scripts Plugin Ribbon Architecture - Ordered Layout
            // Order: Scripts Control > Management > Smart Tools > Production

            // 🎛️ Panel 1: "Scripts Control" - Development and Management Tools
            _scriptsControlPanel = CreatePanel("🎛️ Scripts Control");
            _activePanels["ScriptsControl"] = _scriptsControlPanel;
            CreateScriptManagementButtons(_scriptsControlPanel);

            // ⚙️ Panel 2: "Management" - Available for script organization via Layout Manager
            _managementPanel = CreatePanel("⚙️ Management");
            _activePanels["Management"] = _managementPanel;
            // NOTE: No hardcoded buttons - Layout Manager will handle all script buttons

            // 🧠 Panel 3: "Smart Tools β" - P2/P3 AI-Assisted Scripts (Yellow/Orange Theme)
            _smartToolsPanel = CreatePanel("🧠 Smart Tools β");
            _activePanels["SmartTools"] = _smartToolsPanel;
            // NOTE: No hardcoded buttons - Layout Manager will handle all script buttons

            // 🟢 Panel 4: "Production" - P1 Dedicated Scripts (Green Theme)
            _productionPanel = CreatePanel("🟢 Production");
            _activePanels["Production"] = _productionPanel;
            // NOTE: No hardcoded buttons - Layout Manager will handle all script buttons

            // 🎯 NEW ASYNC-FIRST ARCHITECTURE: ScriptService handles all script loading
            // UI shows bundled scripts immediately, updates asynchronously when GitHub completes
            try
            {
                _logger.Log("🎯 Initializing ribbon with async-first ScriptService architecture");

                // Create initial layout with bundled scripts (immediate)
                CreateInitialLayoutFromScriptService();

                _logger.Log("✅ Ribbon initialized with bundled scripts - GitHub update will follow asynchronously");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize ribbon with ScriptService", ex);
                _logger.Log("⚠️ Attempting fallback with minimal layout");
                try
                {
                    CreateMinimalFallbackLayout();
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError("Minimal fallback also failed", fallbackEx);
                    _logger.Log("⚠️ System will continue with management buttons only");
                }
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
        /// Create script management buttons for Scripts Control panel
        /// </summary>
        private void CreateScriptManagementButtons(RibbonPanel panel)
        {
            // Create button data for stacked buttons
            var reloadButtonData = new PushButtonData(
                "ReloadScripts",
                "Reload\nScripts",
                Assembly.GetExecutingAssembly().Location,
                "TycoonRevitAddin.Commands.ReloadScriptsCommand"
            );
            reloadButtonData.ToolTip = "Reload all scripts from the scripts directory";
            reloadButtonData.LargeImage = LoadIcon("ReloadIcon.png", 32);
            reloadButtonData.Image = LoadIcon("ReloadIcon.png", 16);

            var resetButtonData = new PushButtonData(
                "ResetLayout",
                "Reset\nLayout",
                Assembly.GetExecutingAssembly().Location,
                "TycoonRevitAddin.Commands.ResetLayoutCommand"
            );
            resetButtonData.ToolTip = "Reset to automatic layout";
            resetButtonData.LargeImage = LoadIcon("ResetIcon.png", 32);
            resetButtonData.Image = LoadIcon("ResetIcon.png", 16);

            var githubButtonData = new PushButtonData(
                "GitHubSettings",
                "GitHub\nSettings",
                Assembly.GetExecutingAssembly().Location,
                "TycoonRevitAddin.Commands.GitHubSettingsCommand"
            );
            githubButtonData.ToolTip = "Configure GitHub repository settings for script updates";
            githubButtonData.LargeImage = LoadIcon("GitHubIcon.png", 32);
            githubButtonData.Image = LoadIcon("GitHubIcon.png", 16);

            // Create stacked buttons
            var stackedItems = panel.AddStackedItems(
                reloadButtonData,
                resetButtonData,
                githubButtonData
            );

            // Open Scripts Folder button (standalone)
            AddPushButton(
                panel,
                "OpenScriptsFolder",
                "Open Scripts\nFolder",
                "TycoonRevitAddin.Commands.OpenScriptsFolderCommand",
                "Open the scripts directory in Windows Explorer",
                "FolderIcon.png"
            );

            // Script Editor button (standalone)
            AddPushButton(
                panel,
                "ScriptEditor",
                "Script\nEditor",
                "TycoonRevitAddin.Commands.ScriptEditorCommand",
                "Open the built-in script editor with syntax highlighting",
                "EditorIcon.png"
            );

            // 🎯 Layout Management button (standalone)
            AddPushButton(
                panel,
                "LayoutManager",
                "Layout\nManager",
                "TycoonRevitAddin.Commands.LayoutManagerCommand",
                "Customize button stacking and layout",
                "LayoutIcon.png"
            );
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
        /// 🔥 PyRevit-Style Hot-Reload Implementation (Simplified Unified Approach)
        /// Instantly refreshes script buttons without Revit restart using unified event-driven system
        /// </summary>
        public void RefreshScripts()
        {
            try
            {
                _logger.Log("🔥 Starting simplified hot-reload via unified event system");

                // 1. Clear existing metadata and button tracking
                _scriptMetadata.Clear();
                foreach (var capabilityList in _buttonsByCapability.Values)
                {
                    capabilityList.Clear();
                }

                // 2. 🎯 UNIFIED REFRESH: Let the event-driven system handle everything
                RefreshRibbonViaEvents();

                var p1Count = _scriptMetadata.Count(kvp => kvp.Value.CapabilityLevel == ScriptCapabilityLevel.P1_Deterministic);
                var p2Count = _scriptMetadata.Count(kvp => kvp.Value.CapabilityLevel == ScriptCapabilityLevel.P2_Analytic);
                var p3Count = _scriptMetadata.Count(kvp => kvp.Value.CapabilityLevel == ScriptCapabilityLevel.P3_Adaptive);

                _logger.Log($"🔥 Unified hot-reload complete: P1={p1Count}, P2={p2Count}, P3={p3Count}");
                _logger.Log($"🎯 {_dynamicButtons.Count} buttons configured via unified system");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to refresh scripts with unified hot-reload", ex);
                throw;
            }
        }

        /// <summary>
        /// 🎯 Create minimal fallback layout using unified system
        /// Used when main initialization fails but we still want to use event-driven architecture
        /// </summary>
        private void CreateMinimalFallbackLayout()
        {
            try
            {
                _logger.Log("🎯 Creating minimal fallback layout via unified system");

                // Create a minimal layout with just essential management tools
                var minimalLayout = new RibbonLayoutSchema
                {
                    Panels = new List<PanelLayout>
                    {
                        new PanelLayout
                        {
                            Id = "Production",
                            Name = "🟢 Production",
                            Stacks = new List<StackLayout>
                            {
                                new StackLayout
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    Name = "Essential Tools",
                                    Order = 1,
                                    StackType = StackType.Vertical,
                                    ScriptItems = new List<ScriptItem>() // Empty - management tools are in Scripts Control panel
                                }
                            }
                        }
                    }
                };

                // Apply minimal layout
                ApplyLayoutToRibbon(minimalLayout);
                _logger.Log("✅ Minimal fallback layout created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create minimal fallback layout", ex);
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
                "ScriptsControl" => _scriptsControlPanel,
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
                // Use ScriptItems (current format)
                var scriptItems = stackLayout.ScriptItems ?? new List<ScriptItem>();

                _logger.Log($"🔍 CreateStackFromLayout called for stack '{stackLayout.Name}' with {scriptItems.Count} items");
                var scriptsWithItems = new List<(ScriptMetadata metadata, ScriptItem scriptItem)>();

                // Find script metadata for items in this stack
                foreach (var scriptItem in scriptItems)
                {
                    var script = _scriptMetadata.Values.FirstOrDefault(s => s.Name == scriptItem.Name);
                    if (script != null)
                    {
                        scriptsWithItems.Add((script, scriptItem));
                        _logger.Log($"✅ Found script '{scriptItem.Name}' for stack '{stackLayout.Name}'");
                        if (!string.IsNullOrEmpty(scriptItem.IconPath))
                        {
                            _logger.Log($"🖼️ Custom icon found for '{scriptItem.Name}': {scriptItem.IconPath}");
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"❌ Script '{scriptItem.Name}' not found in metadata for stack '{stackLayout.Name}'");
                        // Log available script names for debugging
                        var availableNames = string.Join(", ", _scriptMetadata.Values.Select(s => s.Name).Take(10));
                        _logger.Log($"📋 Available script names ({_scriptMetadata.Count} total): {availableNames}");
                    }
                }

                if (!scriptsWithItems.Any())
                {
                    _logger.LogWarning($"No scripts found for stack '{stackLayout.Name}'");
                    return;
                }

                _logger.Log($"🔥 Creating stack '{stackLayout.Name}' with {scriptsWithItems.Count} scripts");

                if (scriptsWithItems.Count == 1)
                {
                    // Single button - create individual button
                    var (script, scriptItem) = scriptsWithItems[0];
                    var scriptName = script.Name;
                    PushButton button = null;

                    // Create button with custom icon support
                    var buttonData = CreatePushButtonDataForScript(script, scriptItem);
                    if (buttonData != null)
                    {
                        button = panel.AddItem(buttonData) as PushButton;
                    }
                    if (button != null)
                    {
                        // Update tracking - this replaces any existing button reference
                        _dynamicButtons[scriptName] = button;

                        // Ensure capability tracking is updated
                        var capabilityList = _buttonsByCapability[script.CapabilityLevel];
                        if (!capabilityList.Contains(button))
                        {
                            capabilityList.Add(button);
                        }

                        _logger.Log($"🆕 Created new button for '{scriptName}' on panel '{panel.Name}'");
                        if (!string.IsNullOrEmpty(scriptItem.IconPath))
                        {
                            _logger.Log($"🖼️ Applied custom icon: {scriptItem.IconPath}");
                        }
                    }
                }
                else
                {
                    // Multiple buttons - create stacked buttons (PyRevit style!)
                    _logger.Log($"🔥 Creating stacked buttons for stack '{stackLayout.Name}' with {scriptsWithItems.Count} scripts");

                    // Create button data for each script (stacked buttons are always small)
                    var buttonDataList = new List<PushButtonData>();
                    var scriptList = new List<ScriptMetadata>();

                    foreach (var (script, scriptItem) in scriptsWithItems)
                    {
                        var buttonData = CreatePushButtonDataForScript(script, scriptItem);
                        if (buttonData != null)
                        {
                            buttonDataList.Add(buttonData);
                            scriptList.Add(script);
                            if (!string.IsNullOrEmpty(scriptItem.IconPath))
                            {
                                _logger.Log($"🖼️ Custom icon for stacked button '{script.Name}': {scriptItem.IconPath}");
                            }
                        }
                    }

                    if (buttonDataList.Count > 1)
                    {
                        // 🔥 CREATE PYREVIT-STYLE STACKED BUTTONS!
                        IList<RibbonItem> stackedItems = null;

                        if (buttonDataList.Count == 2)
                        {
                            stackedItems = panel.AddStackedItems(buttonDataList[0], buttonDataList[1]);
                            _logger.Log($"✅ Created 2-button stack for '{stackLayout.Name}'");
                        }
                        else if (buttonDataList.Count == 3)
                        {
                            stackedItems = panel.AddStackedItems(buttonDataList[0], buttonDataList[1], buttonDataList[2]);
                            _logger.Log($"✅ Created 3-button stack for '{stackLayout.Name}'");
                        }
                        else if (buttonDataList.Count > 3)
                        {
                            // For now, create individual buttons for >3 items (future: implement pulldown)
                            _logger.Log($"⚠️ Stack '{stackLayout.Name}' has {buttonDataList.Count} items (>3), creating individual buttons");
                            foreach (var (script, scriptItem) in scriptsWithItems)
                            {
                                var buttonData = CreatePushButtonDataForScript(script, scriptItem);
                                if (buttonData != null)
                                {
                                    var button = panel.AddItem(buttonData) as PushButton;
                                    if (button != null)
                                    {
                                        _dynamicButtons[script.Name] = button;
                                        _buttonsByCapability[script.CapabilityLevel].Add(button);
                                        if (!string.IsNullOrEmpty(scriptItem.IconPath))
                                        {
                                            _logger.Log($"🖼️ Applied custom icon to individual button '{script.Name}': {scriptItem.IconPath}");
                                        }
                                    }
                                }
                            }
                        }

                        // Track stacked buttons
                        if (stackedItems != null)
                        {
                            _dynamicStacks.Add(stackedItems);

                            // Track individual buttons within the stack
                            for (int i = 0; i < stackedItems.Count && i < scriptList.Count; i++)
                            {
                                if (stackedItems[i] is PushButton pushButton)
                                {
                                    var script = scriptList[i];
                                    _dynamicButtons[script.Name] = pushButton;

                                    // Ensure capability tracking is updated
                                    var capabilityList = _buttonsByCapability[script.CapabilityLevel];
                                    if (!capabilityList.Contains(pushButton))
                                    {
                                        capabilityList.Add(pushButton);
                                    }

                                    _logger.Log($"🆕 Tracked stacked button for '{script.Name}' on panel '{panel.Name}'");
                                }
                            }
                        }
                    }
                    else if (buttonDataList.Count == 1)
                    {
                        // Single button fallback
                        var button = panel.AddItem(buttonDataList[0]) as PushButton;
                        if (button != null)
                        {
                            var script = scriptList[0];
                            _dynamicButtons[script.Name] = button;
                            _buttonsByCapability[script.CapabilityLevel].Add(button);
                            _logger.Log($"🆕 Created single button for '{script.Name}' on panel '{panel.Name}'");
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
        /// 🎯 Create PushButtonData for Script with Custom Icon Support
        /// Creates button data without adding to panel, enabling reuse for both individual and stacked buttons
        /// </summary>
        private PushButtonData CreatePushButtonDataForScript(ScriptMetadata metadata, ScriptItem scriptItem = null)
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

                // Prioritize custom icon from layout, fallback to capability icon
                System.Windows.Media.ImageSource iconToUse = null;
                if (!string.IsNullOrEmpty(scriptItem?.IconPath))
                {
                    iconToUse = LoadIcon(scriptItem.IconPath, 32);
                }

                // Use capability icon if no custom icon
                if (iconToUse == null)
                {
                    var capabilityIconPath = GetCapabilityIcon(metadata.CapabilityLevel);
                    if (!string.IsNullOrEmpty(capabilityIconPath))
                    {
                        iconToUse = LoadIcon(capabilityIconPath, 32);
                    }
                }

                // Apply icon (PyRevit stacking will handle sizing automatically)
                if (iconToUse != null)
                {
                    buttonData.LargeImage = iconToUse;
                    buttonData.Image = iconToUse; // Set both for compatibility
                }

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
                // Use the helper method to create button data
                var buttonData = CreatePushButtonDataForScript(metadata);
                if (buttonData == null)
                {
                    return null;
                }

                // Add button to panel
                var button = panel.AddItem(buttonData) as PushButton;

                var displayName = FormatScriptName(metadata.Name);
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
            if (string.IsNullOrEmpty(iconPath) || !File.Exists(iconPath))
            {
                _logger.LogWarning($"Icon path is null, empty, or file does not exist: {iconPath}");
                return null;
            }

            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);

                // Set pixel size for performance and to match button size
                bitmap.DecodePixelWidth = size;
                bitmap.DecodePixelHeight = size;

                bitmap.EndInit();
                bitmap.Freeze(); // Freeze for performance and cross-thread safety
                return bitmap;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to load icon from path: {iconPath}", ex);
                return null; // Return null on failure to allow fallback
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

                // Auto-download scripts if no settings AND no cached scripts (first run)
                if (!hasSettings && !hasCache)
                {
                    _logger.Log("🚀 First run detected - will auto-download scripts without popup");

                    // Schedule automatic download after UI initialization
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                        new Action(AttemptFirstRunAutoDownload));
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
        /// 🚀 Attempt automatic first-run download without popup dialog
        /// </summary>
        private async void AttemptFirstRunAutoDownload()
        {
            try
            {
                _logger.Log("🚀 Starting automatic first-run script download...");

                // Save hardcoded settings first (same as FirstRunWizard)
                await SaveHardcodedGitHubSettings();

                // Start automatic download without blocking UI
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Create cancellation token with 60-second timeout
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

                        // Try to refresh cache automatically
                        var success = await _gitCacheManager.RefreshCacheAsync(forceRefresh: true, cancellationToken: cts.Token);

                        if (success)
                        {
                            _logger.Log("✅ Automatic first-run download completed successfully");

                            // Refresh the ribbon to show downloaded scripts (on UI thread)
                            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                                System.Windows.Threading.DispatcherPriority.Background,
                                new Action(() => RefreshRibbonWithGitHubScripts()));

                            _logger.Log("🎯 First-run auto-download complete - scripts are now available");
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ Automatic first-run download failed - will use offline mode");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error during automatic first-run download: {ex.Message}");
                    }
                });

                _logger.Log("🚀 Automatic first-run download task started - UI remains responsive");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting automatic first-run download: {ex.Message}");
            }
        }

        /// <summary>
        /// 💾 Save hardcoded GitHub settings for first-run auto-download
        /// </summary>
        private async Task SaveHardcodedGitHubSettings()
        {
            try
            {
                var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tycoon", "github-settings.json");
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));

                var settings = new
                {
                    RepositoryOwner = "Jrandolph3110",
                    RepositoryName = "tycoon-ai-bim-platform",
                    Branch = "main",
                    ScriptsPath = "scripts",
                    LastUpdated = DateTime.UtcNow.ToString("O")
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(settingsPath, json);

                _logger.Log("💾 Saved hardcoded GitHub settings for auto-download");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save hardcoded GitHub settings", ex);
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

                    // Create metadata for GitHub script - capability level doesn't matter since auto-sorting is disabled
                    var metadata = new ScriptMetadata
                    {
                        Name = fileName, // CRITICAL: Use clean name without prefix
                        Description = $"GitHub script: {relativePath}",
                        Author = "GitHub Repository",
                        Version = "Latest",
                        CapabilityLevel = ScriptCapabilityLevel.P2_Analytic, // Default - not used since auto-sorting disabled
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

        #region Unified Event-Driven System

        /// <summary>
        /// 🔄 Refresh ribbon using ScriptService data
        /// Called by ScriptService event handlers when scripts are updated
        /// </summary>
        private void RefreshRibbonViaEvents()
        {
            try
            {
                _logger.Log("🔄 Refreshing ribbon via ScriptService event system");

                // Update script metadata from ScriptService
                var localScripts = ScriptService.Instance.GetCurrentLocalScripts();
                var githubScripts = ScriptService.Instance.GetCurrentGitHubScripts();
                UpdateScriptMetadataFromScriptService(localScripts, githubScripts);

                // Generate fresh layout with current script metadata
                var layout = _layoutManager.MergeLayouts(_scriptMetadata);
                _logger.Log($"🔄 Generated refresh layout with {layout.Panels.Count} panels");

                // Apply layout via unified system
                ApplyLayoutToRibbon(layout);

                _logger.Log("🔄 Ribbon refresh complete");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to refresh ribbon via ScriptService", ex);
                throw;
            }
        }



        #endregion

        #region Event Handlers

        /// <summary>
        /// 📡 Handle LayoutChanged events from Layout Manager (Chat's event-driven architecture)
        /// </summary>
        private void OnLayoutChanged(LayoutChangedEvent layoutEvent)
        {
            try
            {
                _logger.Log($"📡 Received LayoutChanged event from {layoutEvent.Source} at {layoutEvent.Timestamp}");
                _logger.Log($"📡 Layout has {layoutEvent.Layout.Panels.Count} panels with {layoutEvent.Layout.Panels.Sum(p => p.Stacks.Count)} total stacks");

                // 🔥 CRITICAL: Apply the layout changes to the actual ribbon
                _logger.Log("🔥 Applying layout changes to ribbon (Chat's RibbonRefresher pattern)");
                ApplyLayoutToRibbon(layoutEvent.Layout);

                _logger.Log("✅ Layout changes applied to ribbon successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to handle LayoutChanged event", ex);
            }
        }

        /// <summary>
        /// 🔥 Apply layout changes to the actual Revit ribbon (Chat's RibbonRefresher)
        /// </summary>
        private void ApplyLayoutToRibbon(RibbonLayoutSchema layout)
        {
            try
            {
                _logger.Log("🔥 RIBBON REFRESHER: Starting layout application to ribbon");

                // Hide all existing dynamic buttons first
                HideDynamicButtons();

                // Apply the new layout by creating buttons in the correct panels
                foreach (var panelLayout in layout.Panels)
                {
                    var ribbonPanel = GetRibbonPanelById(panelLayout.Id);
                    if (ribbonPanel == null)
                    {
                        _logger.LogWarning($"🔥 RIBBON REFRESHER: No ribbon panel found for layout panel '{panelLayout.Id}'");
                        continue;
                    }

                    _logger.Log($"🔥 RIBBON REFRESHER: Applying layout to panel '{panelLayout.Id}' with {panelLayout.Stacks.Count} stacks");

                    foreach (var stackLayout in panelLayout.Stacks.OrderBy(s => s.Order))
                    {
                        CreateStackFromLayout(ribbonPanel, stackLayout);
                    }
                }

                _logger.Log($"🔥 RIBBON REFRESHER: Layout application complete - {_dynamicButtons.Count} buttons configured");
            }
            catch (Exception ex)
            {
                _logger.LogError("RIBBON REFRESHER: Failed to apply layout to ribbon", ex);
                throw;
            }
        }

        #endregion



        #region ScriptService Event Handlers

        /// <summary>
        /// 🎯 Create initial layout from ScriptService (immediate, bundled scripts)
        /// </summary>
        private void CreateInitialLayoutFromScriptService()
        {
            try
            {
                _logger.Log("🎯 Creating initial layout from ScriptService");

                // Get current scripts from ScriptService (bundled scripts available immediately)
                var localScripts = ScriptService.Instance.GetCurrentLocalScripts();
                var githubScripts = ScriptService.Instance.GetCurrentGitHubScripts();

                _logger.Log($"🎯 Initial layout: {localScripts.Count()} local, {githubScripts.Count()} GitHub scripts");

                // Convert to legacy format for Layout Manager compatibility
                UpdateScriptMetadataFromScriptService(localScripts, githubScripts);

                // Generate and apply layout
                var layout = _layoutManager.MergeLayouts(_scriptMetadata);
                ApplyLayoutToRibbon(layout);

                _logger.Log("🎯 Initial layout created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create initial layout from ScriptService", ex);
                throw;
            }
        }

        /// <summary>
        /// 🎯 Handle local scripts updated from ScriptService
        /// </summary>
        private void OnLocalScriptsUpdated(IEnumerable<ScriptViewModel> scripts)
        {
            try
            {
                _logger.Log($"🎯 Local scripts updated: {scripts.Count()} scripts");

                // Update layout with new local scripts
                var githubScripts = ScriptService.Instance.GetCurrentGitHubScripts();
                UpdateScriptMetadataFromScriptService(scripts, githubScripts);
                RefreshRibbonViaEvents();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to handle local scripts update", ex);
            }
        }

        /// <summary>
        /// 🎯 Handle GitHub scripts updated from ScriptService
        /// </summary>
        private void OnGitHubScriptsUpdated(IEnumerable<ScriptViewModel> scripts)
        {
            try
            {
                _logger.Log($"🎯 GitHub scripts updated: {scripts.Count()} scripts");

                // Update layout with new GitHub scripts
                var localScripts = ScriptService.Instance.GetCurrentLocalScripts();
                UpdateScriptMetadataFromScriptService(localScripts, scripts);
                RefreshRibbonViaEvents();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to handle GitHub scripts update", ex);
            }
        }

        /// <summary>
        /// 🎯 Handle update status changed from ScriptService
        /// </summary>
        private void OnUpdateStatusChanged(string status)
        {
            try
            {
                _logger.Log($"🎯 Update status: {status}");
                // Could update UI status indicators here in the future
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to handle update status change", ex);
            }
        }

        /// <summary>
        /// 🎯 Handle updating state changed from ScriptService
        /// </summary>
        private void OnIsUpdatingChanged(bool isUpdating)
        {
            try
            {
                _logger.Log($"🎯 Updating state: {(isUpdating ? "Started" : "Completed")}");
                // Could update UI loading indicators here in the future
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to handle updating state change", ex);
            }
        }

        /// <summary>
        /// 🎯 Convert ScriptService ViewModels to legacy ScriptMetadata format
        /// Maintains compatibility with existing Layout Manager
        /// </summary>
        private void UpdateScriptMetadataFromScriptService(IEnumerable<ScriptViewModel> localScripts, IEnumerable<ScriptViewModel> githubScripts)
        {
            try
            {
                _scriptMetadata.Clear();

                // Add local scripts
                foreach (var script in localScripts)
                {
                    var metadata = new ScriptMetadata
                    {
                        Name = script.Name,
                        FilePath = script.Command,
                        Description = script.Description,
                        CapabilityLevel = ScriptCapabilityLevel.P1_Deterministic, // Default for local
                        IsGitHubScript = false,
                        LastModified = DateTime.Now,
                        Author = "Local",
                        SchemaVersion = "1.0.0"
                    };

                    _scriptMetadata[script.Command] = metadata;
                }

                // Add GitHub scripts
                foreach (var script in githubScripts)
                {
                    var metadata = new ScriptMetadata
                    {
                        Name = script.Name,
                        FilePath = script.Command,
                        Description = script.Description,
                        CapabilityLevel = ScriptCapabilityLevel.P2_Analytic, // Default for GitHub
                        IsGitHubScript = true,
                        LastModified = DateTime.Now,
                        Author = "GitHub",
                        SchemaVersion = "1.0.0"
                    };

                    _scriptMetadata[script.Command] = metadata;
                }

                _logger.Log($"🎯 Updated script metadata: {localScripts.Count()} local + {githubScripts.Count()} GitHub = {_scriptMetadata.Count} total");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to update script metadata from ScriptService", ex);
            }
        }

        #endregion

    }
}
