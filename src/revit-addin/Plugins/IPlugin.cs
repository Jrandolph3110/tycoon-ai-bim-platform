using System;
using System.Collections.Generic;
using Autodesk.Revit.UI;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.Plugins
{
    /// <summary>
    /// Interface for Tycoon plugin categories
    /// Enables dynamic loading and management of different tool sets
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Unique plugin identifier
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Display name for the plugin
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Plugin description
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Plugin version
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Plugin icon path (optional)
        /// </summary>
        string IconPath { get; }

        /// <summary>
        /// Whether this plugin is currently active
        /// </summary>
        bool IsActive { get; set; }

        /// <summary>
        /// Initialize the plugin and create its ribbon panels
        /// </summary>
        /// <param name="application">Revit UI application</param>
        /// <param name="tabName">Parent tab name</param>
        /// <returns>List of created ribbon panels</returns>
        List<RibbonPanel> Initialize(UIControlledApplication application, string tabName);

        /// <summary>
        /// Activate the plugin (show panels, enable functionality)
        /// </summary>
        void Activate();

        /// <summary>
        /// Deactivate the plugin (hide panels, disable functionality)
        /// </summary>
        void Deactivate();

        /// <summary>
        /// Cleanup plugin resources
        /// </summary>
        void Dispose();

        /// <summary>
        /// Get all ribbon panels created by this plugin
        /// </summary>
        List<RibbonPanel> GetPanels();

        /// <summary>
        /// Handle plugin-specific events or updates
        /// </summary>
        void OnUpdate();
    }

    /// <summary>
    /// Base class for Tycoon plugins with common functionality
    /// </summary>
    public abstract class PluginBase : IPlugin
    {
        protected readonly Logger _logger;
        protected readonly List<RibbonPanel> _panels = new List<RibbonPanel>();
        protected UIControlledApplication _application;
        protected string _tabName;

        public abstract string Id { get; }
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract string Version { get; }
        public virtual string IconPath => null;
        public bool IsActive { get; set; } = false;

        protected PluginBase(Logger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public virtual List<RibbonPanel> Initialize(UIControlledApplication application, string tabName)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _tabName = tabName ?? throw new ArgumentNullException(nameof(tabName));

            _panels.Clear();
            _logger.Log($"🔧 Initializing plugin: {Name}");

            // Create panels for this plugin
            CreatePanels();

            // Initially deactivate (hide panels)
            Deactivate();

            _logger.Log($"✅ Plugin {Name} initialized with {_panels.Count} panels");
            return new List<RibbonPanel>(_panels);
        }

        public virtual void Activate()
        {
            IsActive = true;
            foreach (var panel in _panels)
            {
                panel.Visible = true;
            }
            OnActivated();
            _logger.Log($"🟢 Plugin {Name} activated");
        }

        public virtual void Deactivate()
        {
            IsActive = false;
            foreach (var panel in _panels)
            {
                panel.Visible = false;
            }
            OnDeactivated();
            _logger.Log($"🔴 Plugin {Name} deactivated");
        }

        public virtual void Dispose()
        {
            _panels.Clear();
            OnCleanup();
            _logger.Log($"🗑️ Plugin {Name} disposed");
        }

        public List<RibbonPanel> GetPanels()
        {
            return new List<RibbonPanel>(_panels);
        }

        public virtual void OnUpdate()
        {
            // Override in derived classes for plugin-specific updates
        }

        /// <summary>
        /// Override to create plugin-specific panels and buttons
        /// </summary>
        protected abstract void CreatePanels();

        /// <summary>
        /// Called when plugin is activated
        /// </summary>
        protected virtual void OnActivated() { }

        /// <summary>
        /// Called when plugin is deactivated
        /// </summary>
        protected virtual void OnDeactivated() { }

        /// <summary>
        /// Called when plugin is cleaned up
        /// </summary>
        protected virtual void OnCleanup() { }

        /// <summary>
        /// Helper method to create a ribbon panel
        /// </summary>
        protected RibbonPanel CreatePanel(string panelName)
        {
            var panel = _application.CreateRibbonPanel(_tabName, panelName);
            _panels.Add(panel);
            return panel;
        }

        /// <summary>
        /// Helper method to add a push button to a panel
        /// </summary>
        protected PushButton AddPushButton(RibbonPanel panel, string buttonName, string displayName,
            string className, string tooltip = null, string iconPath = null)
        {
            var buttonData = new PushButtonData(
                buttonName,
                displayName,
                System.Reflection.Assembly.GetExecutingAssembly().Location,
                className
            );

            if (!string.IsNullOrEmpty(tooltip))
                buttonData.ToolTip = tooltip;

            if (!string.IsNullOrEmpty(iconPath))
            {
                try
                {
                    buttonData.LargeImage = LoadImage(iconPath);
                }
                catch
                {
                    // Icon not found, continue without it
                }
            }

            return panel.AddItem(buttonData) as PushButton;
        }

        /// <summary>
        /// Helper method to load images for buttons
        /// </summary>
        protected System.Windows.Media.ImageSource LoadImage(string imagePath)
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = $"TycoonRevitAddin.Resources.{imagePath}";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                        return bitmap;
                    }
                }
            }
            catch
            {
                // Fallback to file path
                return new System.Windows.Media.Imaging.BitmapImage(new Uri(imagePath));
            }

            return null;
        }
    }

    /// <summary>
    /// Plugin metadata for registration
    /// </summary>
    public class PluginMetadata
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string IconPath { get; set; }
        public Type PluginType { get; set; }
        public int SortOrder { get; set; } = 100;
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// Plugin activation event arguments
    /// </summary>
    public class PluginActivatedEventArgs : EventArgs
    {
        public IPlugin Plugin { get; set; }
        public IPlugin PreviousPlugin { get; set; }
    }
}
