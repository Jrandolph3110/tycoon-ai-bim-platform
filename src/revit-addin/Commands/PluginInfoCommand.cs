using System;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace TycoonRevitAddin.Commands
{
    /// <summary>
    /// Command to show information about the current plugin
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class PluginInfoCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var logger = Application.Logger;
                logger?.Log("🔍 Plugin Info command executed");

                // Get the plugin manager from the application
                var pluginManager = Application.PluginManager;
                if (pluginManager == null)
                {
                    MessageBox.Show(
                        "Plugin Manager not initialized.",
                        "Plugin Info",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return Result.Cancelled;
                }

                var activePlugin = pluginManager.ActivePlugin;
                if (activePlugin == null)
                {
                    MessageBox.Show(
                        "No plugin is currently active.",
                        "Plugin Info",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    return Result.Succeeded;
                }

                // Build plugin information
                var info = $"Plugin Information\n\n" +
                          $"Name: {activePlugin.Name}\n" +
                          $"ID: {activePlugin.Id}\n" +
                          $"Version: {activePlugin.Version}\n" +
                          $"Description: {activePlugin.Description}\n" +
                          $"Status: {(activePlugin.IsActive ? "Active" : "Inactive")}\n" +
                          $"Panels: {activePlugin.GetPanels().Count}";

                MessageBox.Show(
                    info,
                    "Current Plugin Information",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                var logger = Application.Logger;
                logger?.LogError("Plugin Info command failed", ex);
                
                message = $"Plugin Info command failed: {ex.Message}";
                return Result.Failed;
            }
        }
    }
}
