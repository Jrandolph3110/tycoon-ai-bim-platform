using System;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TycoonRevitAddin.Plugins;

namespace TycoonRevitAddin.Commands
{
    /// <summary>
    /// Command to refresh GitHub scripts cache and update ribbon
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RefreshGitHubScriptsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var logger = TycoonRevitAddin.Application.Logger;
                logger?.Log("📥 Refresh GitHub Scripts command executed");

                // Get the Scripts plugin from PluginManager
                var pluginManager = TycoonRevitAddin.Application.PluginManager;
                if (pluginManager == null)
                {
                    message = "Plugin Manager not available";
                    return Result.Failed;
                }

                var scriptsPlugin = pluginManager.GetPlugin("scripts-plugin") as ScriptsPlugin;
                if (scriptsPlugin == null)
                {
                    message = "Scripts Plugin not found";
                    return Result.Failed;
                }

                // Show progress dialog and refresh GitHub scripts asynchronously
                var progressDialog = new TaskDialog("GitHub Scripts Refresh")
                {
                    MainInstruction = "Refreshing GitHub Scripts",
                    MainContent = "Downloading latest verified scripts from GitHub repository...\n\nThis may take a few moments.",
                    CommonButtons = TaskDialogCommonButtons.None,
                    DefaultButton = TaskDialogResult.None,
                    AllowCancellation = false
                };

                // Start async refresh
                Task.Run(async () =>
                {
                    try
                    {
                        await scriptsPlugin.RefreshGitHubScriptsAsync();
                        
                        // Show completion dialog on UI thread
                        commandData.Application.ActiveUIDocument.Application.Idling += (sender, args) =>
                        {
                            TaskDialog.Show("GitHub Scripts Refreshed",
                                "✅ GitHub scripts have been refreshed successfully!\n\n" +
                                "Latest verified production scripts are now available in the ribbon panels.\n" +
                                "🌐 GitHub scripts are marked with a globe icon.");
                        };
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError("Failed to refresh GitHub scripts", ex);
                        
                        // Show error dialog on UI thread
                        commandData.Application.ActiveUIDocument.Application.Idling += (sender, args) =>
                        {
                            TaskDialog.Show("GitHub Scripts Refresh Failed",
                                $"❌ Failed to refresh GitHub scripts:\n\n{ex.Message}\n\n" +
                                "Please check your internet connection and try again.");
                        };
                    }
                });

                // Show immediate feedback
                TaskDialog.Show("GitHub Scripts Refresh Started",
                    "📥 GitHub scripts refresh started in background.\n\n" +
                    "You will be notified when the refresh is complete.\n" +
                    "The ribbon will be updated automatically.");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Failed to start GitHub scripts refresh: {ex.Message}";
                TycoonRevitAddin.Application.Logger?.LogError("❌ Failed to start GitHub scripts refresh", ex);
                return Result.Failed;
            }
        }
    }
}
