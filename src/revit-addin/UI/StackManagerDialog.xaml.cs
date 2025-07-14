using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TycoonRevitAddin.Layout;
using TycoonRevitAddin.Models;
using TycoonRevitAddin.Plugins;
using TycoonRevitAddin.Services;
using TycoonRevitAddin.Utils;
using TycoonRevitAddin.Events;

namespace TycoonRevitAddin.UI
{
    /// <summary>
    /// 🎯 Chat's Stack Manager Dialog
    /// User-friendly GUI for customizing button stacking without script editing
    /// </summary>
    public partial class StackManagerDialog : Window
    {
        private readonly RibbonLayoutManager _layoutManager;
        private readonly Dictionary<string, ScriptMetadata> _scriptMetadata;
        private readonly Logger _logger;
        private GitCacheManager _gitCacheManager;
        private LayoutManagerViewModel _viewModel;
        private object _draggedItem;
        private Point _dragStartPoint;
        private ScriptViewModel _copiedScript;
        private RibbonLayoutSchema _currentLayout;
        private bool _hasChanges = false;

        public StackManagerDialog(RibbonLayoutManager layoutManager, Logger logger)
        {
            InitializeComponent();
            _layoutManager = layoutManager;
            _logger = logger;

            // 🎯 NEW: Get GitCacheManager from ScriptService
            _gitCacheManager = ScriptService.Instance.GetGitCacheManager();

            // 🎯 NEW: Initialize script metadata from ScriptService
            _scriptMetadata = new Dictionary<string, ScriptMetadata>();
            LoadScriptMetadataFromScriptService();

            // 🎯 NEW: Subscribe to ScriptService events for real-time updates
            ScriptService.Instance.LocalScriptsUpdated += OnScriptServiceLocalScriptsUpdated;
            ScriptService.Instance.GitHubScriptsUpdated += OnScriptServiceGitHubScriptsUpdated;

            _logger.Log($"🎯 StackManagerDialog initialized with ScriptService - {_scriptMetadata.Count} scripts loaded");

            InitializeViewModel();
        }



        /// <summary>
        /// Initialize the ViewModel with advanced features
        /// </summary>
        private void InitializeViewModel()
        {
            _viewModel = new LayoutManagerViewModel();

            // Try to load existing layout first
            if (TryLoadExistingLayout())
            {
                _logger.Log("🎯 Loaded existing user scripts layout");
            }
            else
            {
                // Fall back to creating default panels using script metadata (includes GitHub scripts)
                CreateDefaultPanelsFromMetadata();
                _logger.Log("🎯 Created default layout from script metadata");
            }

            DataContext = _viewModel;
        }

