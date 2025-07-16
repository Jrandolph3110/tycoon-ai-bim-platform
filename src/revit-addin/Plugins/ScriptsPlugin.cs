using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;
using TycoonRevitAddin.Scripting;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.Plugins
{
    /// <summary>
    /// Scripts Plugin - Integrates clean script architecture with ribbon system
    /// Creates Scripts Control panel and displays discovered scripts
    /// </summary>
    public class ScriptsPlugin : PluginBase
    {
        private readonly ScriptDiscoveryService _scriptDiscovery;
        private readonly RibbonManager _ribbonManager;
        private List<ScriptDefinition> _currentScripts;
        private RibbonPanel _scriptsControlPanel;
        private RibbonPanel _productionPanel;
        private RibbonPanel _smartToolsPanel;
        private RibbonPanel _managementPanel;
        private List<PushButton> _scriptButtons;

        public override string Id => "scripts-plugin";
        public override string Name => "Scripts System";
        public override string Version => "1.0.0";
        public override string Description => "Clean script architecture with hot-reload support";

        public ScriptsPlugin(Logger logger = null) : base(logger)
        {
            _scriptDiscovery = new ScriptDiscoveryService(_logger);
            _ribbonManager = new RibbonManager(null, _logger);
            _currentScripts = new List<ScriptDefinition>();
            _scriptButtons = new List<PushButton>();
        }

        protected override void CreatePanels()
        {
            try
            {
                _logger.Log("🎨 Creating Scripts panels with dual-source support");

                // Always create Scripts Control panel first
                _scriptsControlPanel = CreatePanel("🎯 Scripts Control");
                CreateScriptControlButtons(_scriptsControlPanel);

                // Always create the standard script panels (even if empty)
                CreateStandardScriptPanels();

                // Discover scripts from both local and GitHub sources
                _currentScripts = _scriptDiscovery.DiscoverAllScripts();

                // Populate panels with discovered scripts
                PopulateScriptPanels();

                _logger.Log($"✅ Scripts panels created successfully with {_currentScripts.Count} scripts");
                LogScriptSources();
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Failed to create Scripts panels", ex);
            }
        }

        private void CreateScriptControlButtons(RibbonPanel panel)
        {
            try
            {
                // Reload Scripts button
                var reloadButton = AddPushButton(
                    panel,
                    "ReloadScripts",
                    "Reload Scripts",
                    "TycoonRevitAddin.Commands.ReloadScriptsCommand",
                    "🔄 Reload all scripts from both local and GitHub sources\nRefresh the script list by scanning local directory and GitHub cache. Enables hot-reload development workflow."
                );
                _scriptButtons.Add(reloadButton);

                // Refresh GitHub Scripts button
                var refreshGitHubButton = AddPushButton(
                    panel,
                    "RefreshGitHubScripts",
                    "Refresh GitHub",
                    "TycoonRevitAddin.Commands.RefreshGitHubScriptsCommand",
                    "📥 Download latest scripts from GitHub\nRefresh GitHub script cache with latest verified production scripts from repository."
                );
                _scriptButtons.Add(refreshGitHubButton);

                _logger.Log("🎯 Created Scripts Control buttons (Reload + GitHub Refresh)");
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Failed to create Scripts Control buttons", ex);
            }
        }

        /// <summary>
        /// Create standard script panels (always created, even if empty)
        /// </summary>
        private void CreateStandardScriptPanels()
        {
            try
            {
                // Always create the standard panels
                _productionPanel = CreatePanel("🟢 Production");
                _smartToolsPanel = CreatePanel("🧠 Smart Tools");
                _managementPanel = CreatePanel("📊 Management");

                _logger.Log("🎨 Created standard script panels (Production, Smart Tools, Management)");
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Failed to create standard script panels", ex);
            }
        }

        /// <summary>
        /// Populate script panels with discovered scripts
        /// </summary>
        private void PopulateScriptPanels()
        {
            try
            {
                if (_currentScripts == null || _currentScripts.Count == 0)
                {
                    _logger.Log("📝 No scripts found - panels remain empty (ready for GitHub download)");
                    return;
                }

                // Group scripts by panel
                var panelGroups = _currentScripts.GroupBy(s => s.Panel ?? "Production");

                foreach (var panelGroup in panelGroups)
                {
                    var panelName = panelGroup.Key;
                    var scripts = panelGroup.ToList();

                    // Get the appropriate panel
                    RibbonPanel targetPanel = panelName switch
                    {
                        "Production" => _productionPanel,
                        "SmartTools" => _smartToolsPanel,
                        "Management" => _managementPanel,
                        _ => _productionPanel // Default to Production
                    };

                    if (targetPanel != null)
                    {
                        // Create buttons for scripts in this panel
                        CreateScriptButtons(targetPanel, scripts);
                        _logger.Log($"🎨 Populated '{panelName}' panel with {scripts.Count} scripts");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Failed to populate script panels", ex);
            }
        }

        private void CreateScriptButtons(RibbonPanel panel, List<ScriptDefinition> scripts)
        {
            try
            {
                // Group scripts by stack for proper button organization
                var stackGroups = scripts.GroupBy(s => s.Stack ?? Guid.NewGuid().ToString());

                foreach (var stackGroup in stackGroups)
                {
                    var stackName = stackGroup.Key;
                    var stackScripts = stackGroup.OrderBy(s => s.StackOrder).Take(10).ToList(); // Limit for ribbon space

                    // Check if this is a real stack (not a GUID) and has multiple scripts
                    bool isRealStack = !Guid.TryParse(stackName, out _) && stackScripts.Count > 1;

                    if (isRealStack)
                    {
                        // Determine stack type from first script (all scripts in stack should have same type)
                        var stackType = stackScripts.First().StackType?.ToLower() ?? "stacked";

                        if (stackType == "dropdown")
                        {
                            CreateDropdownButtons(panel, stackScripts, stackName);
                        }
                        else // Default to "stacked" for vertical stacking
                        {
                            CreateStackedButtons(panel, stackScripts, stackName);
                        }
                    }
                    else
                    {
                        // Create individual buttons
                        foreach (var script in stackScripts)
                        {
                            CreateIndividualButton(panel, script);
                        }
                    }
                }

                _logger.Log($"🔘 Created buttons for {scripts.Count} scripts in panel");
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Failed to create script buttons", ex);
            }
        }

        /// <summary>
        /// Create vertically stacked buttons for scripts that belong to the same stack
        /// Uses Revit's AddStackedItems API for proper vertical stacking (not dropdown)
        /// </summary>
        private void CreateStackedButtons(RibbonPanel panel, List<ScriptDefinition> stackScripts, string stackName)
        {
            try
            {
                // Create button data for each script in the stack
                var buttonDataList = new List<PushButtonData>();

                foreach (var script in stackScripts)
                {
                    var buttonText = script.Source == "GitHub" ? $"🌐 {script.Name}" : script.Name;
                    var uniqueButtonName = $"Stack_{stackName}_{script.Name}_{DateTime.Now.Ticks}";

                    var buttonData = new PushButtonData(
                        uniqueButtonName,
                        buttonText,
                        typeof(ScriptsPlugin).Assembly.Location,
                        "TycoonRevitAddin.Commands.UnifiedScriptCommand"
                    );

                    buttonData.ToolTip = $"{script.Description ?? $"Execute {script.Name} script"}\nSource: {script.Source}\nStack: {stackName}";
                    buttonData.LongDescription = script.ToolTip ?? buttonData.ToolTip;

                    buttonDataList.Add(buttonData);
                }

                // Use AddStackedItems for proper vertical stacking
                // Revit API requires exactly 2 or 3 items for stacking
                if (buttonDataList.Count == 2)
                {
                    var stackedItems = panel.AddStackedItems(buttonDataList[0], buttonDataList[1]);
                    ProcessStackedItems(stackedItems, stackScripts);
                }
                else if (buttonDataList.Count == 3)
                {
                    var stackedItems = panel.AddStackedItems(buttonDataList[0], buttonDataList[1], buttonDataList[2]);
                    ProcessStackedItems(stackedItems, stackScripts);
                }
                else
                {
                    // Fallback for other counts - create individual buttons
                    _logger.Log($"⚠️ Stack '{stackName}' has {buttonDataList.Count} items, falling back to individual buttons");
                    foreach (var script in stackScripts)
                    {
                        CreateIndividualButton(panel, script);
                    }
                    return;
                }

                _logger.Log($"📚 Created vertically stacked buttons for '{stackName}' with {stackScripts.Count} scripts");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Failed to create stacked buttons for '{stackName}': {ex.Message}", ex);

                // Fallback to individual buttons
                _logger.Log($"🔄 Falling back to individual buttons for '{stackName}'");
                foreach (var script in stackScripts)
                {
                    CreateIndividualButton(panel, script);
                }
            }
        }

        /// <summary>
        /// Process stacked items returned by AddStackedItems and configure them
        /// </summary>
        private void ProcessStackedItems(IList<RibbonItem> stackedItems, List<ScriptDefinition> stackScripts)
        {
            // Configure each button and store script name for execution
            for (int i = 0; i < stackedItems.Count && i < stackScripts.Count; i++)
            {
                if (stackedItems[i] is PushButton button)
                {
                    // Store script name for UnifiedScriptCommand
                    button.ToolTip = stackScripts[i].Name;
                    _scriptButtons.Add(button);
                }
            }
        }

        /// <summary>
        /// Create dropdown buttons for scripts that should appear in a pulldown menu
        /// Uses SplitButton for dropdown/pulldown behavior
        /// </summary>
        private void CreateDropdownButtons(RibbonPanel panel, List<ScriptDefinition> stackScripts, string stackName)
        {
            try
            {
                var primaryScript = stackScripts.First();
                var buttonText = primaryScript.Source == "GitHub" ? $"🌐 {primaryScript.Name}" : primaryScript.Name;
                var uniqueButtonName = $"Dropdown_{stackName}_{DateTime.Now.Ticks}";

                var splitButtonData = new SplitButtonData(uniqueButtonName, buttonText);
                var splitButton = panel.AddItem(splitButtonData) as SplitButton;

                if (splitButton != null)
                {
                    // Add primary button
                    var primaryButtonData = new PushButtonData(
                        $"{uniqueButtonName}_Primary",
                        buttonText,
                        typeof(ScriptsPlugin).Assembly.Location,
                        "TycoonRevitAddin.Commands.UnifiedScriptCommand"
                    );
                    primaryButtonData.ToolTip = $"{primaryScript.Description ?? $"Execute {primaryScript.Name} script"}\nSource: {primaryScript.Source}\nStack: {stackName}";

                    var primaryButton = splitButton.AddPushButton(primaryButtonData);
                    primaryButton.ToolTip = primaryScript.Name; // For UnifiedScriptCommand
                    _scriptButtons.Add(primaryButton);

                    // Add secondary buttons to dropdown
                    foreach (var script in stackScripts.Skip(1))
                    {
                        var secondaryButtonText = script.Source == "GitHub" ? $"🌐 {script.Name}" : script.Name;
                        var secondaryButtonData = new PushButtonData(
                            $"{uniqueButtonName}_{script.Name}",
                            secondaryButtonText,
                            typeof(ScriptsPlugin).Assembly.Location,
                            "TycoonRevitAddin.Commands.UnifiedScriptCommand"
                        );
                        secondaryButtonData.ToolTip = $"{script.Description ?? $"Execute {script.Name} script"}\nSource: {script.Source}\nStack: {stackName}";

                        var secondaryButton = splitButton.AddPushButton(secondaryButtonData);
                        secondaryButton.ToolTip = script.Name; // For UnifiedScriptCommand
                        _scriptButtons.Add(secondaryButton);
                    }

                    _logger.Log($"📋 Created dropdown buttons for '{stackName}' with {stackScripts.Count} scripts");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Failed to create dropdown buttons for '{stackName}': {ex.Message}", ex);

                // Fallback to individual buttons
                _logger.Log($"🔄 Falling back to individual buttons for '{stackName}'");
                foreach (var script in stackScripts)
                {
                    CreateIndividualButton(panel, script);
                }
            }
        }

        /// <summary>
        /// Create an individual button for a single script
        /// </summary>
        private void CreateIndividualButton(RibbonPanel panel, ScriptDefinition script)
        {
            try
            {
                var buttonText = script.Source == "GitHub" ? $"🌐 {script.Name}" : script.Name;
                var uniqueButtonName = $"Script_{script.Name}_{script.Source}_{DateTime.Now.Ticks}";

                var button = AddPushButton(
                    panel,
                    uniqueButtonName,
                    buttonText,
                    "TycoonRevitAddin.Commands.UnifiedScriptCommand",
                    $"{script.Description ?? $"Execute {script.Name} script"}\nSource: {script.Source}\nScript: {script.Name}\nPanel: {script.Panel}\nAssembly: {script.AssemblyPath}"
                );

                // Store script name in button for execution (UnifiedScriptCommand will use this)
                button.ToolTip = script.Name; // UnifiedScriptCommand will read this

                _scriptButtons.Add(button);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Failed to create individual button for '{script.Name}'", ex);
            }
        }

        /// <summary>
        /// Refresh scripts from all sources and update ribbon (hot-reload)
        /// </summary>
        public void RefreshScripts()
        {
            try
            {
                _logger.Log("🔥 Hot-reload: Refreshing scripts from all sources");

                // Clear existing buttons completely to avoid naming conflicts
                ClearAllScriptButtons();

                // Discover scripts from both local and GitHub sources
                var newScripts = _scriptDiscovery.DiscoverAllScripts();
                _currentScripts = newScripts;

                // Repopulate the existing panels with new scripts
                PopulateScriptPanels();

                _logger.Log($"🔥 Hot-reload: Refreshed {newScripts.Count} scripts");
                LogScriptSources();
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Failed to refresh scripts", ex);
            }
        }

        /// <summary>
        /// Clear only script buttons (not Scripts Control buttons) to prevent naming conflicts
        /// Handles all button types: individual buttons, stacked buttons, split buttons
        /// </summary>
        private void ClearAllScriptButtons()
        {
            try
            {
                // Only clear script buttons, not Scripts Control buttons
                var scriptButtonsToRemove = _scriptButtons.Where(b =>
                    b != null &&
                    b.Name != "ReloadScripts" &&
                    b.Name != "RefreshGitHubScripts").ToList();

                foreach (var button in scriptButtonsToRemove)
                {
                    try
                    {
                        // Disable and hide the button
                        button.Enabled = false;
                        button.Visible = false;

                        // Additional cleanup for different button types
                        if (button is PushButton pushButton)
                        {
                            // Clear tooltip to prevent conflicts
                            pushButton.ToolTip = "";
                        }
                    }
                    catch (Exception buttonEx)
                    {
                        _logger.Log($"⚠️ Failed to clear individual button {button.Name}: {buttonEx.Message}");
                    }
                }

                // Clear the collection
                _scriptButtons.RemoveAll(b => scriptButtonsToRemove.Contains(b));

                _logger.Log($"🧹 Cleared {scriptButtonsToRemove.Count} script buttons (preserved Scripts Control buttons)");
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Failed to clear script buttons", ex);
            }
        }

        /// <summary>
        /// Refresh GitHub scripts cache and update ribbon
        /// </summary>
        public async System.Threading.Tasks.Task RefreshGitHubScriptsAsync()
        {
            try
            {
                _logger.Log("📥 Refreshing GitHub scripts cache...");

                var success = await _scriptDiscovery.RefreshGitHubScriptsAsync();
                if (success)
                {
                    // Refresh all scripts after GitHub update
                    RefreshScripts();
                    _logger.Log("✅ GitHub scripts refreshed and ribbon updated");
                }
                else
                {
                    _logger.Log("❌ Failed to refresh GitHub scripts");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Failed to refresh GitHub scripts", ex);
            }
        }

        /// <summary>
        /// Log script sources for debugging
        /// </summary>
        private void LogScriptSources()
        {
            var localCount = _currentScripts.Count(s => s.Source == "Local");
            var githubCount = _currentScripts.Count(s => s.Source == "GitHub");
            _logger.Log($"📊 Script sources: Local={localCount}, GitHub={githubCount}, Total={_currentScripts.Count}");
        }

        /// <summary>
        /// Get current scripts for external access
        /// </summary>
        public List<ScriptDefinition> GetCurrentScripts()
        {
            return _currentScripts ?? new List<ScriptDefinition>();
        }

        protected override void OnCleanup()
        {
            _scriptButtons?.Clear();
            _currentScripts?.Clear();
        }
    }
}
