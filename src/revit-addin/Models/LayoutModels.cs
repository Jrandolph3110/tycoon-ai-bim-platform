using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Input;
using System.Linq;

namespace TycoonRevitAddin.Models
{
    /// <summary>
    /// Represents a panel in the layout manager
    /// </summary>
    public class PanelViewModel : INotifyPropertyChanged
    {
        private string _name;
        private string _id;
        private Brush _color;
        private int _order;
        private bool _isDragOver;
        private bool _canReorder = true;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public Brush Color
        {
            get => _color;
            set { _color = value; OnPropertyChanged(); }
        }

        public int Order
        {
            get => _order;
            set { _order = value; OnPropertyChanged(); }
        }

        public bool IsDragOver
        {
            get => _isDragOver;
            set { _isDragOver = value; OnPropertyChanged(); }
        }

        public bool CanReorder
        {
            get => _canReorder;
            set { _canReorder = value; OnPropertyChanged(); }
        }

        public ObservableCollection<StackViewModel> Stacks { get; set; } = new ObservableCollection<StackViewModel>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Add a new stack to this panel
        /// </summary>
        public StackViewModel AddStack(string name, StackLayoutType layoutType = StackLayoutType.Vertical)
        {
            var stack = new StackViewModel
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                LayoutType = layoutType,
                ParentPanel = this
            };
            Stacks.Add(stack);
            return stack;
        }