        /// <summary>
        /// Try to load current working layout from LayoutManager
        /// This shows what's actually on the ribbon, not just saved user customizations
        /// </summary>
        private bool TryLoadExistingLayout()
        {
            try
            {
                // CRITICAL FIX: Get the current merged layout (what's actually working on the ribbon)
                // instead of just the saved user layout
                _logger.Log("🔍 DIAGNOSTIC: Getting current working layout from LayoutManager");
                var currentLayout = _layoutManager.MergeLayouts(_scriptMetadata);

                if (currentLayout != null && currentLayout.Panels.Count > 0)
                {
                    // Convert current working layout to ViewModel
                    ConvertLayoutToViewModel(currentLayout);
                    _logger.Log($"🎯 Loaded current working layout with {currentLayout.Panels.Count} panels, mode: {currentLayout.Mode}");

                    // Log panel contents for debugging
                    foreach (var panel in currentLayout.Panels)
                    {
                        // Count scripts from ScriptItems
                        var scriptCount = panel.Stacks.Sum(s =>
                            s.ScriptItems?.Count ?? 0);
                        _logger.Log($"🔍 DIAGNOSTIC: Panel '{panel.Name}' has {panel.Stacks.Count} stacks with {scriptCount} total scripts");
                    }

                    // CRITICAL FIX: Always add GitHub Scripts panel to show unplaced scripts
                    AddGitHubScriptsPanel(currentLayout);

                    return true;
                }
                else
                {
                    _logger.LogWarning("🔍 DIAGNOSTIC: MergeLayouts returned null or empty layout");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load current working layout", ex);
            }
            return false;
        }

        /// <summary>
        /// Add GitHub Scripts panel showing ONLY unplaced GitHub scripts for user organization
        /// </summary>
        private void AddGitHubScriptsPanel(RibbonLayoutSchema currentLayout)
        {
            try
            {
                // CRITICAL FIX: Check if GitHub Scripts panel already exists to prevent duplicates
                var existingGitHubPanel = _viewModel.Panels.FirstOrDefault(p => p.Id == "GitHubScripts");  // 🔧 FIX: PascalCase
                if (existingGitHubPanel != null)
                {
                    _logger.Log("🔍 DIAGNOSTIC: GitHub Scripts panel already exists - skipping duplicate creation");
                    return;
                }

                // Get scripts that are already placed in other panels
                var placedScripts = GetScriptsAlreadyPlacedInPanels();

                // Get ONLY unplaced GitHub scripts for user organization
                var unplacedGitHubScripts = _scriptMetadata.Values
                    .Where(s => s.IsGitHubScript && !placedScripts.Contains(s.Name))
                    .ToList();

                _logger.Log($"🔍 DIAGNOSTIC: Found {_scriptMetadata.Values.Count(s => s.IsGitHubScript)} total GitHub scripts, {placedScripts.Count} already placed, {unplacedGitHubScripts.Count} unplaced");

                // Create GitHub Scripts panel
                var githubScriptsPanel = new PanelViewModel
                {
                    Id = "GitHubScripts",  // 🔧 FIX: Use PascalCase to match layout templates
                    Name = "📦 GitHub Scripts",
                    Color = new SolidColorBrush(Color.FromRgb(33, 150, 243)), // #2196F3
                    Order = 3
                };

                // Add ONLY unplaced GitHub scripts to the panel for user organization
                if (unplacedGitHubScripts.Count > 0)
                {
                    var githubScriptsStack = githubScriptsPanel.AddStack("Available Scripts", StackLayoutType.Vertical);
                    foreach (var script in unplacedGitHubScripts)
                    {
                        githubScriptsStack.AddScript(script.Name, script.Description ?? $"GitHub script: {script.Name}");
                    }
                    _logger.Log($"🎯 Added {unplacedGitHubScripts.Count} unplaced GitHub scripts to GitHub panel for organization");
                }
                else
                {
                    _logger.Log("🎯 No unplaced GitHub scripts found - GitHub panel will be empty");
                }

                _viewModel.Panels.Add(githubScriptsPanel);
                _logger.Log("🎯 Successfully created new GitHub Scripts panel");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to add GitHub Scripts panel", ex);
            }
        }

        /// <summary>
        /// Get list of script names that are already placed in non-GitHub panels
        /// </summary>
        private HashSet<string> GetScriptsAlreadyPlacedInPanels()
        {
            var placedScripts = new HashSet<string>();

            // Check all panels except GitHub Scripts panel
            foreach (var panel in _viewModel.Panels.Where(p => !p.Id.Equals("GitHubScripts", StringComparison.OrdinalIgnoreCase)))  // 🔧 FIX: PascalCase
            {
                foreach (var stack in panel.Stacks)
                {
                    foreach (var script in stack.Scripts)
                    {
                        placedScripts.Add(script.Name);
                    }
                }
            }

            return placedScripts;
        }

        /// <summary>
        /// Convert LayoutManager data to ViewModel format
        /// </summary>
        private void ConvertLayoutToViewModel(RibbonLayoutSchema layout)
        {
            var colors = new[]
            {
                new SolidColorBrush(Color.FromRgb(39, 174, 96)),   // Green - Production
                new SolidColorBrush(Color.FromRgb(243, 156, 18)),  // Orange - Smart Tools
                new SolidColorBrush(Color.FromRgb(142, 68, 173)),  // Purple - Management
                new SolidColorBrush(Color.FromRgb(33, 150, 243))   // Blue - GitHub Scripts
            };

            for (int i = 0; i < layout.Panels.Count; i++)
            {
                var panel = layout.Panels[i];
                var panelVM = new PanelViewModel
                {
                    Id = panel.Id,
                    Name = panel.Name,
                    Color = colors[i % colors.Length],
                    Order = i
                };

                foreach (var stack in panel.Stacks)
                {
                    var stackVM = panelVM.AddStack(stack.Name, StackLayoutType.Vertical);

                    // Use ScriptItems with icon information
                    var scriptItems = stack.ScriptItems ?? new List<ScriptItem>();

                    foreach (var scriptItem in scriptItems)
                    {
                        // Handle GitHub scripts differently
                        if (panel.Id.Equals("GitHubScripts", StringComparison.OrdinalIgnoreCase))  // 🔧 FIX: PascalCase
                        {
                            stackVM.AddScript(scriptItem.Name, $"GitHub script: {scriptItem.Name}", scriptItem.IconPath);
                        }
                        else
                        {
                            stackVM.AddScript(scriptItem.Name, $"User script: {scriptItem.Name}", scriptItem.IconPath);
                        }
                    }
                }

                _viewModel.Panels.Add(panelVM);
            }
        }

        /// <summary>
        /// Convert current ViewModel state to RibbonLayoutSchema for saving
        /// </summary>
        private RibbonLayoutSchema ConvertViewModelToLayout()
        {
            try
            {
                var layout = new RibbonLayoutSchema
                {
                    Mode = TycoonRevitAddin.Layout.LayoutMode.Manual, // User has customized the layout
                    LastModified = DateTime.UtcNow,
                    Panels = new List<PanelLayout>()
                };

                foreach (var panelVM in _viewModel.Panels.OrderBy(p => p.Order))
                {
                    // Skip GitHub Scripts panel - it's not part of the main layout
                    if (panelVM.Id.Equals("GitHubScripts", StringComparison.OrdinalIgnoreCase))  // 🔧 FIX: PascalCase
                        continue;

                    var panelLayout = new PanelLayout
                    {
                        Id = panelVM.Id,
                        Name = panelVM.Name,
                        Stacks = new List<StackLayout>()
                    };

                    int stackOrder = 0;
                    foreach (var stackVM in panelVM.Stacks)
                    {
                        // Only save stacks that have scripts
                        if (stackVM.Scripts.Any())
                        {
                            var stackLayout = new StackLayout
                            {
                                Id = stackVM.Id,
                                Name = stackVM.Name,
                                // 🔥 NEW: Save script items with icon information
                                ScriptItems = stackVM.Scripts.Select(s => new ScriptItem
                                {
                                    Name = s.Name,
                                    IconPath = s.IconPath // Preserve the custom icon from UI
                                }).ToList(),
                                Order = stackOrder++
                            };

                            panelLayout.Stacks.Add(stackLayout);
                        }
                    }

                    layout.Panels.Add(panelLayout);
                }

                _logger.Log($"🔄 Converted ViewModel to layout: {layout.Panels.Count} panels, {layout.Panels.Sum(p => p.Stacks.Count)} stacks");
                return layout;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to convert ViewModel to layout", ex);
                return null;
            }
        }

        /// <summary>
        /// Create default panels using script metadata (includes both local and GitHub scripts)
        /// </summary>
        private void CreateDefaultPanelsFromMetadata()
        {
            // Create default panels with proper colors
            var productionPanel = new PanelViewModel
            {
                Id = "Production",  // 🔧 FIX: Use PascalCase to match layout templates and ScriptsPlugin
                Name = "🟢 Production",
                Color = new SolidColorBrush(Color.FromRgb(39, 174, 96)), // #27AE60
                Order = 0
            };

            var smartToolsPanel = new PanelViewModel
            {
                Id = "SmartTools",  // 🔧 FIX: Use PascalCase to match layout templates and ScriptsPlugin
                Name = "🧠 Smart Tools β",
                Color = new SolidColorBrush(Color.FromRgb(243, 156, 18)), // #F39C12
                Order = 1
            };

            var managementPanel = new PanelViewModel
            {
                Id = "Management",  // 🔧 FIX: Use PascalCase to match layout templates and ScriptsPlugin
                Name = "⚙️ Management",
                Color = new SolidColorBrush(Color.FromRgb(142, 68, 173)), // #8E44AD
                Order = 2
            };

            var githubScriptsPanel = new PanelViewModel
            {
                Id = "GitHubScripts",  // 🔧 FIX: Use PascalCase to match layout templates
                Name = "📦 GitHub Scripts",
                Color = new SolidColorBrush(Color.FromRgb(33, 150, 243)), // #2196F3
                Order = 3
            };

            // Load scripts from metadata instead of scanning files
            LoadScriptsFromMetadata(productionPanel, smartToolsPanel, managementPanel, githubScriptsPanel);

            _viewModel.Panels.Add(productionPanel);
            _viewModel.Panels.Add(smartToolsPanel);
            _viewModel.Panels.Add(managementPanel);
            _viewModel.Panels.Add(githubScriptsPanel);
        }

        /// <summary>
        /// Load scripts from metadata (includes both local and GitHub scripts)
        /// </summary>
        private void LoadScriptsFromMetadata(PanelViewModel productionPanel, PanelViewModel smartToolsPanel, PanelViewModel managementPanel, PanelViewModel githubScriptsPanel)
        {
            try
            {
                _logger.Log($"🎯 Loading scripts from metadata: {_scriptMetadata.Count} total scripts");

                // Separate local and GitHub scripts
                var localScripts = new List<ScriptMetadata>();
                var githubScripts = new List<ScriptMetadata>();

                foreach (var script in _scriptMetadata.Values)
                {
                    if (script.IsGitHubScript)
                    {
                        githubScripts.Add(script);
                    }
                    else
                    {
                        localScripts.Add(script);
                    }
                }

                _logger.Log($"🎯 Found {localScripts.Count} local scripts and {githubScripts.Count} GitHub scripts");

                // Add local scripts to Production panel if any exist
                if (localScripts.Count > 0)
                {
                    var userScriptsStack = productionPanel.AddStack("User Scripts", StackLayoutType.Vertical);
                    foreach (var script in localScripts)
                    {
                        userScriptsStack.AddScript(script.Name, script.Description ?? $"Local script: {script.Name}");
                    }
                    _logger.Log($"🎯 Added {localScripts.Count} local scripts to Production panel");
                }

                // Add GitHub scripts to GitHub panel
                if (githubScripts.Count > 0)
                {
                    var githubScriptsStack = githubScriptsPanel.AddStack("Available Scripts", StackLayoutType.Vertical);
                    foreach (var script in githubScripts)
                    {
                        githubScriptsStack.AddScript(script.Name, script.Description ?? $"GitHub script: {script.Name}");
                    }
                    _logger.Log($"🎯 Added {githubScripts.Count} GitHub scripts to GitHub panel");
                }

                // No placeholder buttons - if no scripts exist, just leave panels empty
                if (localScripts.Count == 0 && githubScripts.Count == 0)
                {
                    _logger.Log("🎯 No scripts found in metadata - panels will remain empty");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load scripts from metadata", ex);
                // Don't create placeholder buttons on error - just leave panels empty
            }
        }





        /// <summary>
        /// Load actual cached GitHub scripts
        /// </summary>
        private void LoadCachedGitHubScripts(PanelViewModel githubPanel, string cachedScriptsPath)
        {
            var analysisStack = githubPanel.AddStack("Analysis", StackLayoutType.Vertical);
            var managementStack = githubPanel.AddStack("Management", StackLayoutType.Vertical);
            var utilitiesStack = githubPanel.AddStack("Utilities", StackLayoutType.Vertical);

            // Load scripts from each category
            LoadScriptsFromCategory(analysisStack, Path.Combine(cachedScriptsPath, "Analysis"));
            LoadScriptsFromCategory(managementStack, Path.Combine(cachedScriptsPath, "Management"));
            LoadScriptsFromCategory(utilitiesStack, Path.Combine(cachedScriptsPath, "Utilities"));

            _logger.Log($"🎯 Loaded GitHub scripts from cache: {cachedScriptsPath}");
        }

        /// <summary>
        /// Load scripts from a specific category folder
        /// </summary>
        private void LoadScriptsFromCategory(StackViewModel stack, string categoryPath)
        {
            try
            {
                if (Directory.Exists(categoryPath))
                {
                    var scriptFiles = Directory.GetFiles(categoryPath, "*.py");
                    foreach (var scriptFile in scriptFiles)
                    {
                        var scriptName = Path.GetFileNameWithoutExtension(scriptFile);
                        stack.AddScript(scriptName, $"GitHub script: {scriptName}");
                    }
                }

                if (stack.Scripts.Count == 0)
                {
                    stack.AddScript("📭 No scripts", "No scripts in this category");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to load scripts from {categoryPath}: {ex.Message}");
                stack.AddScript("❌ Load Error", "Failed to load scripts from this category");
            }
        }

        /// <summary>
        /// Create placeholder content for GitHub panel
        /// </summary>
        private void CreateGitHubPlaceholderContent(PanelViewModel githubPanel, string reason)
        {
            var statusStack = githubPanel.AddStack("GitHub Status", StackLayoutType.Vertical);
            statusStack.AddScript("🔄 Refresh from GitHub", "Click refresh button to check for updates");
            statusStack.AddScript($"📭 {reason}", "Use refresh button to download scripts");
        }

        /// <summary>
        /// Handle script conflicts during refresh (renames, moves, removals)
        /// </summary>
        private void HandleScriptConflicts(PanelViewModel githubPanel)
        {
            try
            {
                // Find scripts that may have conflicts
                var conflictedScripts = new List<ScriptConflict>();

                // Check for removed scripts (scripts in layout but not in cache)
                foreach (var stack in githubPanel.Stacks.ToList())
                {
                    foreach (var script in stack.Scripts.ToList())
                    {
                        if (IsScriptRemoved(script.Name))
                        {
                            conflictedScripts.Add(new ScriptConflict
                            {
                                ScriptName = script.Name,
                                ConflictType = ConflictType.Removed,
                                OriginalStack = stack,
                                OriginalScript = script
                            });
                        }
                    }
                }

                // Handle conflicts if any found
                if (conflictedScripts.Any())
                {
                    HandleConflictResolution(conflictedScripts);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to handle script conflicts", ex);
            }
        }

        /// <summary>
        /// Check if a script has been removed from GitHub
        /// </summary>
        private bool IsScriptRemoved(string scriptName)
        {
            try
            {
                if (_gitCacheManager == null) return false;

                var cachedScriptsPath = _gitCacheManager.GetCachedScriptsPath();
                if (string.IsNullOrEmpty(cachedScriptsPath)) return false;

                // Check if script exists in any category folder
                var categories = new[] { "Analysis", "Management", "Utilities" };
                foreach (var category in categories)
                {
                    var categoryPath = Path.Combine(cachedScriptsPath, category);
                    var scriptPath = Path.Combine(categoryPath, $"{scriptName}.py");
                    if (File.Exists(scriptPath))
                    {
                        return false; // Script still exists
                    }
                }

                return true; // Script not found in any category
            }
            catch
            {
                return false; // Assume script exists if we can't check
            }
        }

        /// <summary>
        /// Handle conflict resolution with user interaction
        /// </summary>
        private void HandleConflictResolution(List<ScriptConflict> conflicts)
        {
            try
            {
                var conflictDialog = new ScriptConflictDialog(conflicts);
                if (conflictDialog.ShowDialog() == true)
                {
                    // Apply user's conflict resolution choices
                    foreach (var conflict in conflicts)
                    {
                        switch (conflict.Resolution)
                        {
                            case ConflictResolution.Remove:
                                RemoveConflictedScript(conflict);
                                break;
                            case ConflictResolution.GreyOut:
                                GreyOutConflictedScript(conflict);
                                break;
                            case ConflictResolution.Keep:
                                // Keep as-is, do nothing
                                break;
                        }
                    }

                    _logger.Log($"🔧 Resolved {conflicts.Count} script conflicts");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to handle conflict resolution", ex);
            }
        }

        /// <summary>
        /// Remove a conflicted script from the layout
        /// </summary>
        private void RemoveConflictedScript(ScriptConflict conflict)
        {
            try
            {
                conflict.OriginalStack?.RemoveScript(conflict.OriginalScript);
                _logger.Log($"🗑️ Removed conflicted script: {conflict.ScriptName}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to remove conflicted script {conflict.ScriptName}", ex);
            }
        }

        /// <summary>
        /// Grey out a conflicted script to show it's unavailable
        /// </summary>
        private void GreyOutConflictedScript(ScriptConflict conflict)
        {
            try
            {
                if (conflict.OriginalScript != null)
                {
                    conflict.OriginalScript.Name = $"❌ {conflict.ScriptName}";
                    conflict.OriginalScript.Description = "Script removed from GitHub";
                    conflict.OriginalScript.IsEnabled = false;
                    _logger.Log($"🔘 Greyed out conflicted script: {conflict.ScriptName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to grey out conflicted script {conflict.ScriptName}", ex);
            }
        }



































        /// <summary>
        /// Save layout changes
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Convert ViewModel back to LayoutManager format and save
                SaveViewModelToLayoutManager();

                _hasChanges = false;
                _viewModel.HasUnsavedChanges = false;

                _logger.Log("🎯 Saved user scripts layout changes to disk");

                // Auto-reload scripts after saving
                TriggerScriptReload();

                DialogResult = true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save layout", ex);
                MessageBox.Show($"Error saving layout: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Cancel changes
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_hasChanges)
            {
                var result = MessageBox.Show("Discard changes?", "Confirm Cancel", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No) return;
            }

            DialogResult = false;
        }

        /// <summary>
        /// Export current layout as template file
        /// </summary>
        private void ExportTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show save file dialog
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Layout Template",
                    Filter = "JSON Template Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = "json",
                    FileName = $"layout-template-{DateTime.Now:yyyy-MM-dd}.json"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    // Convert current ViewModel to layout format
                    var layout = ConvertViewModelToLayout();

                    // Serialize to JSON with formatting
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(layout, Newtonsoft.Json.Formatting.Indented);

                    // Save to file
                    File.WriteAllText(saveDialog.FileName, json);

                    _logger.Log($"📤 Exported layout template to: {saveDialog.FileName}");
                    MessageBox.Show($"Layout template exported successfully!\n\nFile: {saveDialog.FileName}\n\nYou can share this template file to update the default GitHub layout.",
                                  "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to export template", ex);
                MessageBox.Show($"Error exporting template: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Advanced Event Handlers

        /// <summary>
        /// Clear filters button click
        /// </summary>
        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ClearFilters();
        }

        /// <summary>
        /// Save layout button click (Top button - now unified with bottom button behavior)
        /// </summary>
        private void SaveLayout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.Log("💾 User clicked top Save Layout button - using unified save logic");

                // 🔥 CRITICAL FIX: Use the same comprehensive save logic as the bottom Save Layout button
                // This ensures ribbon refresh, event publishing, and proper persistence
                SaveViewModelToLayoutManager();

                _hasChanges = false;
                _viewModel.HasUnsavedChanges = false;

                _logger.Log("🎯 Top Save Layout completed - layout saved and ribbon refreshed");

                // Auto-reload scripts after saving (same as bottom button)
                TriggerScriptReload();

                // Show success message (since this button doesn't close the dialog)
                MessageBox.Show("Layout saved successfully! The ribbon has been updated.", "Save Layout", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error saving layout", ex);
                MessageBox.Show($"Error saving layout: {ex.Message}", "Save Layout", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Refresh GitHub scripts button click
        /// </summary>
        private async void RefreshGitHub_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.Log("🔄 User requested GitHub refresh");

                if (_gitCacheManager != null)
                {
                    // Disable the button during refresh
                    var button = sender as Button;
                    if (button != null)
                    {
                        button.IsEnabled = false;
                        button.Content = "🔄 Refreshing...";
                    }

                    try
                    {
                        // Refresh cache from GitHub
                        var success = await _gitCacheManager.RefreshCacheAsync(forceRefresh: true);

                        if (success)
                        {
                            MessageBox.Show("GitHub scripts refreshed successfully!",
                                "GitHub Refresh", MessageBoxButton.OK, MessageBoxImage.Information);

                            // Refresh the GitHub Scripts panel (dialog UI)
                            RefreshGitHubScriptsPanel();

                            // 🔄 NEW: Trigger safe ribbon refresh via ScriptsPlugin
                            TriggerRibbonRefresh();
                        }
                        else
                        {
                            MessageBox.Show("Failed to refresh GitHub scripts. Check network connection.",
                                "GitHub Refresh", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    finally
                    {
                        // Re-enable the button
                        if (button != null)
                        {
                            button.IsEnabled = true;
                            button.Content = "🔄 Refresh GitHub";
                        }
                    }
                }
                else
                {
                    MessageBox.Show("GitHub cache manager not available.",
                        "GitHub Refresh", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to refresh GitHub scripts: {ex.Message}", "Refresh Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _logger.LogError("Failed to refresh GitHub scripts", ex);
            }
        }

        /// <summary>
        /// 🔄 Trigger safe ribbon refresh via ScriptsPlugin
        /// </summary>
        private void TriggerRibbonRefresh()
        {
            try
            {
                var pluginManager = PluginManager.Instance;
                if (pluginManager != null)
                {
                    var scriptsPlugin = pluginManager.GetPlugin("scripts");
                    if (scriptsPlugin is ScriptsPlugin plugin)
                    {
                        _logger.Log("🔄 Requesting ribbon refresh via ScriptsPlugin");
                        plugin.RequestRibbonRefresh();
                    }
                    else
                    {
                        _logger.LogWarning("🔄 Could not access ScriptsPlugin for ribbon refresh");
                    }
                }
                else
                {
                    _logger.LogWarning("🔄 PluginManager not available for ribbon refresh");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to trigger ribbon refresh", ex);
            }
        }

        /// <summary>
        /// Refresh the GitHub Scripts panel content
        /// </summary>
        private void RefreshGitHubScriptsPanel()
        {
            try
            {
                var githubPanel = _viewModel.Panels.FirstOrDefault(p =>
                    p.Id.Equals("GitHubScripts", StringComparison.OrdinalIgnoreCase));  // 🔧 FIX: PascalCase

                if (githubPanel != null)
                {
                    // Clear existing stacks
                    githubPanel.Stacks.Clear();

                    // Get scripts that are already placed in other panels
                    var placedScripts = GetScriptsAlreadyPlacedInPanels();

                    // Reload ONLY unplaced GitHub scripts from metadata
                    var unplacedGitHubScripts = _scriptMetadata.Values
                        .Where(s => s.IsGitHubScript && !placedScripts.Contains(s.Name))
                        .ToList();

                    if (unplacedGitHubScripts.Count > 0)
                    {
                        var githubScriptsStack = githubPanel.AddStack("Available Scripts", StackLayoutType.Vertical);
                        foreach (var script in unplacedGitHubScripts)
                        {
                            githubScriptsStack.AddScript(script.Name, script.Description ?? $"GitHub script: {script.Name}");
                        }
                        _logger.Log($"🔄 Refreshed GitHub Scripts panel with {unplacedGitHubScripts.Count} unplaced scripts (total GitHub scripts: {_scriptMetadata.Values.Count(s => s.IsGitHubScript)})");
                    }
                    else
                    {
                        _logger.Log("🔄 No unplaced GitHub scripts found during refresh - all scripts are placed in other panels");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to refresh GitHub Scripts panel", ex);
            }
        }

        /// <summary>
        /// Panel header mouse down for drag initiation
        /// </summary>
        private void PanelHeader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var border = sender as Border;
                var panel = border?.DataContext as PanelViewModel;
                if (panel != null && panel.CanReorder)
                {
                    _draggedItem = panel;
                    _dragStartPoint = e.GetPosition(border);

                    // Start drag operation
                    DragDrop.DoDragDrop(border, panel, DragDropEffects.Move);
                }
            }
        }

        /// <summary>
        /// Stack header mouse down for drag initiation
        /// </summary>
        private void StackHeader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var border = sender as Border;
                var stack = border?.DataContext as StackViewModel;
                if (stack != null && stack.CanReorder)
                {
                    _draggedItem = stack;
                    _dragStartPoint = e.GetPosition(border);

                    // Start drag operation
                    DragDrop.DoDragDrop(border, stack, DragDropEffects.Move);
                }
            }
        }

        /// <summary>
        /// Script mouse down for drag initiation
        /// </summary>
        private void Script_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var border = sender as Border;
                var script = border?.DataContext as ScriptViewModel;
                if (script != null && script.CanReorder)
                {
                    _draggedItem = script;
                    _dragStartPoint = e.GetPosition(border);

                    // Start drag operation
                    DragDrop.DoDragDrop(border, script, DragDropEffects.Move);
                }
            }
        }

        /// <summary>
        /// Panel drag over
        /// </summary>
        private void Panel_DragOver(object sender, DragEventArgs e)
        {
            var targetBorder = sender as Border;
            var targetPanel = targetBorder?.DataContext as PanelViewModel;

            if (targetPanel != null)
            {
                targetPanel.IsDragOver = true;
                e.Effects = DragDropEffects.Move;
            }
            e.Handled = true;
        }

        /// <summary>
        /// Panel drag leave
        /// </summary>
        private void Panel_DragLeave(object sender, DragEventArgs e)
        {
            var targetBorder = sender as Border;
            var targetPanel = targetBorder?.DataContext as PanelViewModel;

            if (targetPanel != null)
            {
                targetPanel.IsDragOver = false;
            }
        }

        /// <summary>
        /// Panel drop
        /// </summary>
        private void Panel_Drop(object sender, DragEventArgs e)
        {
            var targetBorder = sender as Border;
            var targetPanel = targetBorder?.DataContext as PanelViewModel;

            if (targetPanel != null)
            {
                targetPanel.IsDragOver = false;

                if (e.Data.GetData(typeof(PanelViewModel)) is PanelViewModel draggedPanel && targetPanel != draggedPanel)
                {
                    // Reorder panels
                    var draggedIndex = _viewModel.Panels.IndexOf(draggedPanel);
                    var targetIndex = _viewModel.Panels.IndexOf(targetPanel);
                    _viewModel.ReorderPanels(draggedIndex, targetIndex);

                    // 🔄 AutoSaveChanges includes ribbon refresh - no need for duplicate call
                    AutoSaveChanges();

                    _logger.Log($"🎯 Reordered panel '{draggedPanel.Name}' - ribbon updated");
                }
                else if (e.Data.GetData(typeof(ScriptViewModel)) is ScriptViewModel draggedScript)
                {
                    // Move script to panel
                    MoveScriptToPanel(draggedScript, targetPanel);
                }
                else if (e.Data.GetData(typeof(StackViewModel)) is StackViewModel draggedStack)
                {
                    // Move stack to panel
                    MoveStackToPanel(draggedStack, targetPanel);
                }
            }

            e.Handled = true;
        }

        /// <summary>
        /// Stack drag over
        /// </summary>
        private void Stack_DragOver(object sender, DragEventArgs e)
        {
            var itemsControl = sender as ItemsControl;
            var stack = itemsControl?.DataContext as StackViewModel;

            if (stack != null)
            {
                stack.IsDragOver = true;
                e.Effects = DragDropEffects.Move;
            }
            e.Handled = true;
        }

        /// <summary>
        /// Stack drag leave
        /// </summary>
        private void Stack_DragLeave(object sender, DragEventArgs e)
        {
            var itemsControl = sender as ItemsControl;
            var stack = itemsControl?.DataContext as StackViewModel;

            if (stack != null)
            {
                stack.IsDragOver = false;
            }
        }

        /// <summary>
        /// Stack drop
        /// </summary>
        private void Stack_Drop(object sender, DragEventArgs e)
        {
            var itemsControl = sender as ItemsControl;
            var targetStack = itemsControl?.DataContext as StackViewModel;

            if (targetStack != null)
            {
                targetStack.IsDragOver = false;

                if (e.Data.GetData(typeof(ScriptViewModel)) is ScriptViewModel draggedScript)
                {
                    // Move script to stack
                    MoveScriptToStack(draggedScript, targetStack);
                }
            }

            e.Handled = true;
        }

        /// <summary>
        /// Script drag over
        /// </summary>
        private void Script_DragOver(object sender, DragEventArgs e)
        {
            var border = sender as Border;
            var script = border?.DataContext as ScriptViewModel;

            if (script != null)
            {
                script.IsDragOver = true;
                e.Effects = DragDropEffects.Move;
            }
            e.Handled = true;
        }

        /// <summary>
        /// Script drag leave
        /// </summary>
        private void Script_DragLeave(object sender, DragEventArgs e)
        {
            var border = sender as Border;
            var script = border?.DataContext as ScriptViewModel;

            if (script != null)
            {
                script.IsDragOver = false;
            }
        }

        /// <summary>
        /// Script drop
        /// </summary>
        private void Script_Drop(object sender, DragEventArgs e)
        {
            var border = sender as Border;
            var targetScript = border?.DataContext as ScriptViewModel;

            if (targetScript != null)
            {
                targetScript.IsDragOver = false;

                if (e.Data.GetData(typeof(ScriptViewModel)) is ScriptViewModel draggedScript && targetScript != draggedScript)
                {
                    // Reorder scripts within stack
                    ReorderScriptsInStack(draggedScript, targetScript);
                }
            }

            e.Handled = true;
        }

        /// <summary>
        /// Stack drop zone drag over
        /// </summary>
        private void StackDropZone_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        /// <summary>
        /// Stack drop zone drop
        /// </summary>
        private void StackDropZone_Drop(object sender, DragEventArgs e)
        {
            var border = sender as Border;
            var stack = border?.DataContext as StackViewModel;

            if (stack != null && e.Data.GetData(typeof(ScriptViewModel)) is ScriptViewModel draggedScript)
            {
                MoveScriptToStack(draggedScript, stack);
                // Note: MoveScriptToStack already calls TriggerRibbonRefresh()
            }

            e.Handled = true;
        }

        /// <summary>
        /// Toggle stack expansion
        /// </summary>
        private void ToggleStack_Click(object sender, MouseButtonEventArgs e)
        {
            var textBlock = sender as TextBlock;
            var stack = textBlock?.DataContext as StackViewModel;
            stack?.ToggleExpansion();
            e.Handled = true;
        }

        /// <summary>
        /// Add script button click - Show available scripts or create new
        /// </summary>
        private void AddScript_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var stack = button?.Tag as StackViewModel;

            if (stack != null)
            {
                var availableScripts = GetAvailableScripts();

                if (availableScripts.Count > 0)
                {
                    // Show dialog with available scripts
                    var dialog = new AvailableScriptsDialog(availableScripts);
                    if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedScript))
                    {
                        stack.AddScript(dialog.SelectedScript, $"User script: {dialog.SelectedScript}");
                        _viewModel.HasUnsavedChanges = true;
                        // 🔄 AutoSaveChanges includes ribbon refresh - no need for duplicate call
                        AutoSaveChanges();

                        _logger.Log($"🎯 Added script '{dialog.SelectedScript}' to stack '{stack.Name}'");
                    }
                }
                else
                {
                    // No available scripts - offer to create new one
                    var result = MessageBox.Show(
                        "No available scripts found in Scripts folder.\n\nWould you like to create a new script?",
                        "Add Script",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        var dialog = new ScriptNameDialog();
                        if (dialog.ShowDialog() == true)
                        {
                            CreateNewScriptFile(dialog.ScriptName, stack);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Add stack button click
        /// </summary>
        private void AddStack_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var panel = button?.Tag as PanelViewModel;

            if (panel != null)
            {
                var dialog = new StackNameDialog();
                if (dialog.ShowDialog() == true)
                {
                    panel.AddStack(dialog.StackName, StackLayoutType.Vertical);
                    _viewModel.HasUnsavedChanges = true;
                    AutoSaveChanges();
                }
            }
        }

        /// <summary>
        /// Add stack button drag over
        /// </summary>
        private void AddStackButton_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(ScriptViewModel)) is ScriptViewModel)
            {
                e.Effects = DragDropEffects.Move;
                // Visual feedback - change button appearance
                var button = sender as Button;
                if (button != null)
                {
                    button.Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)); // Blue highlight
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        /// <summary>
        /// Add stack button drop - Create new stack with dropped script
        /// </summary>
        private void AddStackButton_Drop(object sender, DragEventArgs e)
        {
            var button = sender as Button;
            var panel = button?.Tag as PanelViewModel;

            // Reset button appearance
            if (button != null)
            {
                button.Background = new SolidColorBrush(Color.FromRgb(236, 240, 241)); // Original color
            }

            if (panel != null && e.Data.GetData(typeof(ScriptViewModel)) is ScriptViewModel draggedScript)
            {
                // Remove script from source
                draggedScript.ParentStack?.RemoveScript(draggedScript);

                // Create new stack with a default name
                var newStackName = $"New Stack {panel.Stacks.Count + 1}";
                var newStack = panel.AddStack(newStackName, StackLayoutType.Vertical);

                // Add the script to the new stack
                draggedScript.ParentStack = newStack;
                newStack.Scripts.Add(draggedScript);

                _viewModel.HasUnsavedChanges = true;
                // 🔄 AutoSaveChanges includes ribbon refresh - no need for duplicate call
                AutoSaveChanges();

                _logger.Log($"🎯 Created new stack '{newStackName}' with script '{draggedScript.Name}' - ribbon updated");
            }

            e.Handled = true;
        }

        /// <summary>
        /// Script context menu opening
        /// </summary>
        private void Script_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var border = sender as Border;
            var script = border?.DataContext as ScriptViewModel;

            if (script != null)
            {
                // Update context menu based on script state
                var contextMenu = border.ContextMenu;
                if (contextMenu != null)
                {
                    var favoriteItem = contextMenu.Items[0] as MenuItem;
                    if (favoriteItem != null)
                    {
                        favoriteItem.Header = script.IsFavorite ? "☆ Remove Favorite" : "⭐ Add Favorite";
                    }
                }
            }
        }

        /// <summary>
        /// Toggle favorite menu item click
        /// </summary>
        private void ToggleFavorite_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var border = contextMenu?.PlacementTarget as Border;
            var script = border?.DataContext as ScriptViewModel;

            script?.ToggleFavorite();
            _viewModel.HasUnsavedChanges = true;
            AutoSaveChanges();
        }

        /// <summary>
        /// Set icon menu item click
        /// </summary>
        private void SetIcon_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var border = contextMenu?.PlacementTarget as Border;
            var script = border?.DataContext as ScriptViewModel;

            if (script != null)
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select Icon for Script",
                    Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico|All Files|*.*",
                    FilterIndex = 1
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    script.IconPath = openFileDialog.FileName;
                    _viewModel.HasUnsavedChanges = true;
                    AutoSaveChanges();

                    _logger.Log($"🖼️ Set custom icon for script '{script.Name}': {script.IconPath}");
                }
            }
        }

        /// <summary>
        /// Remove script menu item click
        /// </summary>
        private void RemoveScript_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var border = contextMenu?.PlacementTarget as Border;
            var script = border?.DataContext as ScriptViewModel;

            if (script != null && script.ParentStack != null)
            {
                // Remove from current stack
                script.ParentStack.RemoveScript(script);

                // If it's a GitHub script, return it to GitHub Scripts panel
                if (IsGitHubScript(script.Name))
                {
                    ReturnScriptToGitHubPanel(script);
                }

                _viewModel.HasUnsavedChanges = true;
                AutoSaveChanges();

                _logger.Log($"🗑️ Removed script '{script.Name}' from layout");
            }
        }

