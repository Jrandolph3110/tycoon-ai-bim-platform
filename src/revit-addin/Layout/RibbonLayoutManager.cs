using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TycoonRevitAddin.Utils;
using TycoonRevitAddin.Plugins;

namespace TycoonRevitAddin.Layout
{
    /// <summary>
    /// 🎯 Chat's Ribbon Layout Manager
    /// Implements merge logic: User Preference > Script Header > Capability Auto
    /// Handles persistence, conflict resolution, and graceful fallbacks
    /// </summary>
    public class RibbonLayoutManager
    {
        private readonly Logger _logger;
        private readonly string _layoutFilePath;
        private RibbonLayoutSchema _currentLayout;

        public RibbonLayoutManager(Logger logger)
        {
            _logger = logger;
            
            // Per-user layout persistence: %APPDATA%\Tycoon\RibbonLayout_v1.json
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var tycoonPath = Path.Combine(appDataPath, "Tycoon");
            Directory.CreateDirectory(tycoonPath);
            _layoutFilePath = Path.Combine(tycoonPath, "RibbonLayout_v1.json");
        }

        /// <summary>
        /// 🔄 Chat's Merge Logic Implementation
        /// Priority: User Preference > Script Header > Capability Auto
        /// </summary>
        public RibbonLayoutSchema MergeLayouts(Dictionary<string, ScriptMetadata> scriptMetadata)
        {
            try
            {
                _logger.Log("🎯 Starting layout merge (Chat's priority system)");

                // 1. Load user layout (highest priority)
                var userLayout = LoadUserLayout();
                
                // 2. Generate auto layout from script metadata (fallback)
                var autoLayout = GenerateAutoLayout(scriptMetadata);

                // 3. Merge with priority: User > Script Header > Capability Auto
                var mergedLayout = MergeLayoutsWithPriority(userLayout, autoLayout, scriptMetadata);

                _currentLayout = mergedLayout;
                _logger.Log($"🎯 Layout merge complete: {mergedLayout.Panels.Sum(p => p.Stacks.Count)} stacks across {mergedLayout.Panels.Count} panels");

                return mergedLayout;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to merge layouts", ex);
                return LayoutValidator.CreateDefaultLayout();
            }
        }

