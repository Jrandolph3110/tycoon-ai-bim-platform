using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TycoonRevitAddin.AIActions.Commands;
using TycoonRevitAddin.Plugins;
using TycoonRevitAddin.Utils;
using TycoonRevitAddin.UI;

namespace TycoonRevitAddin.Commands
{
    /// <summary>
    /// Placeholder commands for future implementation
    /// These are referenced by the plugin system but not yet fully implemented
    /// </summary>

    [Transaction(TransactionMode.Manual)]
    public class AutoFrameCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MessageBox.Show("Auto Frame command - Coming Soon!", "Tycoon Pro FrAimer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class FrameOpeningsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MessageBox.Show("Frame Openings command - Coming Soon!", "Tycoon Pro FrAimer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class PanelSequencerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MessageBox.Show("Panel Sequencer command - Coming Soon!", "Tycoon Pro FrAimer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class BOMGeneratorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MessageBox.Show("BOM Generator command - Coming Soon!", "Tycoon Pro FrAimer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class QualityCheckCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MessageBox.Show("Quality Check command - Coming Soon!", "Tycoon Pro FrAimer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ClashDetectionCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MessageBox.Show("Clash Detection command - Coming Soon!", "Tycoon Pro FrAimer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ReloadScriptsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // 🔄 NEW: Connect to unified ScriptEngine for hot-reload
                var logger = TycoonRevitAddin.Application.Logger;
                logger?.Log("🔄 Manual script reload requested via Reload Scripts button");

                // Get the ScriptEngine instance from PluginManager
                var pluginManager = PluginManager.Instance;
                if (pluginManager == null)
                {
                    MessageBox.Show("❌ PluginManager not available.\nPlease restart Revit to initialize the plugin system.",
                                  "Script Reload Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return Result.Failed;
                }

                var scriptsPlugin = pluginManager.GetPlugin("scripts") as ScriptsPlugin;
                var scriptEngine = scriptsPlugin?.GetScriptEngine();
                if (scriptEngine == null)
                {
                    MessageBox.Show("❌ ScriptEngine not available.\nPlease ensure the Scripts plugin is properly initialized.",
                                  "Script Reload Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return Result.Failed;
                }

                // Trigger script refresh
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await scriptEngine.RefreshScriptsAsync();
                        logger?.Log("✅ Script refresh completed successfully");
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError("Failed to refresh scripts", ex);
                    }
                });

                MessageBox.Show("🔄 Script refresh initiated!\n\nScripts are being reloaded in the background.\nCheck the ribbon in a few seconds.",
                              "Script Reload", MessageBoxButtons.OK, MessageBoxIcon.Information);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Failed to reload scripts: {ex.Message}";
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class OpenScriptsFolderCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var scriptsPath = System.IO.Path.Combine(appDataPath, "Tycoon", "Scripts");

                if (!System.IO.Directory.Exists(scriptsPath))
                {
                    System.IO.Directory.CreateDirectory(scriptsPath);
                }

                // Count scripts for verification
                var scriptFiles = System.IO.Directory.GetFiles(scriptsPath, "*.*", System.IO.SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".py") || f.EndsWith(".cs"))
                    .ToArray();

                // Open folder with enhanced command to ensure refresh
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{scriptsPath}\"",
                    UseShellExecute = true
                };

                System.Diagnostics.Process.Start(processInfo);