        /// <summary>
        /// Copy script menu item click
        /// </summary>
        private void CopyScript_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var border = contextMenu?.PlacementTarget as Border;
            var script = border?.DataContext as ScriptViewModel;

            if (script != null)
            {
                _copiedScript = script;
            }
        }

        /// <summary>
        /// Paste script menu item click
        /// </summary>
        private void PasteScript_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var border = contextMenu?.PlacementTarget as Border;
            var script = border?.DataContext as ScriptViewModel;

            if (_copiedScript != null && script?.ParentStack != null)
            {
                var newScript = script.ParentStack.AddScript(
                    $"{_copiedScript.Name} (Copy)",
                    _copiedScript.Description,
                    _copiedScript.IconPath);
                newScript.IsFavorite = _copiedScript.IsFavorite;
                _viewModel.HasUnsavedChanges = true;
                AutoSaveChanges();
            }
        }



        #endregion

        #region Save/Load Methods

        /// <summary>
        /// Save ViewModel data back to LayoutManager and persist to disk
        /// </summary>
        private void SaveViewModelToLayoutManager()
        {
            try
            {
                // Convert ViewModel panels back to LayoutManager format
                var updatedLayout = new RibbonLayoutSchema
                {
                    Mode = TycoonRevitAddin.Layout.LayoutMode.Manual,
                    LastModified = DateTime.UtcNow,
                    Panels = new List<PanelLayout>()
                };

                foreach (var panelVM in _viewModel.Panels)
                {
                    // Skip GitHub Scripts panel - it's not part of the main layout
                    if (panelVM.Id.Equals("GitHubScripts", StringComparison.OrdinalIgnoreCase))  // 🔧 FIX: PascalCase
                        continue;

                    var panel = new PanelLayout
                    {
                        Id = panelVM.Id,
                        Name = panelVM.Name,
                        Stacks = new List<StackLayout>()
                    };

                    int stackOrder = 0;
                    foreach (var stackVM in panelVM.Stacks)
                    {
                        // Only save stacks that have scripts
                        if (stackVM.Scripts.Any())
                        {
                            var stack = new StackLayout
                            {
                                Id = stackVM.Id,
                                Name = stackVM.Name,
                                // 🔥 NEW: Save script items with icon information
                                ScriptItems = stackVM.Scripts.Select(s => new ScriptItem
                                {
                                    Name = s.Name,
                                    IconPath = s.IconPath // Preserve the custom icon from UI
                                }).ToList(),
                                Order = stackOrder++
                            };
                            panel.Stacks.Add(stack);
                        }
                    }

                    updatedLayout.Panels.Add(panel);
                }

                // Save to LayoutManager with explicit flush
                _layoutManager.SaveUserLayout(updatedLayout);

                // 🔥 CRITICAL FIX: Publish LayoutChanged event (Chat's event-driven architecture)
                _logger.Log("📡 Publishing LayoutChanged event to notify ScriptsPlugin");
                EventBus.Instance.Publish(new LayoutChangedEvent(updatedLayout));

                // Force file system flush to ensure write completes
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();

                _logger.Log($"🎯 Saved layout with {updatedLayout.Panels.Count} panels to LayoutManager");

                // Log what was saved for debugging
                foreach (var panel in updatedLayout.Panels)
                {
                    var scriptCount = panel.Stacks.Sum(s => s.ScriptItems?.Count ?? 0);
                    _logger.Log($"🎯 Saved panel '{panel.Name}': {panel.Stacks.Count} stacks, {scriptCount} scripts");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save ViewModel to LayoutManager", ex);
                throw;
            }
        }

        /// <summary>
        /// Auto-save changes (called after drag-and-drop operations)
        /// </summary>
        private void AutoSaveChanges()
        {
            try
            {
                _logger.Log("🎯 AUTO-SAVE DEBUG: Starting AutoSaveChanges");
                SaveViewModelToLayoutManager();
                _logger.Log("🎯 AUTO-SAVE DEBUG: Layout saved to disk");

                // Add small delay to ensure file system write completes before refresh
                System.Threading.Thread.Sleep(100);

                _logger.Log("🎯 AUTO-SAVE DEBUG: Calling TriggerScriptReload");
                // Trigger immediate script reload to reflect changes in ribbon
                TriggerScriptReload();
                _logger.Log("🎯 AUTO-SAVE DEBUG: AutoSaveChanges completed");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to auto-save changes", ex);
                // Don't throw - auto-save failures shouldn't break the UI
            }
        }

        private static bool _isRefreshing = false;
        private static readonly object _refreshLock = new object();

        /// <summary>
        /// Trigger script reload to update the ribbon using event-driven architecture
        /// </summary>
        private void TriggerScriptReload()
        {
            try
            {
                lock (_refreshLock)
                {
                    if (_isRefreshing)
                    {
                        _logger.Log("🔄 Script refresh already in progress - skipping duplicate refresh");
                        return;
                    }

                    _isRefreshing = true;
                    _logger.Log("🔥 TriggerScriptReload: Using event-driven architecture for ribbon update");

                    // Get current layout state from ViewModel
                    var currentLayout = ConvertViewModelToLayout();
                    _logger.Log($"🔥 Current layout has {currentLayout.Panels.Count} panels:");
                    foreach (var panel in currentLayout.Panels)
                    {
                        var scriptCount = panel.Stacks.Sum(s => s.ScriptItems?.Count ?? 0);
                        _logger.Log($"  - Panel '{panel.Name}' (ID: {panel.Id}): {panel.Stacks.Count} stacks, {scriptCount} scripts");
                    }

                    // 🔥 CRITICAL FIX: Use the same event-driven system as SaveViewModelToLayoutManager
                    _logger.Log("🔥 Publishing LayoutChanged event for immediate ribbon update");
                    EventBus.Instance.Publish(new LayoutChangedEvent(currentLayout));
                    _logger.Log("✅ LayoutChanged event published - ribbon should update immediately");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to trigger script reload via event system", ex);
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        #endregion

        #region Script Management Methods

        /// <summary>
        /// Get scripts that exist in Scripts folder but aren't in current layout
        /// </summary>
        private List<string> GetAvailableScripts()
        {
            var availableScripts = new List<string>();

            try
            {
                var scriptsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tycoon", "Scripts");

                if (!Directory.Exists(scriptsPath))
                    return availableScripts;

                var scriptFiles = Directory.GetFiles(scriptsPath, "*.py", SearchOption.AllDirectories);
                var scriptsInLayout = GetAllScriptsInCurrentLayout();

                foreach (var scriptFile in scriptFiles)
                {
                    var scriptName = Path.GetFileNameWithoutExtension(scriptFile);

                    // Skip system files and scripts already in layout
                    if (scriptName.StartsWith("_") || scriptName.ToLower().Contains("system"))
                        continue;

                    if (!scriptsInLayout.Contains(scriptName))
                    {
                        availableScripts.Add(scriptName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to get available scripts", ex);
            }

            return availableScripts;
        }

        /// <summary>
        /// Get all script names currently in the layout
        /// </summary>
        private HashSet<string> GetAllScriptsInCurrentLayout()
        {
            var scriptsInLayout = new HashSet<string>();

            foreach (var panel in _viewModel.Panels)
            {
                foreach (var stack in panel.Stacks)
                {
                    foreach (var script in stack.Scripts)
                    {
                        scriptsInLayout.Add(script.Name);
                    }
                }
            }

            return scriptsInLayout;
        }

        /// <summary>
        /// Create a new script file and add it to the layout
        /// </summary>
        private void CreateNewScriptFile(string scriptName, StackViewModel stack)
        {
            try
            {
                var scriptsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tycoon", "Scripts");

                // Ensure Scripts directory exists
                if (!Directory.Exists(scriptsPath))
                {
                    Directory.CreateDirectory(scriptsPath);
                }

                var scriptFilePath = Path.Combine(scriptsPath, $"{scriptName}.py");

                // Check if file already exists
                if (File.Exists(scriptFilePath))
                {
                    MessageBox.Show($"Script '{scriptName}.py' already exists!", "File Exists", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Create basic script template
                var scriptTemplate = $@"# {scriptName}
# Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
# Author: User

'''
{scriptName} - User Script
Add your script description here.
'''

# Import required modules
import clr
clr.AddReference('RevitAPI')
from Autodesk.Revit.DB import *

def main():
    '''Main script function'''
    # Add your script logic here
    print(""Hello from {scriptName}!"")

if __name__ == '__main__':
    main()
";

                File.WriteAllText(scriptFilePath, scriptTemplate);

                // Add to layout
                stack.AddScript(scriptName, $"User script: {scriptName}");
                _viewModel.HasUnsavedChanges = true;

                // 🔄 AutoSaveChanges includes ribbon refresh - no need for duplicate call
                AutoSaveChanges();

                _logger.Log($"🎯 Created new script file '{scriptName}.py' and added to layout");
                MessageBox.Show($"Created new script '{scriptName}.py' successfully!\n\n🔄 Ribbon will refresh automatically to show the new script.", "Script Created", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create new script file '{scriptName}'", ex);
                MessageBox.Show($"Failed to create script file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Clean up saved layout to remove phantom scripts that don't exist in Scripts folder
        /// </summary>
        private void CleanupSavedLayout(HashSet<string> existingScripts)
        {
            try
            {
                // Use reflection to access the private LoadUserLayout method
                var loadUserLayoutMethod = typeof(RibbonLayoutManager).GetMethod("LoadUserLayout",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (loadUserLayoutMethod != null)
                {
                    var savedLayout = loadUserLayoutMethod.Invoke(_layoutManager, null) as RibbonLayoutSchema;
                    if (savedLayout != null)
                    {
                        bool needsCleanup = false;

                        // Check for phantom scripts in saved layout
                        foreach (var panel in savedLayout.Panels)
                        {
                            foreach (var stack in panel.Stacks)
                            {
                                // 🔥 NEW: Handle both old Items and new ScriptItems formats
                                // Get script names from ScriptItems
                                var scriptNames = stack.ScriptItems?.Select(si => si.Name).ToList() ?? new List<string>();

                                var phantomScripts = scriptNames.Where(item => !existingScripts.Contains(item)).ToList();
                                if (phantomScripts.Any())
                                {
                                    _logger.Log($"🧹 Found {phantomScripts.Count} phantom scripts in saved layout: {string.Join(", ", phantomScripts)}");

                                    // Remove phantom scripts from ScriptItems
                                    foreach (var phantom in phantomScripts)
                                    {
                                        stack.ScriptItems?.RemoveAll(si => si.Name == phantom);
                                    }
                                    needsCleanup = true;
                                }
                            }
                        }

                        // Save cleaned layout if needed
                        if (needsCleanup)
                        {
                            _layoutManager.SaveUserLayout(savedLayout);
                            _logger.Log("🧹 Cleaned up saved layout and removed phantom scripts");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to cleanup saved layout", ex);
                // Don't throw - cleanup failure shouldn't break script loading
            }
        }

        // REMOVED: TriggerRibbonRefresh() method - redundant with TriggerScriptReload()
        // All refresh operations now use the single TriggerScriptReload() method called by AutoSaveChanges()

        #endregion

        #region Helper Methods

        /// <summary>
        /// Move script to a different panel
        /// </summary>
        private void MoveScriptToPanel(ScriptViewModel script, PanelViewModel targetPanel)
        {
            var sourcePanel = script.ParentStack?.ParentPanel?.Name ?? "Unknown";
            _logger.Log($"🎯 MOVE DEBUG: Moving script '{script.Name}' from '{sourcePanel}' to '{targetPanel.Name}'");

            // Remove from source
            script.ParentStack?.RemoveScript(script);

            // Add to target panel's first stack (or create one)
            if (targetPanel.Stacks.Count == 0)
            {
                targetPanel.AddStack("Default Stack");
            }

            var targetStack = targetPanel.Stacks[0];
            script.ParentStack = targetStack;
            targetStack.Scripts.Add(script);

            _viewModel.HasUnsavedChanges = true;

            _logger.Log($"🎯 MOVE DEBUG: Script moved in ViewModel, calling AutoSaveChanges...");

            // 🔄 AutoSaveChanges includes both save and ribbon refresh - no need for duplicate refresh
            AutoSaveChanges();

            _logger.Log($"🎯 MOVE DEBUG: AutoSaveChanges completed for script '{script.Name}' move to '{targetPanel.Name}'");
        }

        /// <summary>
        /// Check if a script is from GitHub
        /// </summary>
        private bool IsGitHubScript(string scriptName)
        {
            try
            {
                if (_gitCacheManager == null) return false;

                var cachedScriptsPath = _gitCacheManager.GetCachedScriptsPath();
                if (string.IsNullOrEmpty(cachedScriptsPath)) return false;

                // Check if script exists in any GitHub category folder
                var categories = new[] { "Analysis", "Management", "Utilities" };
                foreach (var category in categories)
                {
                    var categoryPath = Path.Combine(cachedScriptsPath, category);
                    var scriptPath = Path.Combine(categoryPath, $"{scriptName}.py");
                    if (File.Exists(scriptPath))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to check if script '{scriptName}' is from GitHub", ex);
                return false;
            }
        }

        /// <summary>
        /// Return a removed script to the GitHub Scripts panel
        /// </summary>
        private void ReturnScriptToGitHubPanel(ScriptViewModel script)
        {
            try
            {
                // Find the GitHub Scripts panel
                var gitHubPanel = _viewModel.Panels.FirstOrDefault(p => p.Id.Equals("GitHubScripts", StringComparison.OrdinalIgnoreCase));  // 🔧 FIX: PascalCase
                if (gitHubPanel != null)
                {
                    // Check if script is already in GitHub Scripts panel
                    var existingScript = gitHubPanel.Stacks.SelectMany(s => s.Scripts).FirstOrDefault(s => s.Name == script.Name);
                    if (existingScript == null)
                    {
                        // Add to the first stack in GitHub Scripts panel
                        if (gitHubPanel.Stacks.Count == 0)
                        {
                            gitHubPanel.AddStack("Available Scripts");
                        }

                        var gitHubStack = gitHubPanel.Stacks[0];

                        // Create a new script instance for GitHub panel (don't reuse the same instance)
                        var newScript = new ScriptViewModel
                        {
                            Name = script.Name,
                            Description = script.Description ?? $"GitHub script: {script.Name}",
                            ParentStack = gitHubStack
                        };

                        gitHubStack.Scripts.Add(newScript);
                        _logger.Log($"🔄 Returned script '{script.Name}' to GitHub Scripts panel");
                    }
                    else
                    {
                        _logger.Log($"🔄 Script '{script.Name}' already exists in GitHub Scripts panel");
                    }
                }
                else
                {
                    _logger.LogWarning($"GitHub Scripts panel not found - cannot return script '{script.Name}'");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to return script '{script.Name}' to GitHub Scripts panel", ex);
            }
        }

        /// <summary>
        /// Move script to a different stack
        /// </summary>
        private void MoveScriptToStack(ScriptViewModel script, StackViewModel targetStack)
        {
            // Remove from source
            script.ParentStack?.RemoveScript(script);

            // Add to target
            script.ParentStack = targetStack;
            targetStack.Scripts.Add(script);

            _viewModel.HasUnsavedChanges = true;

            // 🔄 AutoSaveChanges includes both save and ribbon refresh - no need for duplicate refresh
            AutoSaveChanges();

            _logger.Log($"🎯 Moved script '{script.Name}' to stack '{targetStack.Name}' - layout saved and ribbon updated");
        }

        /// <summary>
        /// Move stack to a different panel
        /// </summary>
        private void MoveStackToPanel(StackViewModel stack, PanelViewModel targetPanel)
        {
            // Remove from source
            stack.ParentPanel?.RemoveStack(stack);

            // Add to target
            stack.ParentPanel = targetPanel;
            targetPanel.Stacks.Add(stack);

            _viewModel.HasUnsavedChanges = true;

            // 🔄 AutoSaveChanges includes both save and ribbon refresh - no need for duplicate refresh
            AutoSaveChanges();

            _logger.Log($"🎯 Moved stack '{stack.Name}' to panel '{targetPanel.Name}' - layout saved and ribbon updated");
        }

        /// <summary>
        /// Reorder scripts within the same stack
        /// </summary>
        private void ReorderScriptsInStack(ScriptViewModel draggedScript, ScriptViewModel targetScript)
        {
            var stack = draggedScript.ParentStack;
            if (stack != null && stack == targetScript.ParentStack)
            {
                var draggedIndex = stack.Scripts.IndexOf(draggedScript);
                var targetIndex = stack.Scripts.IndexOf(targetScript);

                if (draggedIndex != -1 && targetIndex != -1 && draggedIndex != targetIndex)
                {
                    stack.Scripts.Move(draggedIndex, targetIndex);
                    _viewModel.HasUnsavedChanges = true;

                    // 🔄 AutoSaveChanges includes both save and ribbon refresh - no need for duplicate refresh
                    AutoSaveChanges();

                    _logger.Log($"🎯 Reordered script '{draggedScript.Name}' within stack '{stack.Name}' - layout saved and ribbon updated");
                }
            }
        }

        #endregion

        #region ScriptService Integration

        /// <summary>
        /// 🎯 Load script metadata from ScriptService (replaces dictionary parameter)
        /// </summary>
        private void LoadScriptMetadataFromScriptService()
        {
            try
            {
                _scriptMetadata.Clear();

                // Get current scripts from ScriptService
                var localScripts = ScriptService.Instance.GetCurrentLocalScripts();
                var githubScripts = ScriptService.Instance.GetCurrentGitHubScripts();

                // Convert local scripts to ScriptMetadata format
                foreach (var script in localScripts)
                {
                    var metadata = new ScriptMetadata
                    {
                        Name = script.Name,
                        Description = script.Description,
                        Author = "Local",
                        CapabilityLevel = ScriptCapabilityLevel.P1_Deterministic,
                        IsGitHubScript = false,
                        SchemaVersion = "1.0.0",
                        FilePath = script.Command,
                        LastModified = DateTime.Now
                    };

                    _scriptMetadata[script.Command] = metadata;
                }

                // Convert GitHub scripts to ScriptMetadata format
                foreach (var script in githubScripts)
                {
                    var metadata = new ScriptMetadata
                    {
                        Name = script.Name,
                        Description = script.Description,
                        Author = "GitHub",
                        CapabilityLevel = ScriptCapabilityLevel.P2_Analytic,
                        IsGitHubScript = true,
                        SchemaVersion = "1.0.0",
                        FilePath = script.Command,
                        LastModified = DateTime.Now
                    };

                    _scriptMetadata[script.Command] = metadata;
                }

                _logger.Log($"🎯 Loaded script metadata from ScriptService: {localScripts.Count()} local + {githubScripts.Count()} GitHub = {_scriptMetadata.Count} total");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load script metadata from ScriptService", ex);
            }
        }

        /// <summary>
        /// Handle ScriptService local scripts updated event
        /// </summary>
        private void OnScriptServiceLocalScriptsUpdated(IEnumerable<ScriptViewModel> localScripts)
        {
            try
            {
                // Update on UI thread
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoadScriptMetadataFromScriptService();
                    RefreshViewModel();
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to handle local scripts update in StackManagerDialog", ex);
            }
        }

        /// <summary>
        /// Handle ScriptService GitHub scripts updated event
        /// </summary>
        private void OnScriptServiceGitHubScriptsUpdated(IEnumerable<ScriptViewModel> githubScripts)
        {
            try
            {
                // Update on UI thread
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoadScriptMetadataFromScriptService();
                    RefreshViewModel();
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to handle GitHub scripts update in StackManagerDialog", ex);
            }
        }

        /// <summary>
        /// Refresh the ViewModel with current script metadata
        /// </summary>
        private void RefreshViewModel()
        {
            try
            {
                // Try to load existing user layout first
                if (TryLoadExistingLayout())
                {
                    _logger.Log("🎯 Refreshed with existing user layout");
                }
                else
                {
                    // Create default layout from updated metadata
                    CreateDefaultPanelsFromMetadata();
                    _logger.Log("🎯 Refreshed with default layout from updated metadata");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to refresh StackManagerDialog ViewModel", ex);
            }
        }

        #endregion
    }

    /// <summary>
    /// Simple dialog for entering stack names
    /// </summary>
    public partial class StackNameDialog : Window
    {
        public string StackName { get; private set; }

        public StackNameDialog(string currentName = "")
        {
            Title = "Stack Name";
            Width = 300;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new Label { Content = "Stack Name:", Margin = new Thickness(10) };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            var textBox = new TextBox { Text = currentName, Margin = new Thickness(10) };
            Grid.SetRow(textBox, 1);
            grid.Children.Add(textBox);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10) };
            
            var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(5) };
            okButton.Click += (s, e) => { StackName = textBox.Text.Trim(); DialogResult = true; };
            
            var cancelButton = new Button { Content = "Cancel", Width = 75, Margin = new Thickness(5) };
            cancelButton.Click += (s, e) => DialogResult = false;

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            Content = grid;
            textBox.Focus();
            textBox.SelectAll();
        }


    }

    /// <summary>
    /// Dialog for entering script names
    /// </summary>
    public partial class ScriptNameDialog : Window
    {
        public string ScriptName { get; private set; }

        public ScriptNameDialog()
        {
            Title = "Add New Script";
            Width = 300;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Margin = new Thickness(10);

            var label = new TextBlock { Text = "Script Name:", Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            var textBox = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(textBox, 1);
            grid.Children.Add(textBox);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 5, 0) };
            var cancelButton = new Button { Content = "Cancel", Width = 75 };

            okButton.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(textBox.Text))
                {
                    ScriptName = textBox.Text.Trim();
                    DialogResult = true;
                }
            };

            cancelButton.Click += (s, e) => DialogResult = false;

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            Content = grid;

            textBox.Focus();
            textBox.SelectAll();
        }
    }

    /// <summary>
    /// Dialog for selecting from available scripts
    /// </summary>
    public partial class AvailableScriptsDialog : Window
    {
        public string SelectedScript { get; private set; }

        public AvailableScriptsDialog(List<string> availableScripts)
        {
            Title = "Add Available Script";
            Width = 400;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Margin = new Thickness(10);

            var label = new TextBlock { Text = "Select a script to add to the layout:", Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            var listBox = new ListBox { Margin = new Thickness(0, 0, 0, 10) };
            foreach (var script in availableScripts)
            {
                listBox.Items.Add(script);
            }
            if (listBox.Items.Count > 0)
            {
                listBox.SelectedIndex = 0;
            }
            Grid.SetRow(listBox, 1);
            grid.Children.Add(listBox);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okButton = new Button { Content = "Add Script", Width = 100, Margin = new Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "Cancel", Width = 75 };

            okButton.Click += (s, e) =>
            {
                if (listBox.SelectedItem != null)
                {
                    SelectedScript = listBox.SelectedItem.ToString();
                    DialogResult = true;
                }
            };

            cancelButton.Click += (s, e) => DialogResult = false;

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            Content = grid;

            listBox.Focus();
        }
    }

    /// <summary>
    /// Represents a script conflict during GitHub refresh
    /// </summary>
    public class ScriptConflict
    {
        public string ScriptName { get; set; }
        public ConflictType ConflictType { get; set; }
        public ConflictResolution Resolution { get; set; } = ConflictResolution.Keep;
        public StackViewModel OriginalStack { get; set; }
        public ScriptViewModel OriginalScript { get; set; }
        public string NewLocation { get; set; }
    }

    /// <summary>
    /// Types of script conflicts
    /// </summary>
    public enum ConflictType
    {
        Removed,
        Renamed,
        Moved
    }

    /// <summary>
    /// Conflict resolution options
    /// </summary>
    public enum ConflictResolution
    {
        Keep,
        Remove,
        GreyOut,
        Update
    }

    /// <summary>
    /// Dialog for resolving script conflicts
    /// </summary>
    public class ScriptConflictDialog : Window
    {
        private readonly List<ScriptConflict> _conflicts;

        public ScriptConflictDialog(List<ScriptConflict> conflicts)
        {
            _conflicts = conflicts;
            InitializeDialog();
        }

        private void InitializeDialog()
        {
            Title = "Resolve Script Conflicts";
            Width = 600;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Margin = new Thickness(10);

            // Header
            var header = new TextBlock
            {
                Text = $"Found {_conflicts.Count} script conflicts. Choose how to resolve them:",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            // Conflicts list
            var listView = new ListView { Margin = new Thickness(0, 0, 0, 10) };
            foreach (var conflict in _conflicts)
            {
                var item = new ConflictListItem(conflict);
                listView.Items.Add(item);
            }
            Grid.SetRow(listView, 1);
            grid.Children.Add(listView);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okButton = new Button
            {
                Content = "Apply Changes",
                Width = 120,
                Margin = new Thickness(0, 0, 10, 0)
            };
            var cancelButton = new Button { Content = "Cancel", Width = 75 };

            okButton.Click += (s, e) => { DialogResult = true; };
            cancelButton.Click += (s, e) => { DialogResult = false; };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            Content = grid;
        }
    }

    /// <summary>
    /// List item for conflict resolution
    /// </summary>
    public class ConflictListItem : StackPanel
    {
        private readonly ScriptConflict _conflict;

        public ConflictListItem(ScriptConflict conflict)
        {
            _conflict = conflict;
            Orientation = Orientation.Horizontal;
            Margin = new Thickness(0, 5, 0, 5);

            // Script name and conflict type
            var nameText = new TextBlock
            {
                Text = $"❌ {conflict.ScriptName} ({conflict.ConflictType})",
                Width = 200,
                VerticalAlignment = VerticalAlignment.Center
            };
            Children.Add(nameText);

            // Resolution options
            var resolutionCombo = new ComboBox
            {
                Width = 120,
                Margin = new Thickness(10, 0, 0, 0)
            };

            resolutionCombo.Items.Add("Keep (Grey Out)");
            resolutionCombo.Items.Add("Remove");
            resolutionCombo.Items.Add("Keep As-Is");

            resolutionCombo.SelectedIndex = 0; // Default to grey out
            resolutionCombo.SelectionChanged += (s, e) =>
            {
                switch (resolutionCombo.SelectedIndex)
                {
                    case 0: conflict.Resolution = ConflictResolution.GreyOut; break;
                    case 1: conflict.Resolution = ConflictResolution.Remove; break;
                    case 2: conflict.Resolution = ConflictResolution.Keep; break;
                }
            };

            Children.Add(resolutionCombo);
        }
    }
}