        /// <summary>
        /// 📂 Load User Layout from JSON
        /// Returns null if no user customization exists
        /// </summary>
        private RibbonLayoutSchema LoadUserLayout()
        {
            try
            {
                _logger.Log($"🔍 DIAGNOSTIC: Checking layout file path: {_layoutFilePath}");

                if (!File.Exists(_layoutFilePath))
                {
                    _logger.Log("📂 No user layout found - using auto mode");
                    return null;
                }

                _logger.Log($"🔍 DIAGNOSTIC: Layout file exists, reading content...");
                var json = File.ReadAllText(_layoutFilePath);
                _logger.Log($"🔍 DIAGNOSTIC: User layout bytes: {json.Length}");

                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.LogWarning("🔍 DIAGNOSTIC: Layout file is empty");
                    return null;
                }

                _logger.Log($"🔍 DIAGNOSTIC: Deserializing JSON...");
                var layout = JsonConvert.DeserializeObject<RibbonLayoutSchema>(json);

                if (layout == null)
                {
                    _logger.LogWarning("🔍 DIAGNOSTIC: Deserialized layout is null");
                    return null;
                }

                _logger.Log($"🔍 DIAGNOSTIC: Layout deserialized, validating...");
                if (LayoutValidator.IsValidLayout(layout))
                {
                    _logger.Log($"📂 Loaded user layout: {layout.Panels.Sum(p => p.Stacks.Count)} custom stacks");
                    _logger.Log($"🔍 DIAGNOSTIC: Layout validation passed - returning layout");
                    return layout;
                }
                else
                {
                    _logger.LogWarning("📂 Invalid user layout - falling back to auto mode");
                    _logger.LogWarning("🔍 DIAGNOSTIC: Layout validation failed");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"📂 Failed to load user layout: {ex.Message}");
                _logger.LogWarning($"🔍 DIAGNOSTIC: Exception details: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 🤖 Generate Auto Layout from Script Metadata
        /// Uses capability-based grouping + script header hints
        /// </summary>
        private RibbonLayoutSchema GenerateAutoLayout(Dictionary<string, ScriptMetadata> scriptMetadata)
        {
            var layout = LayoutValidator.CreateDefaultLayout();
            layout.Mode = LayoutMode.Auto;

            // Group scripts by capability and stack hints
            var scriptGroups = GroupScriptsByStackHints(scriptMetadata);

            foreach (var group in scriptGroups)
            {
                var targetPanel = GetTargetPanel(layout, group.Key.Capability, group.Key.PreferredPanel);
                if (targetPanel != null)
                {
                    var stack = new StackLayout
                    {
                        Id = group.Key.StackId ?? Guid.NewGuid().ToString(),
                        Name = group.Key.StackName ?? GetDefaultStackName(group.Key.Capability),
                        Items = group.Value.Select(s => s.Name).ToList(),
                        Capability = group.Key.Capability.ToString(),
                        Order = group.Value.Min(s => s.StackOrder)
                    };

                    targetPanel.Stacks.Add(stack);
                }
            }

            // Sort stacks by order within each panel
            foreach (var panel in layout.Panels)
            {
                panel.Stacks = panel.Stacks.OrderBy(s => s.Order).ToList();
            }

            return layout;
        }

        /// <summary>
        /// 🔀 Group Scripts by Stack Hints
        /// Groups by (StackName, Capability, PreferredPanel) combination
        /// </summary>
        private Dictionary<StackGroupKey, List<ScriptMetadata>> GroupScriptsByStackHints(Dictionary<string, ScriptMetadata> scriptMetadata)
        {
            var groups = new Dictionary<StackGroupKey, List<ScriptMetadata>>();

            foreach (var script in scriptMetadata.Values)
            {
                var key = new StackGroupKey
                {
                    StackName = script.StackName,
                    StackId = script.StackId,
                    Capability = script.CapabilityLevel,
                    PreferredPanel = script.PreferredPanel
                };

                if (!groups.ContainsKey(key))
                    groups[key] = new List<ScriptMetadata>();

                groups[key].Add(script);
            }

            return groups;
        }

        /// <summary>
        /// 🎯 Merge Layouts with Priority System
        /// User Preference > Script Header > Capability Auto
        /// </summary>
        private RibbonLayoutSchema MergeLayoutsWithPriority(RibbonLayoutSchema userLayout, RibbonLayoutSchema autoLayout, Dictionary<string, ScriptMetadata> scriptMetadata)
        {
            if (userLayout == null)
            {
                _logger.Log("🎯 No user layout - using pure auto layout");
                _logger.Log($"🔍 DIAGNOSTIC: Auto layout has {autoLayout.Panels.Count} panels with {autoLayout.Panels.Sum(p => p.Stacks.Count)} total stacks");
                return autoLayout;
            }

            _logger.Log("🎯 Merging user layout with auto layout");
            _logger.Log($"🔍 DIAGNOSTIC: User layout has {userLayout.Panels.Count} panels with {userLayout.Panels.Sum(p => p.Stacks.Count)} total stacks");

            var mergedLayout = JsonConvert.DeserializeObject<RibbonLayoutSchema>(JsonConvert.SerializeObject(userLayout));
            mergedLayout.Mode = LayoutMode.Manual;
            mergedLayout.LastModified = DateTime.UtcNow;

            // Add new scripts that aren't in user layout
            var userScripts = GetAllScriptsInLayout(userLayout);
            var newScripts = scriptMetadata.Values.Where(s => !userScripts.Contains(s.Name)).ToList();

            _logger.Log($"🔍 DIAGNOSTIC: User layout contains {userScripts.Count} scripts, found {newScripts.Count} new scripts to add");

            if (newScripts.Any())
            {
                _logger.Log($"🎯 Adding {newScripts.Count} new scripts to user layout: {string.Join(", ", newScripts.Select(s => s.Name))}");
                AddNewScriptsToLayout(mergedLayout, newScripts);
            }

            // Remove scripts that no longer exist
            RemoveObsoleteScriptsFromLayout(mergedLayout, scriptMetadata);

            _logger.Log($"🔍 DIAGNOSTIC: Final merged layout has {mergedLayout.Panels.Count} panels with {mergedLayout.Panels.Sum(p => p.Stacks.Count)} total stacks");
            return mergedLayout;
        }

        /// <summary>
        /// Get target panel for script based on capability and preference
        /// </summary>
        private PanelLayout GetTargetPanel(RibbonLayoutSchema layout, ScriptCapabilityLevel capability, string preferredPanel)
        {
            // First try preferred panel
            if (!string.IsNullOrEmpty(preferredPanel))
            {
                var preferred = layout.Panels.FirstOrDefault(p => p.Id.Equals(preferredPanel, StringComparison.OrdinalIgnoreCase));
                if (preferred != null) return preferred;
            }

            // Fall back to capability-based routing
            return capability switch
            {
                ScriptCapabilityLevel.P1_Deterministic => layout.Panels.FirstOrDefault(p => p.Id == "Production"),
                ScriptCapabilityLevel.P2_Analytic => layout.Panels.FirstOrDefault(p => p.Id == "SmartTools"),
                ScriptCapabilityLevel.P3_Adaptive => layout.Panels.FirstOrDefault(p => p.Id == "SmartTools"),
                _ => layout.Panels.FirstOrDefault(p => p.Id == "SmartTools")
            };
        }

        /// <summary>
        /// Get default stack name for capability level
        /// </summary>
        private string GetDefaultStackName(ScriptCapabilityLevel capability)
        {
            return capability switch
            {
                ScriptCapabilityLevel.P1_Deterministic => "Production Tools",
                ScriptCapabilityLevel.P2_Analytic => "Analytic Tools",
                ScriptCapabilityLevel.P3_Adaptive => "Adaptive Tools",
                _ => "General Tools"
            };
        }

        /// <summary>
        /// Get all script names currently in layout
        /// </summary>
        private HashSet<string> GetAllScriptsInLayout(RibbonLayoutSchema layout)
        {
            var scripts = new HashSet<string>();
            foreach (var panel in layout.Panels)
            {
                foreach (var stack in panel.Stacks)
                {
                    foreach (var item in stack.Items)
                    {
                        scripts.Add(item);
                    }
                }
            }
            return scripts;
        }

        /// <summary>
        /// Add new scripts to layout using their header hints
        /// </summary>
        private void AddNewScriptsToLayout(RibbonLayoutSchema layout, List<ScriptMetadata> newScripts)
        {
            foreach (var script in newScripts)
            {
                var targetPanel = GetTargetPanel(layout, script.CapabilityLevel, script.PreferredPanel);
                if (targetPanel == null) continue;

                // Find or create appropriate stack
                var targetStack = FindOrCreateStack(targetPanel, script);
                if (!targetStack.Items.Contains(script.Name))
                {
                    targetStack.Items.Add(script.Name);
                }
            }
        }

        /// <summary>
        /// Find existing stack or create new one for script
        /// </summary>
        private StackLayout FindOrCreateStack(PanelLayout panel, ScriptMetadata script)
        {
            // Try to find existing stack by name or ID
            var existingStack = panel.Stacks.FirstOrDefault(s => 
                (!string.IsNullOrEmpty(script.StackName) && s.Name == script.StackName) ||
                (!string.IsNullOrEmpty(script.StackId) && s.Id == script.StackId));

            if (existingStack != null)
                return existingStack;

            // Create new stack
            var newStack = new StackLayout
            {
                Id = script.StackId ?? Guid.NewGuid().ToString(),
                Name = script.StackName ?? GetDefaultStackName(script.CapabilityLevel),
                Capability = script.CapabilityLevel.ToString(),
                Order = script.StackOrder
            };

            panel.Stacks.Add(newStack);
            return newStack;
        }

        /// <summary>
        /// Remove scripts that no longer exist from layout
        /// </summary>
        private void RemoveObsoleteScriptsFromLayout(RibbonLayoutSchema layout, Dictionary<string, ScriptMetadata> currentScripts)
        {
            var currentScriptNames = new HashSet<string>(currentScripts.Values.Select(s => s.Name));

            foreach (var panel in layout.Panels)
            {
                foreach (var stack in panel.Stacks.ToList())
                {
                    stack.Items.RemoveAll(item => !currentScriptNames.Contains(item));
                    
                    // Remove empty stacks
                    if (!stack.Items.Any())
                    {
                        panel.Stacks.Remove(stack);
                    }
                }
            }
        }

        /// <summary>
        /// Save current layout to user file
        /// </summary>
        public void SaveUserLayout(RibbonLayoutSchema layout)
        {
            try
            {
                layout.LastModified = DateTime.UtcNow;
                var json = JsonConvert.SerializeObject(layout, Formatting.Indented);
                File.WriteAllText(_layoutFilePath, json);
                _logger.Log($"💾 Saved user layout: {_layoutFilePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save user layout", ex);
            }
        }

        /// <summary>
        /// Reset to auto layout (delete user customizations)
        /// </summary>
        public void ResetToAutoLayout()
        {
            try
            {
                if (File.Exists(_layoutFilePath))
                {
                    File.Delete(_layoutFilePath);
                    _logger.Log("🔄 Reset to auto layout - user customizations cleared");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to reset layout", ex);
            }
        }
    }

    /// <summary>
    /// Key for grouping scripts by stack characteristics
    /// </summary>
    public class StackGroupKey : IEquatable<StackGroupKey>
    {
        public string StackName { get; set; }
        public string StackId { get; set; }
        public ScriptCapabilityLevel Capability { get; set; }
        public string PreferredPanel { get; set; }

        public bool Equals(StackGroupKey other)
        {
            if (other == null) return false;
            return StackName == other.StackName && 
                   StackId == other.StackId && 
                   Capability == other.Capability && 
                   PreferredPanel == other.PreferredPanel;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (StackName?.GetHashCode() ?? 0);
                hash = hash * 23 + (StackId?.GetHashCode() ?? 0);
                hash = hash * 23 + Capability.GetHashCode();
                hash = hash * 23 + (PreferredPanel?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
