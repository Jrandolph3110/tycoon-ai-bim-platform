using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TycoonRevitAddin.Plugins;
using TycoonRevitAddin.Scripting;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.Commands
{
    /// <summary>
    /// Unified Script Command - Executes scripts through the new ScriptEngine
    /// Replaces legacy DynamicScriptCommand with AppDomain isolation and transaction safety
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class UnifiedScriptCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Get the script name from the button that was clicked
                var scriptName = GetScriptNameFromButton(commandData);
                if (string.IsNullOrEmpty(scriptName))
                {
                    TaskDialog.Show("Script Error", "Could not determine which script to execute.");
                    return Result.Failed;
                }

                // Get ScriptEngine from PluginManager
                var scriptEngine = GetScriptEngine();
                if (scriptEngine == null)
                {
                    TaskDialog.Show("Script Error", 
                        "ScriptEngine not available. Please ensure the Scripts plugin is properly initialized.");
                    return Result.Failed;
                }

                // Execute script asynchronously
                ExecuteScriptAsync(scriptEngine, scriptName, commandData);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Script Execution Error", 
                    $"An error occurred while executing the script:\n\n{ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Get script name from the button that was clicked
        /// </summary>
        private string GetScriptNameFromButton(ExternalCommandData commandData)
        {
            try
            {
                // For now, we'll use a simple approach - extract from the command name
                // The button ID format is "UnifiedScript_{ScriptName}"
                var commandName = commandData.CommandName;
                if (!string.IsNullOrEmpty(commandName) && commandName.StartsWith("UnifiedScript_"))
                {
                    var scriptName = commandName.Substring("UnifiedScript_".Length);
                    // Convert back from button-safe name to display name
                    return scriptName.Replace("", " "); // Simple conversion
                }

                // Fallback: Try to get from journal data
                var journalData = commandData.JournalData;
                if (journalData != null && journalData.Count > 0)
                {
                    // Look for script name in journal data
                    foreach (var kvp in journalData)
                    {
                        if (kvp.Key.Contains("Script") && !string.IsNullOrEmpty(kvp.Value))
                        {
                            return kvp.Value;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                // Log error but don't fail the command
                System.Diagnostics.Debug.WriteLine($"Failed to get script name from button: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get ScriptEngine from PluginManager
        /// </summary>
        private ScriptEngine GetScriptEngine()
        {
            try
            {
                var pluginManager = PluginManager.Instance;
                if (pluginManager == null)
                {
                    return null;
                }

                // Get the ScriptsPlugin and extract its ScriptEngine
                var scriptsPlugin = pluginManager.GetPlugin("scripts") as ScriptsPlugin;
                return scriptsPlugin?.GetScriptEngine();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get ScriptEngine: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Execute script asynchronously through ScriptEngine
        /// </summary>
        private async void ExecuteScriptAsync(ScriptEngine scriptEngine, string scriptName, ExternalCommandData commandData)
        {
            try
            {
                // Execute script with Revit context
                var result = await scriptEngine.ExecuteScriptAsync(
                    scriptName,
                    commandData.Application,
                    commandData.Application.ActiveUIDocument.Document);

                if (!result.Success)
                {
                    TaskDialog.Show("Script Execution Failed",
                        $"Script '{scriptName}' failed to execute:\n\n{result.ErrorMessage}");
                }
                else
                {
                    // Script executed successfully
                    System.Diagnostics.Debug.WriteLine($"Script '{scriptName}' executed successfully in {result.ExecutionTime.TotalMilliseconds}ms");
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Script Execution Error",
                    $"An error occurred while executing script '{scriptName}':\n\n{ex.Message}");
            }
        }

        /// <summary>
        /// Get ScriptProxy from ScriptEngine (helper method)
        /// </summary>
        private ScriptProxy GetScriptProxy(ScriptEngine scriptEngine)
        {
            try
            {
                // This would need to be implemented in ScriptEngine to expose the proxy
                // For now, return null and handle initialization differently
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get ScriptProxy: {ex.Message}");
                return null;
            }
        }
    }
}
