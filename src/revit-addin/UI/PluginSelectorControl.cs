using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TycoonRevitAddin.Plugins;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.UI
{
    /// <summary>
    /// Plugin selector control for switching between different plugin categories
    /// Provides a dropdown interface for dynamic plugin switching
    /// </summary>
    public class PluginSelectorControl : UserControl
    {
        private readonly PluginManager _pluginManager;
        private readonly Logger _logger;
        private ComboBox _pluginComboBox;
        private TextBlock _statusText;
        private Border _statusIndicator;

        public event EventHandler<PluginSelectedEventArgs> PluginSelected;

        public PluginSelectorControl(PluginManager pluginManager, Logger logger)
        {
            _pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            InitializeComponent();
            LoadPlugins();
            
            // Subscribe to plugin manager events
            _pluginManager.PluginActivated += OnPluginActivated;
        }

        private void InitializeComponent()
        {
            // Create main container
            var mainPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(5),
                VerticalAlignment = VerticalAlignment.Center
            };

            // Plugin selector label
            var label = new TextBlock
            {
                Text = "Plugin:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0),
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51))
            };

            // Plugin dropdown
            _pluginComboBox = new ComboBox
            {
                Width = 150,
                Height = 25,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            _pluginComboBox.SelectionChanged += OnPluginSelectionChanged;

            // Status indicator
            _statusIndicator = new Border
            {
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Colors.Gray),
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            // Status text
            _statusText = new TextBlock
            {
                Text = "No plugin selected",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102))
            };

            // Add controls to panel
            mainPanel.Children.Add(label);
            mainPanel.Children.Add(_pluginComboBox);
            mainPanel.Children.Add(_statusIndicator);
            mainPanel.Children.Add(_statusText);

            // Set as content
            Content = mainPanel;

            // Style the control
            Background = new SolidColorBrush(Color.FromRgb(245, 245, 245));
            BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200));
            BorderThickness = new Thickness(1);
            Padding = new Thickness(8, 4, 8, 4);
        }

        private void LoadPlugins()
        {
            try
            {
                _pluginComboBox.Items.Clear();

                var enabledPlugins = _pluginManager.GetEnabledPlugins();
                
                foreach (var pluginMetadata in enabledPlugins)
                {
                    var item = new ComboBoxItem
                    {
                        Content = pluginMetadata.Name,
                        Tag = pluginMetadata.Id,
                        ToolTip = pluginMetadata.Description
                    };

                    _pluginComboBox.Items.Add(item);
                }

                _logger.Log($"🔌 Loaded {enabledPlugins.Count} plugins in selector");

                // Select first plugin by default
                if (_pluginComboBox.Items.Count > 0)
                {
                    _pluginComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load plugins in selector", ex);
            }
        }

        private void OnPluginSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var selectedItem = _pluginComboBox.SelectedItem as ComboBoxItem;
                if (selectedItem?.Tag is string pluginId)
                {
                    _logger.Log($"🎯 Plugin selected: {pluginId}");

                    // Activate the selected plugin
                    var success = _pluginManager.ActivatePlugin(pluginId);
                    
                    if (success)
                    {
                        UpdateStatus(pluginId, true);
                        
                        // Fire event
                        PluginSelected?.Invoke(this, new PluginSelectedEventArgs
                        {
                            PluginId = pluginId,
                            Plugin = _pluginManager.GetPlugin(pluginId)
                        });
                    }
                    else
                    {
                        UpdateStatus(pluginId, false);
                        _logger.LogError($"Failed to activate plugin: {pluginId}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in plugin selection", ex);
                UpdateStatus("", false);
            }
        }

        private void OnPluginActivated(object sender, PluginActivatedEventArgs e)
        {
            // Update UI to reflect the activated plugin
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // Find and select the activated plugin in the dropdown
                    for (int i = 0; i < _pluginComboBox.Items.Count; i++)
                    {
                        var item = _pluginComboBox.Items[i] as ComboBoxItem;
                        if (item?.Tag as string == e.Plugin.Id)
                        {
                            _pluginComboBox.SelectedIndex = i;
                            break;
                        }
                    }

                    UpdateStatus(e.Plugin.Id, true);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating plugin selector UI", ex);
            }
        }

        private void UpdateStatus(string pluginId, bool isActive)
        {
            try
            {
                if (isActive && !string.IsNullOrEmpty(pluginId))
                {
                    var plugin = _pluginManager.GetPlugin(pluginId);
                    if (plugin != null)
                    {
                        _statusIndicator.Background = new SolidColorBrush(Colors.LimeGreen);
                        _statusText.Text = $"{plugin.Name} active";
                        _statusText.Foreground = new SolidColorBrush(Color.FromRgb(34, 139, 34));
                    }
                }
                else
                {
                    _statusIndicator.Background = new SolidColorBrush(Colors.OrangeRed);
                    _statusText.Text = "Plugin activation failed";
                    _statusText.Foreground = new SolidColorBrush(Color.FromRgb(178, 34, 34));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating status", ex);
            }
        }

        /// <summary>
        /// Refresh the plugin list
        /// </summary>
        public void RefreshPlugins()
        {
            LoadPlugins();
        }

        /// <summary>
        /// Select a specific plugin programmatically
        /// </summary>
        public void SelectPlugin(string pluginId)
        {
            try
            {
                for (int i = 0; i < _pluginComboBox.Items.Count; i++)
                {
                    var item = _pluginComboBox.Items[i] as ComboBoxItem;
                    if (item?.Tag as string == pluginId)
                    {
                        _pluginComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error selecting plugin {pluginId}", ex);
            }
        }

        /// <summary>
        /// Get the currently selected plugin ID
        /// </summary>
        public string GetSelectedPluginId()
        {
            var selectedItem = _pluginComboBox.SelectedItem as ComboBoxItem;
            return selectedItem?.Tag as string;
        }

        /// <summary>
        /// Cleanup when control is unloaded
        /// </summary>
        public void Cleanup()
        {
            // Unsubscribe from events
            if (_pluginManager != null)
            {
                _pluginManager.PluginActivated -= OnPluginActivated;
            }
        }
    }

    /// <summary>
    /// Event arguments for plugin selection
    /// </summary>
    public class PluginSelectedEventArgs : EventArgs
    {
        public string PluginId { get; set; }
        public IPlugin Plugin { get; set; }
    }
}
