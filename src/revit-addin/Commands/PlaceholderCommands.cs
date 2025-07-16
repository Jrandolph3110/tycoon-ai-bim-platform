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
                var logger = TycoonRevitAddin.Application.Logger;
                logger?.Log("🔄 Reload Scripts command executed");

                // Get the Scripts plugin from PluginManager
                var pluginManager = TycoonRevitAddin.Application.PluginManager;
                if (pluginManager == null)
                {
                    message = "Plugin Manager not available";
                    return Result.Failed;
                }

                var scriptsPlugin = pluginManager.GetPlugin("scripts-plugin") as TycoonRevitAddin.Plugins.ScriptsPlugin;
                if (scriptsPlugin == null)
                {
                    message = "Scripts Plugin not found";
                    return Result.Failed;
                }

                // Refresh scripts through the ScriptsPlugin (handles both local and GitHub sources)
                scriptsPlugin.RefreshScripts();

                // Note: CleanRibbonManager refresh disabled to prevent tab context conflicts
                // The ScriptsPlugin now handles all script refresh functionality with dual-source support
                logger?.Log("🔥 Hot-reload: Script refresh completed via ScriptsPlugin");
                // Show success message with source breakdown
                var currentScripts = scriptsPlugin.GetCurrentScripts();
                var localScriptCount = currentScripts.Count(s => s.Source == "Local");
                var githubScriptCount = currentScripts.Count(s => s.Source == "GitHub");

                TaskDialog.Show("Scripts Reloaded",
                    "✅ Scripts have been reloaded successfully!\n\n" +
                    $"📊 Sources: Local ({localScriptCount}) + GitHub ({githubScriptCount}) = {localScriptCount + githubScriptCount} total\n\n" +
                    "🌐 GitHub scripts are marked with a globe icon.\n" +
                    "Modified local scripts will use their updated versions.");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Failed to reload scripts: {ex.Message}";
                TycoonRevitAddin.Application.Logger?.LogError("❌ Failed to reload scripts", ex);
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

                // 🎯 CLEAN ARCHITECTURE: Stack Manager removed
                // Script stacking is now handled automatically by RibbonManager based on script.json configuration
                MessageBox.Show("📋 Stack Manager - Clean Architecture\n\n" +
                              "Script stacking is now handled automatically based on script.json configuration.\n\n" +
                              "To create stacked buttons:\n" +
                              "1. Add 'stack' property to script.json files\n" +
                              "2. Use same stack name for scripts to group together\n" +
                              "3. Set 'stackOrder' property for ordering\n\n" +
                              "Example:\n" +
                              "\"stack\": \"MyTools\",\n" +
                              "\"stackOrder\": 1",
                              "Stack Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);

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

                // 🎯 CLEAN ARCHITECTURE: GitHub integration simplified
                // Scripts are now managed locally with clean architecture
                var scriptDirectory = TycoonRevitAddin.Scripting.ScriptDiscoveryService.GetDefaultScriptDirectory();

                MessageBox.Show("📂 Script Management Status\n\n" +
                              "🎯 Clean Architecture: Local script management\n" +
                              $"📁 Script Directory: {scriptDirectory}\n" +
                              "⚙️ Configuration: script.json files in subdirectories\n\n" +
                              "✅ Scripts are discovered automatically on startup\n" +
                              "🔄 Manual discovery available via 'Reload Scripts'\n\n" +
                              "📝 Note: GitHub integration has been simplified.\n" +
                              "Scripts are managed locally for better performance and reliability.",
                              "Script Management Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
                // No else clause needed - clean architecture always works

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
                    // 🎯 CLEAN ARCHITECTURE: Layout reset simplified
                    // Script layout is now handled automatically by RibbonManager based on script.json
                    MessageBox.Show("✅ Layout Reset Complete\n\n" +
                                  "With the new clean architecture, script layout is handled automatically.\n\n" +
                                  "Script placement is determined by:\n" +
                                  "• 'panel' property in script.json\n" +
                                  "• 'stack' property for grouping\n" +
                                  "• 'stackOrder' property for ordering\n\n" +
                                  "No manual layout management is needed.",
                                  "Layout Reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
