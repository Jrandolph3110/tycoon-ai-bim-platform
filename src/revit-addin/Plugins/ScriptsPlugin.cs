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
using TycoonRevitAddin.Scripting;
using Tycoon.Scripting.Contracts;

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
    /// 📜 Scripts Plugin - Unified Script Architecture Implementation
    /// Uses ScriptEngine with AppDomain isolation for hot-reload capability
    /// </summary>
    public class ScriptsPlugin : PluginBase
    {
        // 🚧 LEGACY: Keeping for compatibility during transition
        private readonly string _scriptsPath;
        private readonly Dictionary<string, DateTime> _scriptModificationTimes;
        private readonly List<PushButton> _scriptButtons;
        private readonly Dictionary<string, ScriptMetadata> _scriptMetadata;
        private readonly Dictionary<ScriptCapabilityLevel, List<PushButton>> _buttonsByCapability;

        // ✨ NEW: Unified Script Architecture
        private ScriptEngine _scriptEngine;
        private readonly Dictionary<string, PushButton> _unifiedScriptButtons;

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

        // 🔄 Safe Ribbon Refresh Mechanism (ExternalEvent pattern)
        private readonly object _refreshLock = new object();
        private bool _isRefreshPending = false;
        private ExternalEvent _ribbonRefreshEvent;

        public override string Id => "scripts";
        public override string Name => "Scripts";
        public override string Description => "PyRevit-style script execution and management";
        public override string Version => "1.0.0";
        public override string IconPath => "Resources/ScriptsIcon.png";

        public ScriptsPlugin(Logger logger) : base(logger)
        {
            // 🚧 LEGACY: Set up scripts directory for compatibility
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _scriptsPath = Path.Combine(appDataPath, "Tycoon", "Scripts");

            // 🚧 LEGACY: Initialize legacy collections for compatibility
            _scriptModificationTimes = new Dictionary<string, DateTime>();
            _scriptButtons = new List<PushButton>();
            _scriptMetadata = new Dictionary<string, ScriptMetadata>();
            _buttonsByCapability = new Dictionary<ScriptCapabilityLevel, List<PushButton>>
            {
                { ScriptCapabilityLevel.P1_Deterministic, new List<PushButton>() },
                { ScriptCapabilityLevel.P2_Analytic, new List<PushButton>() },
                { ScriptCapabilityLevel.P3_Adaptive, new List<PushButton>() }
            };

            // ✨ NEW: Initialize unified script architecture
            _unifiedScriptButtons = new Dictionary<string, PushButton>();
            _scriptEngine = new ScriptEngine(_logger);

            // 🔥 Initialize PyRevit-Style Hot-Reload Infrastructure
            _activePanels = new Dictionary<string, RibbonPanel>();
            _dynamicButtons = new Dictionary<string, PushButton>();
            _dynamicStacks = new List<IList<RibbonItem>>();

            // 🎯 Initialize Chat's Layout Management System
            _layoutManager = new RibbonLayoutManager(logger);

            // 🔄 Initialize GitHub Cache Management System
            _gitCacheManager = new GitCacheManager(logger);

            // 🚧 DISABLED: Legacy ScriptService initialization removed
            // ScriptService.Instance.Initialize(_gitCacheManager, logger, _scriptsPath);
            // This will be replaced with the new ScriptEngine initialization

            // 📡 Subscribe to ScriptService events for UI updates
            ScriptService.Instance.LocalScriptsUpdated += OnLocalScriptsUpdated;
            ScriptService.Instance.GitHubScriptsUpdated += OnGitHubScriptsUpdated;
            ScriptService.Instance.UpdateStatusChanged += OnUpdateStatusChanged;
            ScriptService.Instance.IsUpdatingChanged += OnIsUpdatingChanged;

            // 📡 Subscribe to Layout Manager events (Chat's event-driven architecture)
            EventBus.Instance.Subscribe<LayoutChangedEvent>(OnLayoutChanged);
            _logger.Log("📡 Subscribed to LayoutChanged events from Layout Manager");

            // 🚧 DISABLED: Legacy scripts directory creation removed
            // EnsureScriptsDirectory(); // Disabled during unified architecture implementation

            _logger.Log("🎯 ScriptsPlugin initialized with async-first ScriptService architecture");
        }

        public override List<RibbonPanel> Initialize(UIControlledApplication application, string tabName)
        {
            // Initialize ExternalEvent for safe ribbon refresh
            _ribbonRefreshEvent = ExternalEvent.Create(new RibbonRefreshEventHandler(this, _logger));
            _logger.Log("🔄 ExternalEvent created for safe ribbon refresh");

            // Call base initialization
            var panels = base.Initialize(application, tabName);

            // ✨ Initialize ScriptEngine with Development mode for efficient workflow
            InitializeScriptEngineAsync();

            return panels;
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
            // UI shows cached GitHub scripts immediately, updates asynchronously when GitHub refresh completes
            try
            {
                _logger.Log("🎯 Initializing ribbon with async-first ScriptService architecture");

                // Create initial layout from cached GitHub scripts (immediate)
                CreateInitialLayoutFromScriptService();

                _logger.Log("✅ Ribbon initialized with cached scripts - GitHub update will follow asynchronously");
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
        /// ✨ Initialize ScriptEngine with Development mode for efficient workflow
        /// </summary>
        private async void InitializeScriptEngineAsync()
        {
            try
            {
                _logger.Log("🚀 Initializing ScriptEngine with unified architecture");
                System.Diagnostics.Debug.WriteLine("🚀 DEBUG: Initializing ScriptEngine with unified architecture");
                System.Console.WriteLine("🚀 CONSOLE: Initializing ScriptEngine with unified architecture");

                // Configure for Development mode (your workflow)
                var config = new ScriptEngineConfig
                {
                    DevelopmentPath = @"C:\RevitAI\tycoon-ai-bim-platform\test-scripts",
                    GitHubConfig = new GitHubScriptConfig
                    {
                        RepositoryUrl = "https://github.com/your-org/scripts", // TODO: Update with actual repo
                        CachePath = Path.Combine(Environment.GetFolderPath(
                            Environment.SpecialFolder.ApplicationData),
                            "Tycoon", "UnifiedScriptCache"),
                        CacheExpiry = TimeSpan.FromHours(24)
                    }
                };

                // Initialize in Development mode for hot-reload
                await _scriptEngine.InitializeAsync(ScriptEngineMode.Development, config);

                // Subscribe to script changes for ribbon updates
                _scriptEngine.ScriptsChanged += OnUnifiedScriptsChanged;

                // 🎯 TRIGGER INITIAL AUTO-POPULATION: Force initial script discovery and population
                await TriggerInitialScriptPopulation();

                _logger.Log("✅ ScriptEngine initialized successfully in Development mode");
                System.Diagnostics.Debug.WriteLine("✅ DEBUG: ScriptEngine initialized successfully in Development mode");
                System.Console.WriteLine("✅ CONSOLE: ScriptEngine initialized successfully in Development mode");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize ScriptEngine", ex);
                System.Diagnostics.Debug.WriteLine($"❌ DEBUG: ScriptEngine initialization failed: {ex.Message}");
                System.Console.WriteLine($"❌ CONSOLE: ScriptEngine initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 🎯 Trigger initial script population after ScriptEngine initialization
        /// This ensures scripts are auto-populated on startup without waiting for changes
        /// </summary>
        private async Task TriggerInitialScriptPopulation()
        {
            try
            {
                _logger.Log("🎯 Triggering initial script population after ScriptEngine initialization");

                // Get current scripts from ScriptEngine
                var scripts = _scriptEngine.GetLoadedScripts();
                if (scripts != null && scripts.Count > 0)
                {
                    _logger.Log($"🎯 Found {scripts.Count} scripts during initial population");

                    // Trigger auto-population with current scripts
                    OnUnifiedScriptsChanged(scripts);
                }
                else
                {
                    _logger.Log("⚠️ No scripts found during initial population - will wait for script discovery");

                    // Force script refresh to trigger discovery
                    await _scriptEngine.RefreshScriptsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to trigger initial script population", ex);
            }
        }

        /// <summary>
        /// 🚧 LEGACY: Create Production Script Buttons (Chat's P1-Deterministic)
        /// This method is disabled during unified architecture implementation
        /// </summary>
        private void CreateProductionScriptButtons(RibbonPanel panel)
        {
            // 🚧 DISABLED: Legacy P# auto-placement system removed
            // This will be replaced with the new ScriptEngine manifest-based system
            _logger.Log("🚧 Legacy CreateProductionScriptButtons disabled - awaiting unified architecture");
        }

        /// <summary>
        /// 🚧 LEGACY: Create Smart Tools Buttons (Chat's P2-Analytic + P3-Adaptive)
        /// This method is disabled during unified architecture implementation
        /// </summary>
        private void CreateSmartToolsButtons(RibbonPanel panel)
        {
            // 🚧 DISABLED: Legacy P# auto-placement system removed
            // This will be replaced with the new ScriptEngine manifest-based system
            _logger.Log("🚧 Legacy CreateSmartToolsButtons disabled - awaiting unified architecture");
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
        /// 🎯 Create Dynamic Script Button (Public method for Application.CreateDynamicButtonsOnIdle)
        /// </summary>
        public void CreateDynamicScriptButton(RibbonPanel panel, TycoonRevitAddin.Scripting.ScriptInfo script)
        {
            CreateUnifiedScriptButton(panel, script);
        }

        /// <summary>
        /// Get the appropriate command class for a script based on its name
        /// </summary>
        private string GetCommandClassForScript(string scriptName)
        {
            switch (scriptName)
            {
                case "Element Counter":
                    return "TycoonRevitAddin.Commands.ElementCounterScriptCommand";
                case "Hello World":
                    return "TycoonRevitAddin.Commands.HelloWorldScriptCommand";
                default:
                    // Fallback to generic unified command
                    return "TycoonRevitAddin.Commands.UnifiedScriptCommand";
            }
        }

        /// <summary>
        /// ✨ Create a unified script button for the ribbon
        /// </summary>
        private void CreateUnifiedScriptButton(RibbonPanel panel, TycoonRevitAddin.Scripting.ScriptInfo script)
        {
            try
            {
                var buttonId = $"UnifiedScript_{script.Manifest.Name.Replace(" ", "")}";

                // Skip if button already exists
                if (_unifiedScriptButtons.ContainsKey(buttonId))
                {
                    return;
                }

                // Use specific command class based on script name for proper button-to-script mapping
                var commandClass = GetCommandClassForScript(script.Manifest.Name);

                var button = AddPushButton(
                    panel,
                    buttonId,
                    script.Manifest.Name,
                    commandClass,
                    $"🎯 {script.Manifest.Name}\n{script.Manifest.Description}\nAuthor: {script.Manifest.Author}",
                    "ScriptIcon.png" // Default script icon path
                );

                // 🎯 CRITICAL: Script name stored in registry for UnifiedScriptCommand to retrieve
                // Note: PushButton doesn't have Tag property, using registry approach instead

                // Store script info in button's tag for execution
                button.ToolTip = $"🎯 {script.Manifest.Name}\n" +
                               $"{script.Manifest.Description}\n" +
                               $"Author: {script.Manifest.Author}\n" +
                               $"Version: {script.Manifest.Version}\n" +
                               $"Source: {script.Source}";

                // Store script name for execution (using ToolTip as storage since Tag doesn't exist)
                // The script name will be extracted from the button ID in UnifiedScriptCommand

                _unifiedScriptButtons[buttonId] = button;

                // 🎯 CRITICAL: Register button with UnifiedScriptCommand for hot-reload support
                TycoonRevitAddin.Commands.UnifiedScriptCommand.RegisterButtonScript(buttonId, script.Manifest.Name);

                _logger.Log($"✅ Created unified script button: {script.Manifest.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create unified script button for {script.Manifest.Name}", ex);
            }
        }

        /// <summary>
        /// 🚧 LEGACY: Ensure scripts directory exists and create sample scripts
        /// This method is disabled during unified architecture implementation
        /// </summary>
        private void EnsureScriptsDirectory()
        {
            // 🚧 DISABLED: Legacy local scripts directory system removed
            // The new ScriptEngine will handle script discovery through manifests
            _logger.Log("🚧 Legacy EnsureScriptsDirectory disabled - awaiting unified ScriptEngine");
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
        /// ✨ Handle unified script changes from ScriptEngine (Startup auto-population)
        /// </summary>
        private void OnUnifiedScriptsChanged(List<TycoonRevitAddin.Scripting.ScriptInfo> scripts)
        {
            try
            {
                _logger.Log($"🔄 Unified scripts changed: {scripts.Count} scripts available (startup auto-population)");
                System.Diagnostics.Debug.WriteLine($"🔄 DEBUG: Script changes detected: {scripts.Count} scripts available");
                System.Console.WriteLine($"🔄 CONSOLE: Script changes detected: {scripts.Count} scripts available");

                // 🎯 SIMPLIFIED: Only auto-populate during startup, not real-time changes
                // This avoids complex UI thread context issues while providing the desired functionality
                UpdateRibbonWithUnifiedScripts(scripts);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to handle unified script changes", ex);
            }
        }



        /// <summary>
        /// ✨ Update ribbon panels with unified scripts (STARTUP AUTO-POPULATION)
        /// Re-enabled for startup script discovery - works in valid UI context during initialization
        /// </summary>
        private void UpdateRibbonWithUnifiedScripts(List<TycoonRevitAddin.Scripting.ScriptInfo> scripts)
        {
            try
            {
                _logger.Log($"🎯 Auto-populating scripts during startup: {scripts.Count} scripts discovered");

                // Clear existing unified script buttons and registry
                foreach (var button in _unifiedScriptButtons.Values)
                {
                    // Note: Revit doesn't allow removing buttons, so we'll disable them
                    button.Enabled = false;
                }
                _unifiedScriptButtons.Clear();

                // Clear the UnifiedScriptCommand registry for fresh script registration
                TycoonRevitAddin.Commands.UnifiedScriptCommand.ClearRegistry();

                // Find Production panel for script buttons
                var productionPanel = _panels.FirstOrDefault(p => p.Name.Contains("Production"));
                if (productionPanel == null)
                {
                    _logger.LogWarning($"Production panel not found for startup script auto-population. Available panels: {string.Join(", ", _panels.Select(p => p.Name))}");
                    return;
                }

                // Add buttons for each script (during startup - valid UI context)
                foreach (var script in scripts.Take(8)) // Limit for ribbon space
                {
                    CreateUnifiedScriptButton(productionPanel, script);
                }

                _logger.Log($"✅ Startup auto-population completed: {_unifiedScriptButtons.Count} script buttons created");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to auto-populate scripts during startup", ex);
            }
        }

        /// <summary>
        /// 🚧 LEGACY: PyRevit-Style Hot-Reload Implementation
        /// This method is disabled during unified architecture implementation
        /// </summary>
        public void RefreshScripts()
        {
            // 🚧 DISABLED: Legacy hot-reload system removed
            // This will be replaced with the new ScriptEngine AppDomain-based hot-reload
            _logger.Log("🚧 Legacy RefreshScripts disabled - awaiting unified ScriptEngine implementation");
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
        /// 🔄 Safely requests a ribbon refresh on the Revit UI thread
        /// This method is thread-safe and can be called from any context
        /// </summary>
        public void RequestRibbonRefresh()
        {
            lock (_refreshLock)
            {
                if (_isRefreshPending) return; // A refresh is already queued

                _logger.Log("🔄 Received request for ribbon refresh. Raising ExternalEvent.");
                _isRefreshPending = true;

                // Raise ExternalEvent for safe UI updates
                _ribbonRefreshEvent?.Raise();
            }
        }

        /// <summary>
        /// 🔄 Internal method called by ExternalEvent to perform ribbon refresh
        /// </summary>
        internal void ExecuteRibbonRefresh()
        {
            lock (_refreshLock)
            {
                _isRefreshPending = false;
            }

            _logger.Log("🔄 ExternalEvent fired. Proceeding with ribbon refresh in valid Revit context.");

            // Execute the actual refresh logic in valid Revit context
            RefreshRibbonViaEvents();
        }

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
        /// 🎯 Create initial layout from ScriptService (immediate, cached GitHub scripts)
        /// </summary>
        private void CreateInitialLayoutFromScriptService()
        {
            try
            {
                _logger.Log("🎯 Creating initial layout from ScriptService");

                // Get current scripts from ScriptService (cached GitHub scripts available immediately)
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
                RequestRibbonRefresh(); // Use safe refresh method
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
                RequestRibbonRefresh(); // Use safe refresh method
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

        #region Unified Script Architecture Public API

        /// <summary>
        /// ✨ Get the ScriptEngine for external access (e.g., from commands)
        /// </summary>
        public ScriptEngine GetScriptEngine()
        {
            return _scriptEngine;
        }

        /// <summary>
        /// ✨ Execute a script by name through the unified architecture
        /// </summary>
        public async Task<ScriptExecutionResult> ExecuteUnifiedScriptAsync(string scriptName, UIApplication uiApp, Autodesk.Revit.DB.Document doc)
        {
            try
            {
                if (_scriptEngine == null)
                {
                    return new ScriptExecutionResult
                    {
                        Success = false,
                        ErrorMessage = "ScriptEngine not initialized"
                    };
                }

                // The ScriptProxy will be initialized with Revit context during execution
                // This is handled internally by the ScriptEngine
                return await _scriptEngine.ExecuteScriptAsync(scriptName);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to execute unified script '{scriptName}'", ex);
                return new ScriptExecutionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        #endregion

    }

    /// <summary>
    /// 🔄 ExternalEvent handler for safe ribbon refresh operations
    /// </summary>
    internal class RibbonRefreshEventHandler : IExternalEventHandler
    {
        private readonly ScriptsPlugin _plugin;
        private readonly Logger _logger;

        public RibbonRefreshEventHandler(ScriptsPlugin plugin, Logger logger)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Execute(UIApplication app)
        {
            try
            {
                _logger.Log("🔄 RibbonRefreshEventHandler executing in valid Revit context");
                _plugin.ExecuteRibbonRefresh();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to execute ribbon refresh via ExternalEvent", ex);
            }
        }

        public string GetName()
        {
            return "TycoonRibbonRefreshEvent";
        }
    }
}
