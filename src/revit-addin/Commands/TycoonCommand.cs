using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TycoonRevitAddin.UI;

namespace TycoonRevitAddin.Commands
{
    /// <summary>
    /// Main Tycoon command for connecting to AI-BIM server
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TycoonCommand : IExternalCommand, IExternalCommandAvailability
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var logger = Application.Logger;
                var bridge = Application.TycoonBridge;

                logger?.Log("🎯 Tycoon Connect command executed");

                if (bridge.IsConnected)
                {
                    // Already connected - show status or disconnect
                    var result = MessageBox.Show(
                        "Tycoon AI-BIM is currently connected.\n\nWould you like to disconnect?",
                        "Tycoon AI-BIM Status",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                    );

                    if (result == DialogResult.Yes)
                    {
                        bridge.Disconnect();
                        TaskDialog.Show("Tycoon Disconnected", "Disconnected from Tycoon AI-BIM Server");
                    }
                }
                else
                {
                    // Not connected - show real-time connection dialog
                    logger?.Log("🔄 Starting connection attempt...");

                    // TODO: Set connecting status when ribbon manager is ready

                    try
                    {
                        // Create and show real-time connection dialog
                        var connectionDialog = new ConnectionProgressDialog();

                        // Start the connection process in the background
                        Task<bool> connectionTask = null;

                        // Show the dialog and start connection when it loads
                        connectionDialog.Loaded += async (s, e) =>
                        {
                            try
                            {
                                connectionTask = connectionDialog.ConnectAsync(bridge);
                                await connectionTask;
                            }
                            catch (Exception ex)
                            {
                                logger?.LogError("Connection task error", ex);
                            }
                        };

                        // Show the dialog (this will block until user closes it)
                        var dialogResult = connectionDialog.ShowDialog();

                        // Check the connection result
                        bool connected = connectionDialog.WasSuccessful;

                        // Show final result only if dialog was not cancelled
                        if (dialogResult == true && connected)
                        {
                            TaskDialog.Show("Tycoon Connected",
                                "✅ Successfully connected to Tycoon AI-BIM Server!\n\n" +
                                "🎯 AI-Revit integration is now active\n" +
                                "📋 Selection context will be shared with AI\n" +
                                "🤖 Ready for AI-powered workflows");
                        }
                        else if (dialogResult == true && !connected && connectionDialog.ErrorMessage != null)
                        {
                            TaskDialog.Show("Connection Failed",
                                $"❌ Connection failed: {connectionDialog.ErrorMessage}\n\n" +
                                "Please ensure:\n" +
                                "• Tycoon MCP Server is running (npm start)\n" +
                                "• Server is listening on ports 8765-8864\n" +
                                "• No firewall blocking connection");
                        }
                        // If dialogResult is false, user cancelled - no need to show error
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError("Connection error", ex);
                        TaskDialog.Show("Connection Error",
                            $"❌ Connection error: {ex.Message}\n\n" +
                            "Please check the log file for more details.");
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Application.Logger?.LogError("Error in TycoonCommand", ex);
                TaskDialog.Show("Tycoon Error", ex.Message);
                message = $"Error: {ex.Message}";
                return Result.Failed;
            }
        }

