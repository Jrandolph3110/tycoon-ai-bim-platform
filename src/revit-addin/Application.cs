using System;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using TycoonRevitAddin.Communication;
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
        
        public static TycoonBridge TycoonBridge => _tycoonBridge;
        public static Logger Logger => _logger;

        /// <summary>
        /// Application startup
        /// </summary>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Initialize logger
                _logger = new Logger("Tycoon", debugMode: true);
                _logger.Log("🚀 Starting Tycoon AI-BIM Platform v1.0.3.0 (DEBUG BUILD)...");

                // Create ribbon tab and panels
                CreateRibbonInterface(application);

                // Initialize Tycoon bridge
                _tycoonBridge = new TycoonBridge();
                
                // Setup event handlers
                SetupEventHandlers(application);

                _logger.Log("✅ Tycoon AI-BIM Platform initialized successfully");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                _logger?.LogError("Failed to initialize Tycoon", ex);
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
                _logger?.LogError("Error during shutdown", ex);
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

            PushButton connectButton = mainPanel.AddItem(connectButtonData) as PushButton;

            // Create FLC Steel Framing panel
            RibbonPanel flcPanel = application.CreateRibbonPanel(tabName, "Steel Framing");

            // Add placeholder buttons for FLC workflows
            AddFLCButtons(flcPanel);

            _logger.Log("✅ Ribbon interface created");
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
    }
}