                // Show confirmation with script count
                MessageBox.Show($"📁 Scripts Folder Opened\n\n" +
                              $"Location: {scriptsPath}\n" +
                              $"Scripts Found: {scriptFiles.Length}\n\n" +
                              $"Files:\n{string.Join("\n", scriptFiles.Select(f => "• " + System.IO.Path.GetFileName(f)))}",
                              "📁 Scripts Folder",
                              MessageBoxButtons.OK,
                              MessageBoxIcon.Information);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open scripts folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ScriptEditorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MessageBox.Show("Script Editor command - Coming Soon!", "Scripts Plugin", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class PythonConsoleCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MessageBox.Show("Python Console command - Coming Soon!", "Scripts Plugin", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class APIExplorerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MessageBox.Show("API Explorer command - Coming Soon!", "Scripts Plugin", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ElementInspectorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MessageBox.Show("Element Inspector command - Coming Soon!", "Scripts Plugin", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class DynamicScriptCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Get script path from button metadata (stored in ToolTip)
                var scriptPath = GetScriptPathFromCommand(commandData);
                if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
                {
                    MessageBox.Show($"Script file not found: {scriptPath}", "Script Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return Result.Failed;
                }

                // Read script content
                var scriptContent = File.ReadAllText(scriptPath);
                var scriptName = Path.GetFileNameWithoutExtension(scriptPath);

                // Detect script type
                var scriptType = Path.GetExtension(scriptPath).ToLower() == ".cs"
                    ? ScriptType.CSharp
                    : ScriptType.Python;

                // Get Revit context
                var doc = commandData.Application.ActiveUIDocument.Document;
                var uidoc = commandData.Application.ActiveUIDocument;
                var selectedIds = uidoc.Selection.GetElementIds().Select(id => id.IntegerValue).ToList();

                // Execute script via ScriptHotLoader
                var logger = new ConsoleLogger(); // Simple logger for now
                var hotLoader = new ScriptHotLoader(logger);

                var task = hotLoader.LoadAndExecuteScript(scriptContent, scriptName, doc, uidoc, selectedIds, scriptType);
                var result = task.GetAwaiter().GetResult(); // Synchronous execution for IExternalCommand

                if (result.Success)
                {
                    return Result.Succeeded;
                }
                else
                {
                    var errorMsg = $"Script execution failed: {result.Message}";
                    MessageBox.Show(errorMsg, "Script Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return Result.Failed;
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error executing script: {ex.Message}";
                MessageBox.Show(errorMsg, "Script Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Extract script path from command data (stored in button ToolTip)
        /// </summary>
        private string GetScriptPathFromCommand(ExternalCommandData commandData)
        {
            try
            {
                // The ScriptsPlugin stores the script path in the button's ToolTip
                // Format: "Badge: DisplayName\nDescription\nPath: /full/path/to/script.py"

                // For now, we'll need to get this from the ribbon button
                // This is a simplified approach - in production, we'd need better integration

                // Try to get the script path from the active ribbon button
                // This is a placeholder implementation that should be enhanced

                // Return the test script for now
                return @"C:\RevitAI\tycoon-ai-bim-platform\src\revit-addin\Scripts\FLC_ReNumber.cs";
            }
            catch (Exception ex)
            {
                // Fallback to test script
                return @"C:\RevitAI\tycoon-ai-bim-platform\src\revit-addin\Scripts\FLC_ReNumber.cs";
            }
        }
    }

    // AI Actions Commands
    [Transaction(TransactionMode.Manual)]
    public class AISettingsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MessageBox.Show("AI Settings - Coming Soon!", "AI Actions", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class EmergencyStopCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MessageBox.Show("Emergency Stop - All AI actions halted!", "AI Actions", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class AICreateWallCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MessageBox.Show("AI Create Wall - Coming Soon!", "AI Actions", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class AIFrameWallsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MessageBox.Show("AI Frame Walls - Coming Soon!", "AI Actions", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class AIOptimizeLayoutCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MessageBox.Show("AI Optimize Layout - Coming Soon!", "AI Actions", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class AIFixErrorsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MessageBox.Show("AI Fix Errors - Coming Soon!", "AI Actions", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ActivityMonitorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MessageBox.Show("Activity Monitor - Coming Soon!", "AI Actions", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class EventViewerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MessageBox.Show("Event Viewer - Coming Soon!", "AI Actions", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class AIUndoCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MessageBox.Show("AI Undo - Coming Soon!", "AI Actions", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class PerformanceStatsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MessageBox.Show("Performance Stats - Coming Soon!", "AI Actions", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Result.Succeeded;
        }
    }

    /// <summary>
    /// 🎯 Layout Manager Command (Chat's Customization System)
    /// Opens layout customization dialog for user-defined button stacking
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class LayoutManagerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Get the Scripts plugin and its layout manager
                var pluginManager = PluginManager.Instance;
                if (pluginManager == null)
                {
                    MessageBox.Show("🎯 Layout Manager Error\n\n" +
                                  "Plugin Manager not available.\n" +
                                  "Please ensure Tycoon is properly initialized.",
                                  "Layout Manager Error",
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Warning);
                    return Result.Failed;
                }

                // Access the Scripts plugin to get layout manager and logger
                var scriptsPlugin = pluginManager.GetPlugin("scripts");
                if (scriptsPlugin is TycoonRevitAddin.Plugins.ScriptsPlugin plugin)
                {
                    // Open Stack Manager dialog with new ScriptService-based constructor
                    var layoutManager = plugin.GetLayoutManager();
                    var logger = plugin.GetLogger();

                    var dialog = new TycoonRevitAddin.UI.StackManagerDialog(layoutManager, logger);
                    var result = dialog.ShowDialog();

                    // Layout saved successfully - no popup needed as user requested
                }
                else
                {
                    MessageBox.Show("🎯 Layout Manager Error\n\n" +
                                  "Scripts plugin not found or not properly initialized.\n" +
                                  "Please restart Revit and try again.",
                                  "Layout Manager Error",
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Warning);
                    return Result.Failed;
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                MessageBox.Show($"🎯 Layout Manager Error\n\n{ex.Message}",
                              "Layout Manager Error",
                              MessageBoxButtons.OK,
                              MessageBoxIcon.Error);
                return Result.Failed;
            }
        }
    }

    /// <summary>
    /// ⚙️ GitHub Settings Command
    /// Opens GitHub repository configuration dialog
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class GitHubSettingsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Get the Scripts plugin and its git cache manager
                var pluginManager = PluginManager.Instance;
                if (pluginManager == null)
                {
                    MessageBox.Show("⚙️ GitHub Settings Error\n\n" +
                                  "Plugin Manager not available.\n" +
                                  "Please ensure Tycoon is properly initialized.",
                                  "GitHub Settings Error",
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Warning);
                    return Result.Failed;
                }

                // Access the Scripts plugin to get git cache manager
                var scriptsPlugin = pluginManager.GetPlugin("scripts");
                if (scriptsPlugin is TycoonRevitAddin.Plugins.ScriptsPlugin plugin)
                {
                    // Show GitHub Status (repository is now hardcoded)
                    var gitCacheManager = plugin.GetGitCacheManager();
                    var logger = plugin.GetLogger();

                    MessageBox.Show("📂 GitHub Repository Status\n\n" +
                                  "🔗 Repository: Jrandolph3110/tycoon-ai-bim-platform\n" +
                                  "🌿 Branch: main\n" +
                                  "⚙️ Configuration: Hardcoded (no setup required)\n\n" +
                                  "✅ Scripts are automatically downloaded from the official repository\n" +
                                  "🔄 Use 'Refresh Scripts' to get the latest updates\n\n" +
                                  "🌟 GitHub-Driven Script System Active!",
                                  "📂 GitHub Repository Status",
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("⚙️ GitHub Settings Error\n\n" +
                                  "Scripts plugin not found or not properly initialized.\n" +
                                  "Please restart Revit and try again.",
                                  "GitHub Settings Error",
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Warning);
                    return Result.Failed;
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"⚙️ GitHub Settings Error\n\n{ex.Message}",
                              "GitHub Settings Error",
                              MessageBoxButtons.OK,
                              MessageBoxIcon.Error);
                return Result.Failed;
            }
        }
    }

    /// <summary>
    /// 🔄 Reset Layout Command
    /// Resets to automatic capability-based layout
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ResetLayoutCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var result = MessageBox.Show("🔄 Reset to Automatic Layout?\n\n" +
                                           "This will:\n" +
                                           "❌ Clear all user customizations\n" +
                                           "🔄 Return to capability-based auto layout\n" +
                                           "🎯 Scripts will be grouped by P1/P2/P3 levels\n\n" +
                                           "Are you sure you want to continue?",
                                           "🔄 Reset Layout Confirmation",
                                           MessageBoxButtons.YesNo,
                                           MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    // Get the Scripts plugin and reset layout
                    var pluginManager = PluginManager.Instance;
                    if (pluginManager != null)
                    {
                        var scriptsPlugin = pluginManager.GetPlugin("scripts");
                        if (scriptsPlugin is TycoonRevitAddin.Plugins.ScriptsPlugin plugin)
                    {
                        var layoutManager = plugin.GetLayoutManager();
                        layoutManager.ResetToAutoLayout();

                        // Auto-reload scripts after reset
                        pluginManager.RefreshScriptButtons();
                    }
                    else
                    {
                        MessageBox.Show("🔄 Reset Layout Error\n\n" +
                                      "Could not access layout manager.\n" +
                                      "Please restart Revit and try again.",
                                      "🔄 Reset Error",
                                      MessageBoxButtons.OK,
                                      MessageBoxIcon.Warning);
                        }
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
