using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using TycoonRevitAddin.Communication;
using TycoonRevitAddin.Services;
using TycoonRevitAddin.UI;
using TycoonRevitAddin.Utils;

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
        private static TycoonBridge _tycoonBridge;
        private static Logger _logger;
        private static StatusPollingService _statusService;
        private static DynamicRibbonManager _ribbonManager;
        private static PushButton _connectButton;

        /// <summary>
        /// Public access to logger
        /// </summary>
        public static Logger Logger => _logger;

        /// <summary>
        /// Public access to Tycoon bridge
        /// </summary>
        public static TycoonBridge TycoonBridge => _tycoonBridge;

        /// <summary>
        /// Public access to status service
        /// </summary>
        public static StatusPollingService StatusService => _statusService;

        /// <summary>
        /// Public access to ribbon manager
        /// </summary>
        public static DynamicRibbonManager RibbonManager => _ribbonManager;

        /// <summary>
        /// Application startup
        /// </summary>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Initialize logger first (safely)
                _logger = new Logger("Tycoon", debugMode: true);
                _logger.Log("🚀 Starting Tycoon AI-BIM Platform v1.2.0.0 (Foundation Complete - Enterprise Ready)...");

                // Create ribbon tab and panels
                CreateRibbonInterface(application);

                // Initialize Tycoon bridge (minimal version)
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
        /// Application shutdown
        /// </summary>
        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                _logger?.Log("🛑 Shutting down Tycoon AI-BIM Platform...");

                // Disconnect from MCP server
                _tycoonBridge?.Disconnect();

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
        /// Create Tycoon ribbon interface
        /// </summary>
        private void CreateRibbonInterface(UIControlledApplication application)
        {
            // Create Tycoon tab
            string tabName = "Tycoon AI-BIM";
            application.CreateRibbonTab(tabName);

            // Create main panel
            RibbonPanel mainPanel = application.CreateRibbonPanel(tabName, "AI Integration");

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

            PushButton copyConfigButton = mainPanel.AddItem(copyConfigButtonData) as PushButton;
            PushButton connectButton = mainPanel.AddItem(connectButtonData) as PushButton;

            _logger?.Log($"📋 Created buttons - Copy Config: {copyConfigButton != null}, Connect: {connectButton != null}");

            // Store connect button for later registration with ribbon manager
            _connectButton = connectButton;
            _logger?.Log($"🔗 Stored connect button: {_connectButton != null}");

            // Create FLC Steel Framing panel
            RibbonPanel flcPanel = application.CreateRibbonPanel(tabName, "Steel Framing");

            // Add placeholder buttons for FLC workflows
            AddFLCButtons(flcPanel);

            _logger?.Log("✅ Ribbon interface created");
        }

        /// <summary>
        /// Add FLC steel framing buttons
        /// </summary>
        private void AddFLCButtons(RibbonPanel panel)
        {
            // Frame Walls button
            PushButtonData frameWallsData = new PushButtonData(
                "FrameWalls",
                "Frame\nWalls",
                Assembly.GetExecutingAssembly().Location,
                "TycoonRevitAddin.Commands.FrameWallsCommand"
            );
            frameWallsData.ToolTip = "Create steel framing for selected walls";
            panel.AddItem(frameWallsData);

            // Renumber Elements button
            PushButtonData renumberData = new PushButtonData(
                "RenumberElements",
                "Renumber\nElements",
                Assembly.GetExecutingAssembly().Location,
                "TycoonRevitAddin.Commands.RenumberCommand"
            );
            renumberData.ToolTip = "Renumber selected elements using FLC standards";
            panel.AddItem(renumberData);

            // Validate Panels button
            PushButtonData validateData = new PushButtonData(
                "ValidatePanels",
                "Validate\nPanels",
                Assembly.GetExecutingAssembly().Location,
                "TycoonRevitAddin.Commands.ValidateCommand"
            );
            validateData.ToolTip = "Validate panel ticket requirements";
            panel.AddItem(validateData);
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
                
                // Setup selection monitoring for this document
                // This will be implemented in the TycoonBridge
                _tycoonBridge?.OnDocumentOpened(e.Document);
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
                // Note: In Revit 2024+, DocumentClosedEventArgs no longer provides the Document object
                // We'll need to handle this differently in the bridge
                _tycoonBridge?.OnDocumentClosed(null);
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
    }
}
