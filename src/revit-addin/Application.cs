using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using TycoonRevitAddin.Communication;
using TycoonRevitAddin.Services;
using TycoonRevitAddin.UI;
using TycoonRevitAddin.Plugins;
using System.Linq;
using TycoonRevitAddin.Utils;
using TycoonRevitAddin.Scripting;

namespace TycoonRevitAddin
{
    /// <summary>
    /// Tycoon AI-BIM Platform Revit Application
    /// 
    /// Provides:
    /// - Live AI-Revit integration
    /// - Real-time selection context
    /// - FLC steel framing workflows
    /// - Hot-reload development capabilities
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Application : IExternalApplication
    {
        // ✅ RE-ENABLED: Communication layer is cornerstone for AI-Revit manipulation
        private static TycoonBridge _tycoonBridge;

        // 🎯 CLEAN ARCHITECTURE: Legacy dynamic button creation infrastructure removed
        // Script buttons are now created automatically during startup by RibbonManager
        private static UIControlledApplication _uiControlledApp;
        private static Logger _logger;
        // ✅ RE-ENABLED: Communication layer is cornerstone for AI-Revit manipulation
        private static StatusPollingService _statusService;
        private static DynamicRibbonManager _ribbonManager;
        private static PushButton _connectButton;

        // 🔥 HOT-RELOAD: Clean RibbonManager for script hot-reload functionality
        private static TycoonRevitAddin.Scripting.RibbonManager _cleanRibbonManager;
        private static PluginManager _pluginManager;

        /// <summary>
        /// Public access to logger
        /// </summary>
        public static Logger Logger => _logger;

        /// <summary>
        /// Public access to Tycoon bridge - cornerstone for AI-Revit manipulation
        /// </summary>
        public static TycoonBridge TycoonBridge => _tycoonBridge;

        /// <summary>
        /// Public access to Plugin Manager
        /// </summary>
        public static PluginManager PluginManager => _pluginManager;

        /// <summary>
        /// Public access to status service
        /// </summary>
        public static StatusPollingService StatusService => _statusService;

        /// <summary>
        /// Public access to ribbon manager
        /// </summary>
        public static DynamicRibbonManager RibbonManager => _ribbonManager;

        /// <summary>
        /// Public access to clean ribbon manager for hot-reload
        /// </summary>
        public static TycoonRevitAddin.Scripting.RibbonManager CleanRibbonManager => _cleanRibbonManager;

        /// <summary>
        /// Application startup
        /// </summary>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Store reference for dynamic button creation
                _uiControlledApp = application;

                // Initialize logger first (safely)
                _logger = new Logger("Tycoon", debugMode: true);
                _logger.Log("🚀 Starting Tycoon AI-BIM Platform v0.17.0 (AI Actions System - Revolutionary AI Automation)...");

                // Initialize plugin manager
                _pluginManager = new PluginManager(_logger);
                _logger.Log("🔌 PluginManager created");

                // Create ribbon tab and panels with plugin system
                CreateRibbonInterface(application);

                // 🎯 CLEAN ARCHITECTURE: Script system now handled by ScriptsPlugin
                // InitializeCleanScriptSystem(application); // Disabled to prevent tab conflicts

                // Initialize Tycoon bridge - cornerstone for AI-Revit manipulation
                _tycoonBridge = new TycoonBridge();
                _logger.Log("🔗 TycoonBridge initialized");

                // TODO: Initialize status services after ribbon creation
                // Temporarily disabled to prevent crashes
                _logger.Log("⚠️ Status services temporarily disabled for stability");

                // Setup document event handlers
                SetupEventHandlers(application);
                _logger.Log("🔗 Event handlers registered");

                // Check if a document is already open
                // Note: We'll handle this in the document opened event instead
                // since we don't have direct access to UIApplication here

                _logger.Log("✅ Tycoon AI-BIM Platform initialized successfully");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                _logger?.LogError("Failed to initialize Tycoon", ex);
                TaskDialog.Show("Tycoon Error", ex.Message);
                return Result.Failed;
            }
        }

        /// <summary>
        /// 🎯 NEW: Clean, simple script system initialization
        /// Single-flow architecture: Discover → Organize → Build
        /// Replaces the complex ScriptsPlugin/Application dual system
        /// </summary>
        private void InitializeCleanScriptSystem(UIControlledApplication application)
        {
            try
            {
                _logger.Log("🎯 Initializing clean script system...");

                // 1. DISCOVER: Find all script definitions
                var scriptDiscovery = new TycoonRevitAddin.Scripting.ScriptDiscoveryService(_logger);
                var scriptDirectory = TycoonRevitAddin.Scripting.ScriptDiscoveryService.GetDefaultScriptDirectory();
                var scripts = scriptDiscovery.DiscoverScripts(scriptDirectory);

                if (scripts.Count == 0)
                {
                    _logger.Log("⚠️ No scripts found - ribbon will be empty");
                    return;
                }

                // 2. BUILD: Create ribbon UI from script definitions
                _cleanRibbonManager = new TycoonRevitAddin.Scripting.RibbonManager(application, _logger);
                _cleanRibbonManager.BuildRibbon(scripts, "TycoonAI");

                _logger.Log($"✅ Clean script system initialized with {scripts.Count} scripts (hot-reload enabled)");
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Failed to initialize clean script system", ex);
            }
        }

        /// <summary>
        /// Application shutdown
        /// </summary>
        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                _logger?.Log("🛑 Shutting down Tycoon AI-BIM Platform...");

                // Dispose plugin manager
                _pluginManager?.Dispose();

                // 🚧 TEMPORARILY DISABLED: Communication layer causing build issues
                // Disconnect from MCP server
                // _tycoonBridge?.Disconnect();

                _logger?.Log("✅ Tycoon shutdown complete");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                _logger?.LogError("Shutdown error", ex);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Create Tycoon ribbon interface with plugin system
        /// </summary>
        private void CreateRibbonInterface(UIControlledApplication application)
        {
            // Create Tycoon tab
            string tabName = "Tycoon AI-BIM";
            application.CreateRibbonTab(tabName);

            // Create AI Integration panel (always visible)
            RibbonPanel aiPanel = application.CreateRibbonPanel(tabName, "AI Integration");
            CreateAIIntegrationButtons(aiPanel);

            // Initialize plugin manager FIRST (this registers the plugins)
            _pluginManager.Initialize(application, tabName);

            // Create Plugin Control panel AFTER plugins are registered
            RibbonPanel pluginControlPanel = application.CreateRibbonPanel(tabName, "Plugin Control");
            CreatePluginControlButtons(pluginControlPanel);

            // Activate the first plugin by default
            var enabledPlugins = _pluginManager.GetEnabledPlugins();
            if (enabledPlugins.Any())
            {
                _pluginManager.ActivatePlugin(enabledPlugins.First().Id);
            }

            _logger?.Log("✅ Plugin-based ribbon interface created");
        }

        /// <summary>
        /// Create AI Integration buttons (always visible)
        /// </summary>
        private void CreateAIIntegrationButtons(RibbonPanel panel)
        {
            // Add Copy MCP Config button
            PushButtonData copyConfigButtonData = new PushButtonData(
                "TycoonCopyConfig",
                "Copy MCP\nConfig",
                Assembly.GetExecutingAssembly().Location,
                "TycoonRevitAddin.Commands.CopyMCPConfigCommand"
            );
            copyConfigButtonData.ToolTip = "Copy MCP configuration JSON to clipboard for AI assistant setup";

            // Add Connect button
            PushButtonData connectButtonData = new PushButtonData(
                "TycoonConnect",
                "Connect\nto AI",
                Assembly.GetExecutingAssembly().Location,
                "TycoonRevitAddin.Commands.TycoonCommand"
            );

            connectButtonData.ToolTip = "Connect to Tycoon AI-BIM Server";
            connectButtonData.LongDescription = "Establish connection to Tycoon MCP Server for real-time AI-Revit integration";

            // Set button icons (placeholder paths)
            try
            {
                connectButtonData.LargeImage = LoadImage("TycoonIcon32.png");
                connectButtonData.Image = LoadImage("TycoonIcon16.png");
            }
            catch
            {
                // Icons not found, continue without them
            }

            PushButton copyConfigButton = panel.AddItem(copyConfigButtonData) as PushButton;
            PushButton connectButton = panel.AddItem(connectButtonData) as PushButton;

            _logger?.Log($"📋 Created AI Integration buttons - Copy Config: {copyConfigButton != null}, Connect: {connectButton != null}");

            // Store connect button for later registration with ribbon manager
            _connectButton = connectButton;
        }

        /// <summary>
        /// Create Plugin Control buttons (always visible)
        /// </summary>
        private void CreatePluginControlButtons(RibbonPanel panel)
        {
            // Plugin Selector dropdown
            var pluginSelectorData = new ComboBoxData("PluginSelector");
            var pluginSelector = panel.AddItem(pluginSelectorData) as ComboBox;

            // Populate with available plugins
            var enabledPlugins = _pluginManager.GetEnabledPlugins();
            _logger?.Log($"🔍 Found {enabledPlugins.Count} enabled plugins for dropdown");

            foreach (var plugin in enabledPlugins)
            {
                var memberData = new ComboBoxMemberData(plugin.Id, plugin.Name);
                memberData.ToolTip = plugin.Description;
                pluginSelector.AddItem(memberData);
                _logger?.Log($"➕ Added plugin to dropdown: {plugin.Name} (ID: {plugin.Id})");
            }

            // Set event handler for plugin selection
            pluginSelector.CurrentChanged += OnPluginSelectorChanged;

            // Plugin Info button
            PushButtonData pluginInfoData = new PushButtonData(
                "PluginInfo",
                "Plugin\nInfo",
                Assembly.GetExecutingAssembly().Location,
                "TycoonRevitAddin.Commands.PluginInfoCommand"
            );
            pluginInfoData.ToolTip = "Show information about the current plugin";

            panel.AddItem(pluginInfoData);

            _logger?.Log($"🎛️ Created Plugin Control buttons with {enabledPlugins.Count} plugins in dropdown");
        }

        /// <summary>
        /// Handle plugin selector change event
        /// </summary>
        private void OnPluginSelectorChanged(object sender, EventArgs e)
        {
            try
            {
                var comboBox = sender as ComboBox;
                var selectedMember = comboBox?.Current;

                if (selectedMember != null)
                {
                    var pluginId = selectedMember.Name;
                    _logger?.Log($"🎯 Plugin selector changed to: {pluginId}");

                    // Activate the selected plugin
                    _pluginManager.ActivatePlugin(pluginId);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error in plugin selector change", ex);
            }
        }

        /// <summary>
        /// Setup event handlers for real-time integration
        /// </summary>
        private void SetupEventHandlers(UIControlledApplication application)
        {
            // Selection changed events will be handled through document events
            // when documents are opened
            application.ControlledApplication.DocumentOpened += OnDocumentOpened;
            application.ControlledApplication.DocumentClosed += OnDocumentClosed;
            
            _logger.Log("✅ Event handlers setup complete");
        }

        /// <summary>
        /// Handle document opened
        /// </summary>
        private void OnDocumentOpened(object sender, Autodesk.Revit.DB.Events.DocumentOpenedEventArgs e)
        {
            try
            {
                _logger.Log($"📄 Document opened: {e.Document.Title}");

                // 🚧 TEMPORARILY DISABLED: Communication layer causing build issues
                // Setup selection monitoring for this document
                // This will be implemented in the TycoonBridge
                // _tycoonBridge?.OnDocumentOpened(e.Document);

                // 🎯 Script button creation is handled by clean architecture during startup
                // No additional processing needed - scripts are already created
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling document opened", ex);
            }
        }

        /// <summary>
        /// Handle document closed
        /// </summary>
        private void OnDocumentClosed(object sender, Autodesk.Revit.DB.Events.DocumentClosedEventArgs e)
        {
            try
            {
                // In Revit 2024+, DocumentClosedEventArgs has limited properties
                // We'll just log that a document was closed without the title
                _logger.Log("📄 Document closed");
                // 🚧 TEMPORARILY DISABLED: Communication layer causing build issues
                // Note: In Revit 2024+, DocumentClosedEventArgs no longer provides the Document object
                // We'll need to handle this differently in the bridge
                // _tycoonBridge?.OnDocumentClosed(null);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling document closed", ex);
            }
        }

        /// <summary>
        /// Load image from resources
        /// </summary>
        private BitmapImage LoadImage(string imageName)
        {
            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                string assemblyDir = System.IO.Path.GetDirectoryName(assemblyPath);
                string imagePath = System.IO.Path.Combine(assemblyDir, "Resources", imageName);
                
                if (System.IO.File.Exists(imagePath))
                {
                    return new BitmapImage(new Uri(imagePath));
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to load image: {imageName}", ex);
            }
            
            return null;
        }

        /// <summary>
        /// Enhanced assembly resolution event handler for MessagePack and other dependencies
        /// </summary>
        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                // Get the assembly name
                var assemblyName = new AssemblyName(args.Name);
                string assemblyFileName = assemblyName.Name + ".dll";

                // Get the add-in directory
                string addinDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                // Multiple search paths for maximum compatibility
                string[] searchPaths = {
                    addinDirectory,
                    Path.Combine(addinDirectory, "Dependencies"),
                    Path.Combine(addinDirectory, "Libs"),
                    Path.Combine(addinDirectory, "bin"),
                    Environment.CurrentDirectory,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Autodesk", "Revit 2024", "AddIns", "TycoonAI-BIM"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Autodesk", "Revit 2023", "AddIns", "TycoonAI-BIM"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Autodesk", "Revit 2022", "AddIns", "TycoonAI-BIM")
                };

                // Handle MessagePack specifically with enhanced search
                if (assemblyName.Name.Equals("MessagePack", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string searchPath in searchPaths)
                    {
                        if (Directory.Exists(searchPath))
                        {
                            string messagePackPath = Path.Combine(searchPath, "MessagePack.dll");
                            if (File.Exists(messagePackPath))
                            {
                                _logger?.Log($"🔧 Loading MessagePack from: {messagePackPath}");
                                return Assembly.LoadFrom(messagePackPath);
                            }
                        }
                    }

                    // Try to load from GAC or already loaded assemblies
                    try
                    {
                        return Assembly.Load("MessagePack");
                    }
                    catch
                    {
                        _logger?.Log($"❌ MessagePack not found in any search path");
                    }
                }

                // Handle other dependencies with enhanced search
                foreach (string searchPath in searchPaths)
                {
                    if (Directory.Exists(searchPath))
                    {
                        string assemblyPath = Path.Combine(searchPath, assemblyFileName);
                        if (File.Exists(assemblyPath))
                        {
                            _logger?.Log($"🔧 Loading dependency: {assemblyFileName} from {searchPath}");
                            return Assembly.LoadFrom(assemblyPath);
                        }
                    }
                }

                // Try partial name loading as last resort
                try
                {
                    return Assembly.Load(assemblyName.Name);
                }
                catch
                {
                    _logger?.Log($"⚠️ Could not resolve assembly: {args.Name}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to resolve assembly: {args.Name}", ex);
                return null;
            }
        }

        /// <summary>
        /// 🎯 Dynamic Button Creation: Idling Event Handler
        /// Creates ribbon buttons from a valid UI context (when Revit is idle)
        /// </summary>
        [Obsolete("Legacy dynamic button creation removed - scripts are created during startup")]
        private void CreateDynamicButtonsOnIdle(object sender, Autodesk.Revit.UI.Events.IdlingEventArgs e)
        {
            // 🎯 CLEAN ARCHITECTURE: This method is obsolete
            // Script buttons are now created automatically during startup by RibbonManager
            _logger?.Log("⚠️ Legacy CreateDynamicButtonsOnIdle called - this is obsolete with clean architecture");

            // Unsubscribe to prevent further calls
            (sender as UIApplication).Idling -= CreateDynamicButtonsOnIdle;
        }

        /// <summary>
        /// 🎯 LEGACY: Create buttons for a specific panel - REMOVED
        /// Button creation is now handled by clean architecture during startup
        /// </summary>
        [Obsolete("Legacy button creation removed - scripts are created during startup")]
        private static int CreateButtonsForPanel(RibbonPanel targetPanel, List<TycoonRevitAddin.Scripting.ScriptDefinition> scripts,
            Dictionary<string, PushButton> existingButtons, object scriptsPlugin, string panelName)
        {
            // 🎯 CLEAN ARCHITECTURE: This method is obsolete
            // Script buttons are now created automatically during startup by RibbonManager
            return 0;
        }

        /// <summary>
        /// 🎯 LEGACY: Create an individual button for a script - REMOVED
        /// Button creation is now handled by clean architecture during startup
        /// </summary>
        [Obsolete("Legacy button creation removed - scripts are created during startup")]
        private static int CreateIndividualButton(RibbonPanel targetPanel, TycoonRevitAddin.Scripting.ScriptDefinition scriptInfo,
            Dictionary<string, PushButton> existingButtons, object scriptsPlugin, string panelName)
        {
            // 🎯 CLEAN ARCHITECTURE: This method is obsolete
            // Script buttons are now created automatically during startup by RibbonManager
            return 0;
        }

        // 🎯 CLEAN ARCHITECTURE: All legacy script button creation methods removed
        // Script buttons are now created automatically during startup by RibbonManager
        // The following methods are obsolete and have been removed:
        // - CreateIndividualButtonDirect
        // - CreateStackedButtons
        // - GetCommandClassForScript
        // - LoadIcon
        // - QueueScriptsForCreation
        // 🎯 CLEAN ARCHITECTURE: All legacy script button creation methods removed
        // Script buttons are now created automatically during startup by RibbonManager
        // The following legacy methods have been removed:
        // - CreateIndividualButtonDirect, CreateStackedButtons, GetCommandClassForScript
        // - LoadIcon, QueueScriptsForCreation, CreateDynamicButtonsOnIdle
        // - CreateButtonsForPanel, CreateIndividualButton

        /// <summary>
        /// 🔥 HOT-RELOAD: Get the clean RibbonManager for script hot-reload functionality
        /// </summary>
        public static TycoonRevitAddin.Scripting.RibbonManager GetRibbonManager()
        {
            return _cleanRibbonManager;
        }
    }
}
