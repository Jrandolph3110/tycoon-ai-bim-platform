using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.Plugins
{
    /// <summary>
    /// Central manager for Tycoon plugin system
    /// Handles plugin registration, activation, and dynamic panel switching
    /// </summary>
    public class PluginManager : IDisposable
    {
        private readonly Logger _logger;
        private readonly Dictionary<string, IPlugin> _plugins;
        private readonly Dictionary<string, PluginMetadata> _pluginMetadata;
        private IPlugin _activePlugin;
        private UIControlledApplication _application;
        private string _tabName;
        private bool _disposed = false;

        /// <summary>
        /// Event fired when a plugin is activated
        /// </summary>
        public event EventHandler<PluginActivatedEventArgs> PluginActivated;

        /// <summary>
        /// Currently active plugin
        /// </summary>
        public IPlugin ActivePlugin => _activePlugin;

        /// <summary>
        /// All registered plugins
        /// </summary>
        public IReadOnlyDictionary<string, IPlugin> Plugins => _plugins;

        /// <summary>
        /// All plugin metadata
        /// </summary>
        public IReadOnlyDictionary<string, PluginMetadata> PluginMetadata => _pluginMetadata;

        public PluginManager(Logger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _plugins = new Dictionary<string, IPlugin>();
            _pluginMetadata = new Dictionary<string, PluginMetadata>();
        }

        /// <summary>
        /// Initialize the plugin manager with Revit application
        /// </summary>
        public void Initialize(UIControlledApplication application, string tabName)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _tabName = tabName ?? throw new ArgumentNullException(nameof(tabName));

            _logger.Log("🔌 Initializing Plugin Manager");

            // Register built-in plugins
            RegisterBuiltInPlugins();

            // Initialize all plugins
            InitializePlugins();

            _logger.Log($"✅ Plugin Manager initialized with {_plugins.Count} plugins");
        }

        /// <summary>
        /// Register a plugin
        /// </summary>
        public void RegisterPlugin(PluginMetadata metadata, IPlugin plugin)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            if (plugin == null) throw new ArgumentNullException(nameof(plugin));

            if (_plugins.ContainsKey(metadata.Id))
            {
                _logger.LogWarning($"Plugin {metadata.Id} already registered, skipping");
                return;
            }

            _pluginMetadata[metadata.Id] = metadata;
            _plugins[metadata.Id] = plugin;

            _logger.Log($"📦 Registered plugin: {metadata.Name} v{metadata.Version}");
        }

        /// <summary>
        /// Activate a plugin by ID
        /// </summary>
        public bool ActivatePlugin(string pluginId)
        {
            if (string.IsNullOrEmpty(pluginId))
            {
                _logger.LogError("Plugin ID cannot be null or empty");
                return false;
            }

            if (!_plugins.TryGetValue(pluginId, out var plugin))
            {
                _logger.LogError($"Plugin {pluginId} not found");
                return false;
            }

            if (!_pluginMetadata[pluginId].IsEnabled)
            {
                _logger.LogWarning($"Plugin {pluginId} is disabled");
                return false;
            }

            try
            {
                // Deactivate current plugin
                var previousPlugin = _activePlugin;
                if (_activePlugin != null && _activePlugin.Id != pluginId)
                {
                    _activePlugin.Deactivate();
                    _logger.Log($"🔄 Deactivated plugin: {_activePlugin.Name}");
                }

                // Activate new plugin
                plugin.Activate();
                _activePlugin = plugin;

                _logger.Log($"✅ Activated plugin: {plugin.Name}");

                // Fire event
                PluginActivated?.Invoke(this, new PluginActivatedEventArgs
                {
                    Plugin = plugin,
                    PreviousPlugin = previousPlugin
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to activate plugin {pluginId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Get plugin by ID
        /// </summary>
        public IPlugin GetPlugin(string pluginId)
        {
            return _plugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
        }

        /// <summary>
        /// Get all enabled plugins sorted by order
        /// </summary>
        public List<PluginMetadata> GetEnabledPlugins()
        {
            return _pluginMetadata.Values
                .Where(m => m.IsEnabled)
                .OrderBy(m => m.SortOrder)
                .ThenBy(m => m.Name)
                .ToList();
        }

        /// <summary>
        /// Register built-in plugins
        /// </summary>
        private void RegisterBuiltInPlugins()
        {
            // Scripts Plugin (PyRevit-style)
            RegisterPlugin(
                new PluginMetadata
                {
                    Id = "scripts",
                    Name = "Scripts",
                    Description = "PyRevit-style script execution and management",
                    Version = "1.0.0",
                    IconPath = "Resources/ScriptsIcon.png",
                    SortOrder = 10
                },
                new ScriptsPlugin(_logger)
            );

            // Tycoon Pro FrAimer Plugin
            RegisterPlugin(
                new PluginMetadata
                {
                    Id = "tycoon-pro-fraimer",
                    Name = "Tycoon Pro FrAimer",
                    Description = "Advanced FLC steel framing tools and workflows",
                    Version = "1.0.0",
                    IconPath = "Resources/FramerIcon.png",
                    SortOrder = 20
                },
                new TycoonProFrAimerPlugin(_logger)
            );

            // AI Actions Plugin
            RegisterPlugin(
                new PluginMetadata
                {
                    Id = "ai-actions",
                    Name = "AI Actions",
                    Description = "Revolutionary AI-driven Revit automation with enterprise-grade safety",
                    Version = "1.0.0",
                    IconPath = "Resources/AIActionsIcon.png",
                    SortOrder = 30
                },
                new AIActionsPlugin(_logger)
            );
        }

        /// <summary>
        /// Initialize all registered plugins
        /// </summary>
        private void InitializePlugins()
        {
            foreach (var kvp in _plugins)
            {
                try
                {
                    var panels = kvp.Value.Initialize(_application, _tabName);
                    _logger.Log($"🔧 Initialized plugin {kvp.Key} with {panels?.Count ?? 0} panels");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to initialize plugin {kvp.Key}", ex);
                }
            }
        }

        /// <summary>
        /// Dispose of all plugins and resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            foreach (var plugin in _plugins.Values)
            {
                try
                {
                    plugin?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error disposing plugin {plugin?.Id}", ex);
                }
            }

            _plugins.Clear();
            _pluginMetadata.Clear();
            _activePlugin = null;
            _disposed = true;

            _logger.Log("🔌 Plugin Manager disposed");
        }
    }
}
