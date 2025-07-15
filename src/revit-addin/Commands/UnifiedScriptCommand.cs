using System;
using System.Collections.Concurrent;
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
    /// Base class for unified script commands - provides common script execution functionality
    /// Each script gets its own command class to ensure proper button-to-script mapping
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public abstract class UnifiedScriptCommandBase : IExternalCommand
    {
        protected abstract string ScriptName { get; }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var scriptName = ScriptName;
                if (string.IsNullOrEmpty(scriptName))
                {
                    TaskDialog.Show("Script Error", "Script name not defined.");
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
    }

    /// <summary>
    /// Legacy unified script command - kept for backward compatibility
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class UnifiedScriptCommand : UnifiedScriptCommandBase
    {
        protected override string ScriptName => "Element Counter"; // Default fallback

        // Static registry to map button IDs to script names (legacy support)
        private static readonly ConcurrentDictionary<string, string> _buttonScriptRegistry = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// Register a button ID with its corresponding script name
        /// Called by ScriptsPlugin when creating buttons
        /// </summary>
        public static void RegisterButtonScript(string buttonId, string scriptName)
        {
            _buttonScriptRegistry.TryAdd(buttonId, scriptName);
        }

        /// <summary>
        /// Clear all registered button-script mappings
        /// Called when scripts are refreshed
        /// </summary>
        public static void ClearRegistry()
        {
            _buttonScriptRegistry.Clear();
        }
    }

    /// <summary>
    /// Element Counter Script Command
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ElementCounterScriptCommand : UnifiedScriptCommandBase
    {
        protected override string ScriptName => "Element Counter";
    }

    /// <summary>
    /// Hello World Script Command
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class HelloWorldScriptCommand : UnifiedScriptCommandBase
    {
        protected override string ScriptName => "Hello World";
    }
}