        /// <summary>
        /// Remove a stack from this panel
        /// </summary>
        public bool RemoveStack(StackViewModel stack)
        {
            return Stacks.Remove(stack);
        }
    }

    /// <summary>
    /// Represents a stack within a panel
    /// </summary>
    public class StackViewModel : INotifyPropertyChanged
    {
        private string _name;
        private string _id;
        private bool _isExpanded = true;
        private StackLayoutType _layoutType = StackLayoutType.Vertical;
        private bool _isDragOver;
        private bool _canReorder = true;
        private PanelViewModel _parentPanel;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        public StackLayoutType LayoutType
        {
            get => _layoutType;
            set { _layoutType = value; OnPropertyChanged(); OnPropertyChanged(nameof(LayoutDescription)); }
        }

        public bool IsDragOver
        {
            get => _isDragOver;
            set { _isDragOver = value; OnPropertyChanged(); }
        }

        public bool CanReorder
        {
            get => _canReorder;
            set { _canReorder = value; OnPropertyChanged(); }
        }

        public PanelViewModel ParentPanel
        {
            get => _parentPanel;
            set { _parentPanel = value; OnPropertyChanged(); }
        }

        public string LayoutDescription
        {
            get
            {
                return LayoutType switch
                {
                    StackLayoutType.Vertical => "Vertical Stack",
                    StackLayoutType.Horizontal => "Horizontal Row",
                    StackLayoutType.Grid2x2 => "2x2 Grid",
                    StackLayoutType.BigSmall => "Big + Small",
                    StackLayoutType.TripleStack => "Triple Stack",
                    StackLayoutType.DualHorizontal => "Dual Horizontal",
                    _ => "Custom Layout"
                };
            }
        }

        public ObservableCollection<ScriptViewModel> Scripts { get; set; } = new ObservableCollection<ScriptViewModel>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Add a new script to this stack
        /// </summary>
        public ScriptViewModel AddScript(string name, string description = "", ButtonSize size = ButtonSize.Medium)
        {
            var script = new ScriptViewModel
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Description = description,
                Size = size,
                ParentStack = this
            };
            Scripts.Add(script);
            return script;
        }

        /// <summary>
        /// Remove a script from this stack
        /// </summary>
        public bool RemoveScript(ScriptViewModel script)
        {
            return Scripts.Remove(script);
        }

        /// <summary>
        /// Toggle expansion state
        /// </summary>
        public void ToggleExpansion()
        {
            IsExpanded = !IsExpanded;
        }
    }

    /// <summary>
    /// Represents a script/button in the layout
    /// </summary>
    public class ScriptViewModel : INotifyPropertyChanged
    {
        private string _name;
        private string _id;
        private string _description;
        private ButtonSize _size = ButtonSize.Medium;
        private bool _isFavorite;
        private DateTime _lastUsed;
        private bool _isDragOver;
        private bool _canReorder = true;
        private bool _isEnabled = true;
        private StackViewModel _parentStack;
        private string _iconPath;
        private string _command;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public ButtonSize Size
        {
            get => _size;
            set { _size = value; OnPropertyChanged(); OnPropertyChanged(nameof(SizeDescription)); }
        }

        public bool IsFavorite
        {
            get => _isFavorite;
            set { _isFavorite = value; OnPropertyChanged(); OnPropertyChanged(nameof(FavoriteIcon)); }
        }

        public DateTime LastUsed
        {
            get => _lastUsed;
            set { _lastUsed = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsRecent)); }
        }

        public bool IsDragOver
        {
            get => _isDragOver;
            set { _isDragOver = value; OnPropertyChanged(); }
        }

        public bool CanReorder
        {
            get => _canReorder;
            set { _canReorder = value; OnPropertyChanged(); }
        }

        public StackViewModel ParentStack
        {
            get => _parentStack;
            set { _parentStack = value; OnPropertyChanged(); }
        }

        public string IconPath
        {
            get => _iconPath;
            set { _iconPath = value; OnPropertyChanged(); }
        }

        public string Command
        {
            get => _command;
            set { _command = value; OnPropertyChanged(); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        // Computed properties
        public string SizeDescription
        {
            get
            {
                return Size switch
                {
                    ButtonSize.Small => "Small",
                    ButtonSize.Medium => "Medium",
                    ButtonSize.Large => "Large",
                    ButtonSize.Full => "Full Width",
                    _ => "Custom"
                };
            }
        }

        public string FavoriteIcon => IsFavorite ? "⭐" : "☆";

        public bool IsRecent => (DateTime.Now - LastUsed).TotalDays <= 7;

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Toggle favorite status
        /// </summary>
        public void ToggleFavorite()
        {
            IsFavorite = !IsFavorite;
        }

        /// <summary>
        /// Mark as recently used
        /// </summary>
        public void MarkAsUsed()
        {
            LastUsed = DateTime.Now;
        }
    }

    /// <summary>
    /// Layout types for stacks
    /// </summary>
    public enum StackLayoutType
    {
        Vertical,           // Standard vertical stack
        Horizontal,         // Horizontal row
        Grid2x2,           // 2x2 grid
        BigSmall,          // One big button on top, small below
        TripleStack,       // Three small buttons stacked
        DualHorizontal     // Two buttons side by side
    }

    /// <summary>
    /// Button sizes for PyRevit-style layouts
    /// </summary>
    public enum ButtonSize
    {
        Small,      // Small button (for triple stack, dual layouts)
        Medium,     // Standard button
        Large,      // Large button (for big+small layouts)
        Full        // Full-width button
    }

    /// <summary>
    /// Layout manager modes
    /// </summary>
    public enum LayoutMode
    {
        Design,     // Design mode - can drag and drop
        Preview,    // Preview mode - see how it will look
        Locked      // Locked mode - no changes allowed
    }

    /// <summary>
    /// Drag and drop data
    /// </summary>
    public class DragDropData
    {
        public object SourceItem { get; set; }
        public string SourceType { get; set; }
        public int SourceIndex { get; set; }
        public object SourceContainer { get; set; }
    }

    /// <summary>
    /// Layout template for saving/loading
    /// </summary>
    public class LayoutTemplate
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public string Author { get; set; }
        public List<PanelTemplate> Panels { get; set; } = new List<PanelTemplate>();
    }

    /// <summary>
    /// Panel template for serialization
    /// </summary>
    public class PanelTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ColorHex { get; set; }
        public int Order { get; set; }
        public List<StackTemplate> Stacks { get; set; } = new List<StackTemplate>();
    }

    /// <summary>
    /// Stack template for serialization
    /// </summary>
    public class StackTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public StackLayoutType LayoutType { get; set; }
        public bool IsExpanded { get; set; }
        public List<ScriptTemplate> Scripts { get; set; } = new List<ScriptTemplate>();
    }

    /// <summary>
    /// Script template for serialization
    /// </summary>
    public class ScriptTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ButtonSize Size { get; set; }
        public string IconPath { get; set; }
        public string Command { get; set; }
        public bool IsFavorite { get; set; }
    }

    /// <summary>
    /// Main layout manager view model
    /// </summary>
    public class LayoutManagerViewModel : INotifyPropertyChanged
    {
        private string _layoutName = "Custom Layout";
        private bool _hasUnsavedChanges;
        private string _searchText = "";
        private bool _showFavoritesOnly;
        private bool _showRecentOnly;
        private LayoutMode _currentMode = LayoutMode.Design;

        public string LayoutName
        {
            get => _layoutName;
            set { _layoutName = value; OnPropertyChanged(); }
        }

        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set { _hasUnsavedChanges = value; OnPropertyChanged(); }
        }

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); OnPropertyChanged(nameof(FilteredScripts)); }
        }

        public bool ShowFavoritesOnly
        {
            get => _showFavoritesOnly;
            set { _showFavoritesOnly = value; OnPropertyChanged(); OnPropertyChanged(nameof(FilteredScripts)); }
        }

        public bool ShowRecentOnly
        {
            get => _showRecentOnly;
            set { _showRecentOnly = value; OnPropertyChanged(); OnPropertyChanged(nameof(FilteredScripts)); }
        }

        public LayoutMode CurrentMode
        {
            get => _currentMode;
            set { _currentMode = value; OnPropertyChanged(); }
        }

        public ObservableCollection<PanelViewModel> Panels { get; set; } = new ObservableCollection<PanelViewModel>();
        public ObservableCollection<string> AvailableLayouts { get; set; } = new ObservableCollection<string>();

        // Computed properties
        public ObservableCollection<ScriptViewModel> FilteredScripts
        {
            get
            {
                var allScripts = new ObservableCollection<ScriptViewModel>();
                foreach (var panel in Panels)
                {
                    foreach (var stack in panel.Stacks)
                    {
                        foreach (var script in stack.Scripts)
                        {
                            if (MatchesFilter(script))
                            {
                                allScripts.Add(script);
                            }
                        }
                    }
                }
                return allScripts;
            }
        }

        public ObservableCollection<ScriptViewModel> FavoriteScripts
        {
            get
            {
                var favorites = new ObservableCollection<ScriptViewModel>();
                foreach (var panel in Panels)
                {
                    foreach (var stack in panel.Stacks)
                    {
                        foreach (var script in stack.Scripts.Where(s => s.IsFavorite))
                        {
                            favorites.Add(script);
                        }
                    }
                }
                return favorites;
            }
        }

        public ObservableCollection<ScriptViewModel> RecentScripts
        {
            get
            {
                var recent = new ObservableCollection<ScriptViewModel>();
                foreach (var panel in Panels)
                {
                    foreach (var stack in panel.Stacks)
                    {
                        foreach (var script in stack.Scripts.Where(s => s.IsRecent).OrderByDescending(s => s.LastUsed))
                        {
                            recent.Add(script);
                        }
                    }
                }
                return recent;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Check if script matches current filter
        /// </summary>
        private bool MatchesFilter(ScriptViewModel script)
        {
            if (ShowFavoritesOnly && !script.IsFavorite) return false;
            if (ShowRecentOnly && !script.IsRecent) return false;
            if (!string.IsNullOrEmpty(SearchText) &&
                script.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) == -1 &&
                script.Description.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) == -1) return false;

            return true;
        }

        /// <summary>
        /// Add a new panel
        /// </summary>
        public PanelViewModel AddPanel(string name, Brush color)
        {
            var panel = new PanelViewModel
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Color = color,
                Order = Panels.Count
            };
            Panels.Add(panel);
            HasUnsavedChanges = true;
            return panel;
        }

        /// <summary>
        /// Remove a panel
        /// </summary>
        public bool RemovePanel(PanelViewModel panel)
        {
            var result = Panels.Remove(panel);
            if (result)
            {
                // Update order of remaining panels
                for (int i = 0; i < Panels.Count; i++)
                {
                    Panels[i].Order = i;
                }
                HasUnsavedChanges = true;
            }
            return result;
        }

        /// <summary>
        /// Reorder panels
        /// </summary>
        public void ReorderPanels(int oldIndex, int newIndex)
        {
            if (oldIndex >= 0 && oldIndex < Panels.Count && newIndex >= 0 && newIndex < Panels.Count && oldIndex != newIndex)
            {
                var panel = Panels[oldIndex];
                Panels.RemoveAt(oldIndex);
                Panels.Insert(newIndex, panel);

                // Update order properties
                for (int i = 0; i < Panels.Count; i++)
                {
                    Panels[i].Order = i;
                }
                HasUnsavedChanges = true;
            }
        }

        /// <summary>
        /// Clear search and filters
        /// </summary>
        public void ClearFilters()
        {
            SearchText = "";
            ShowFavoritesOnly = false;
            ShowRecentOnly = false;
        }
    }
}