        /// <summary>
        /// Determine if command is available
        /// </summary>
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            // Command is always available
            return true;
        }

        /// <summary>
        /// Show message to user with proper parent window
        /// </summary>
        private void ShowMessage(string message, string title)
        {
            try
            {
                // Get Revit main window handle for proper dialog parenting
                var revitWindow = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

                if (revitWindow != IntPtr.Zero)
                {
                    // Create a wrapper for the Revit window handle
                    var parentWindow = new RevitWindowWrapper(revitWindow);
                    MessageBox.Show(parentWindow, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    // Fallback to regular MessageBox
                    MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch
            {
                // If anything fails, use regular MessageBox
                MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// Wrapper class for Revit window handle
        /// </summary>
        private class RevitWindowWrapper : System.Windows.Forms.IWin32Window
        {
            private readonly IntPtr _handle;

            public RevitWindowWrapper(IntPtr handle)
            {
                _handle = handle;
            }

            public IntPtr Handle => _handle;
        }
    }

    /// <summary>
    /// Command for framing walls
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class FrameWallsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var bridge = Application.TycoonBridge;
                
                if (!bridge.IsConnected)
                {
                    MessageBox.Show(
                        "Please connect to Tycoon AI-BIM Server first using the Connect button.",
                        "Not Connected",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return Result.Cancelled;
                }

                // This would trigger the steel framing workflow
                MessageBox.Show(
                    "Steel framing workflow will be implemented here.\n\n" +
                    "This will analyze selected walls and create framing elements using FLC standards.",
                    "Frame Walls",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Application.Logger?.LogError("Error in FrameWallsCommand", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    /// <summary>
    /// Command for renumbering elements
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class RenumberCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var bridge = Application.TycoonBridge;
                
                if (!bridge.IsConnected)
                {
                    MessageBox.Show(
                        "Please connect to Tycoon AI-BIM Server first using the Connect button.",
                        "Not Connected",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return Result.Cancelled;
                }

                // This would trigger the renumbering workflow
                MessageBox.Show(
                    "Element renumbering workflow will be implemented here.\n\n" +
                    "This will renumber selected elements using FLC sequencing standards.",
                    "Renumber Elements",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Application.Logger?.LogError("Error in RenumberCommand", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    /// <summary>
    /// Command for copying MCP configuration
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CopyMCPConfigCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Generate MCP configuration JSON
                string mcpConfig = GenerateMCPConfiguration();

                // Copy to clipboard
                System.Windows.Forms.Clipboard.SetText(mcpConfig);

                // Check if MCP server exists
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string mcpServerPath = Path.Combine(appDataPath, "Tycoon", "mcp-server", "dist", "index.js");

                string statusMessage;
                if (File.Exists(mcpServerPath))
                {
                    statusMessage = "MCP configuration copied to clipboard!\n\n" +
                                   "Next steps:\n" +
                                   "1. Open your AI assistant (Augment, VS Code, etc.)\n" +
                                   "2. Navigate to MCP settings\n" +
                                   "3. Paste the configuration\n" +
                                   "4. Restart your AI assistant\n" +
                                   "5. Click 'Connect to AI' in Revit to test the connection";
                }
                else
                {
                    statusMessage = "MCP configuration copied to clipboard!\n\n" +
                                   "⚠️ Note: MCP server not found at expected location.\n" +
                                   "You may need to manually install the MCP server from:\n" +
                                   "https://github.com/Jrandolph3110/tycoon-ai-bim-platform/releases\n\n" +
                                   "Then paste this configuration into your AI assistant.";
                }

                // Show success message
                MessageBox.Show(
                    statusMessage,
                    "MCP Configuration Ready",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Application.Logger?.LogError("Error in CopyMCPConfigCommand", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }

        private string GenerateMCPConfiguration()
        {
            // Get the MCP server path
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string mcpServerPath = Path.Combine(appDataPath, "Tycoon", "mcp-server", "dist", "index.js");

            // Create the configuration object
            var config = new
            {
                mcpServers = new
                {
                    tycoon_ai_bim = new
                    {
                        command = "node",
                        args = new[] { mcpServerPath },
                        env = new
                        {
                            NODE_ENV = "production"
                        }
                    }
                }
            };

            // Serialize to JSON with proper formatting
            return Newtonsoft.Json.JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);
        }
    }

    /// <summary>
    /// Command for validating panels
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ValidateCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var bridge = Application.TycoonBridge;
                
                if (!bridge.IsConnected)
                {
                    MessageBox.Show(
                        "Please connect to Tycoon AI-BIM Server first using the Connect button.",
                        "Not Connected",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return Result.Cancelled;
                }

                // This would trigger the panel validation workflow
                MessageBox.Show(
                    "Panel validation workflow will be implemented here.\n\n" +
                    "This will validate selected panels against FLC ticket requirements.",
                    "Validate Panels",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Application.Logger?.LogError("Error in ValidateCommand", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
