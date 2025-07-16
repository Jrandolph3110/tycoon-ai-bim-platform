using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.Scripting
{
    /// <summary>
    /// Clean, simple ribbon manager that builds UI from script definitions
    /// Single responsibility: Take script definitions and create Revit UI elements
    /// </summary>
    public class RibbonManager
    {
        private readonly UIControlledApplication _application;
        private readonly ILogger _logger;

        // 🔥 HOT-RELOAD: Track created buttons and panels for dynamic updates
        private readonly Dictionary<string, PushButton> _createdButtons = new Dictionary<string, PushButton>();
        private readonly Dictionary<string, RibbonPanel> _panels = new Dictionary<string, RibbonPanel>();

        public RibbonManager(UIControlledApplication application, ILogger logger = null)
        {
            _application = application;
            _logger = logger;
        }

        /// <summary>
        /// Build the entire ribbon from script definitions in one clean pass
        /// </summary>
        public void BuildRibbon(List<ScriptDefinition> scripts, string tabName)
        {
            try
            {
                _logger?.Log($"🎯 Building ribbon for {scripts.Count} scripts");

                // Group scripts by panel
                var panelGroups = scripts.GroupBy(s => s.Panel ?? "Production");

                foreach (var panelGroup in panelGroups)
                {
                    var panelName = panelGroup.Key;
                    var ribbonPanel = GetOrCreatePanel(tabName, panelName);
                    
                    if (ribbonPanel != null)
                    {
                        BuildPanelButtons(ribbonPanel, panelGroup.ToList(), panelName);
                    }
                }

                _logger?.Log($"✅ Ribbon building complete");
            }
            catch (Exception ex)
            {
                _logger?.LogError("❌ Failed to build ribbon", ex);
            }
        }

        /// <summary>
        /// Build buttons for a specific panel
        /// </summary>
        private void BuildPanelButtons(RibbonPanel ribbonPanel, List<ScriptDefinition> scripts, string panelName)
        {
            // Group scripts by stack within this panel
            var stackGroups = scripts.GroupBy(s => s.Stack ?? Guid.NewGuid().ToString());

            foreach (var stackGroup in stackGroups)
            {
                var stackName = stackGroup.Key;
                var stackScripts = stackGroup.OrderBy(s => s.StackOrder).ToList();

                // Check if this is a real stack (not a GUID)
                bool isRealStack = !Guid.TryParse(stackName, out _) && stackScripts.Count > 1;

                if (isRealStack)
                {
                    CreateStackedButtons(ribbonPanel, stackScripts, stackName);
                }
                else
                {
                    // Create individual buttons
                    foreach (var script in stackScripts)
                    {
                        CreateIndividualButton(ribbonPanel, script);
                    }
                }
            }
        }

        /// <summary>
        /// Create stacked buttons for a group of scripts
        /// </summary>
        private void CreateStackedButtons(RibbonPanel ribbonPanel, List<ScriptDefinition> stackScripts, string stackName)
        {
            try
            {
                if (stackScripts.Count < 2)
                {
                    _logger?.Log($"⚠️ Stack '{stackName}' has only {stackScripts.Count} scripts, creating individual buttons");
                    foreach (var script in stackScripts)
                    {
                        CreateIndividualButton(ribbonPanel, script);
                    }
                    return;
                }

                // Create button data for each script in the stack
                var buttonDataList = stackScripts.Select(CreatePushButtonData).ToList();

                // Create stacked buttons using Revit's AddStackedItems
                IList<RibbonItem> stackedItems = null;

                if (buttonDataList.Count == 2)
                {
                    stackedItems = ribbonPanel.AddStackedItems(buttonDataList[0], buttonDataList[1]);
                }
                else if (buttonDataList.Count == 3)
                {
                    stackedItems = ribbonPanel.AddStackedItems(buttonDataList[0], buttonDataList[1], buttonDataList[2]);
                }
                else
                {
                    // For >3 buttons, create first 3 as stack, rest as individual
                    stackedItems = ribbonPanel.AddStackedItems(buttonDataList[0], buttonDataList[1], buttonDataList[2]);
                    
                    for (int i = 3; i < buttonDataList.Count; i++)
                    {
                        ribbonPanel.AddItem(buttonDataList[i]);
                    }
                }

                _logger?.Log($"✅ Created {stackScripts.Count}-button stack '{stackName}' in panel '{ribbonPanel.Name}'");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"❌ Failed to create stacked buttons for stack '{stackName}'", ex);
            }
        }

        /// <summary>
        /// Create an individual button for a script
        /// </summary>
        private void CreateIndividualButton(RibbonPanel ribbonPanel, ScriptDefinition script)
        {
            try
            {
                var buttonData = CreatePushButtonData(script);
                var button = ribbonPanel.AddItem(buttonData) as PushButton;

                // 🔥 HOT-RELOAD: Track created button for future updates
                if (button != null)
                {
                    var buttonKey = $"{script.Name}_{ribbonPanel.Name}";
                    _createdButtons[buttonKey] = button;
                }

                _logger?.Log($"✅ Created individual button: {script.Name} in panel '{ribbonPanel.Name}'");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"❌ Failed to create individual button for {script.Name}", ex);
            }
        }

        /// <summary>
        /// Create PushButtonData from script definition
        /// </summary>
        private PushButtonData CreatePushButtonData(ScriptDefinition script)
        {
            var buttonId = $"Script_{script.Name.Replace(" ", "")}";
            var commandClass = GetCommandClassForScript(script.Name);

            var buttonData = new PushButtonData(
                buttonId,
                script.Name,
                typeof(Application).Assembly.Location,
                commandClass
            );

            buttonData.ToolTip = script.ToolTip ?? $"🎯 {script.Name}\n{script.Description}\nAuthor: {script.Author}";
            
            // Set icons if available
            if (!string.IsNullOrEmpty(script.IconPath))
            {
                // Load custom icon
                // buttonData.LargeImage = LoadIcon(script.IconPath, 32);
                // buttonData.Image = LoadIcon(script.IconPath, 16);
            }
            else
            {
                // Use default script icon
                buttonData.LargeImage = LoadDefaultIcon(32);
                buttonData.Image = LoadDefaultIcon(16);
            }

            return buttonData;
        }

        /// <summary>
        /// Get the appropriate command class for a script
        /// </summary>
        private string GetCommandClassForScript(string scriptName)
        {
            return scriptName switch
            {
                "Element Counter" => "TycoonRevitAddin.Commands.ElementCounterCommand",
                "Hello World" => "TycoonRevitAddin.Commands.HelloWorldCommand",
                "Hello2" => "TycoonRevitAddin.Commands.Hello2Command",
                _ => "TycoonRevitAddin.Commands.UnifiedScriptCommand"
            };
        }

        /// <summary>
        /// Get or create a ribbon panel
        /// </summary>
        private RibbonPanel GetOrCreatePanel(string tabName, string panelName)
        {
            try
            {
                // Map panel names to actual panel names
                string actualPanelName = panelName switch
                {
                    "Production" => "🟢 Production",
                    "SmartTools" => "🧠 Smart Tools β",
                    "Management" => "📊 Management",
                    "ScriptsControl" => "🎯 Scripts Control",
                    _ => panelName
                };

                // Try to find existing panel first
                var existingPanels = _application.GetRibbonPanels(tabName);
                var existingPanel = existingPanels?.FirstOrDefault(p => p.Name.Contains(actualPanelName.Split(' ').Last()));

                if (existingPanel != null)
                {
                    _logger?.Log($"📌 Using existing panel: {existingPanel.Name}");
                    return existingPanel;
                }

                // Create new panel if not found
                var newPanel = _application.CreateRibbonPanel(tabName, actualPanelName);
                _logger?.Log($"✅ Created new panel: {actualPanelName}");
                return newPanel;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"❌ Failed to get/create panel {panelName}", ex);
                return null;
            }
        }

        /// <summary>
        /// Load default icon
        /// </summary>
        private System.Windows.Media.ImageSource LoadDefaultIcon(int size)
        {
            // Return null for now - Revit will use default icon
            return null;
        }

        /// <summary>
        /// 🔥 HOT-RELOAD: Refresh script buttons dynamically without restart
        /// </summary>
        public void RefreshScriptButtons(List<ScriptDefinition> newScripts)
        {
            try
            {
                _logger?.Log("🔥 Hot-reload: Refreshing script buttons");

                // Clear existing button tracking (but don't remove from UI - Revit doesn't support that)
                // Instead, we'll disable old buttons and create new ones
                foreach (var existingButton in _createdButtons.Values)
                {
                    existingButton.Enabled = false;
                }

                // Clear tracking dictionaries
                _createdButtons.Clear();

                // Rebuild ribbon with new scripts
                BuildRibbon(newScripts, "TycoonAI");

                _logger?.Log($"🔥 Hot-reload: Successfully refreshed {newScripts.Count} script buttons");
            }
            catch (Exception ex)
            {
                _logger?.LogError("Failed to refresh script buttons", ex);
                throw;
            }
        }
    }
}
