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

                // 🎯 CLEAN ARCHITECTURE: Get script definition and execute directly
                var scriptDefinition = GetScriptDefinition(scriptName);
                if (scriptDefinition == null)
                {
                    TaskDialog.Show("Script Error",
                        $"Script '{scriptName}' not found. Please ensure the script exists in the scripts directory.");
                    return Result.Failed;
                }

                // Execute script directly
                ExecuteScript(scriptDefinition, commandData);

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
        /// 🎯 CLEAN ARCHITECTURE: Get script definition from discovery service
        /// </summary>
        private ScriptDefinition GetScriptDefinition(string scriptName)
        {
            try
            {
                var scriptDiscovery = new TycoonRevitAddin.Scripting.ScriptDiscoveryService();
                var scriptDirectory = TycoonRevitAddin.Scripting.ScriptDiscoveryService.GetDefaultScriptDirectory();
                var scripts = scriptDiscovery.DiscoverScripts(scriptDirectory);

                return scripts.FirstOrDefault(s => s.Name == scriptName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get script definition: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 🎯 CLEAN ARCHITECTURE: Execute script directly using ScriptProxy
        /// </summary>
        private void ExecuteScript(ScriptDefinition scriptDefinition, ExternalCommandData commandData)
        {
            try
            {
                // Create script proxy for execution
                var scriptProxy = new ScriptProxy();
                scriptProxy.Initialize();

                // Execute script
                var success = scriptProxy.ExecuteScript(scriptDefinition);

                if (!success)
                {
                    TaskDialog.Show("Script Execution Failed",
                        $"Script '{scriptDefinition.Name}' failed to execute. Check logs for details.");
                }
                else
                {
                    // Script executed successfully
                    System.Diagnostics.Debug.WriteLine($"Script '{scriptDefinition.Name}' executed successfully");
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Script Execution Error",
                    $"An error occurred while executing script '{scriptDefinition.Name}':\n\n{ex.Message}");
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
