using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TycoonRevitAddin.Models;

namespace TycoonRevitAddin.Layout
{
    /// <summary>
    /// 🎯 Script Item with Icon Information
    /// Represents a single script button with all its properties
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class ScriptItem
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("iconPath")]
        public string IconPath { get; set; }

        // Future properties can be added here easily
        // public bool IsEnabled { get; set; } = true;
        // public string TooltipOverride { get; set; }
    }

    /// <summary>
    /// 🎯 Chat's Ribbon Layout Schema v1
    /// JSON-based layout persistence for user-customizable button stacking
    /// Implements: User Preference > Script Header > Capability Auto priority
    /// </summary>

    [JsonObject(MemberSerialization.OptIn)]
    public class RibbonLayoutSchema
    {
        [JsonProperty("version")]
        public int Version { get; set; } = 1;

        [JsonProperty("created")]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        [JsonProperty("lastModified")]
        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        [JsonProperty("mode")]
        public LayoutMode Mode { get; set; } = LayoutMode.Auto;

        [JsonProperty("panels")]
        public List<PanelLayout> Panels { get; set; } = new List<PanelLayout>();

        [JsonProperty("metadata")]
        public LayoutMetadata Metadata { get; set; } = new LayoutMetadata();
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class PanelLayout
    {
        [JsonProperty("id")]
        public string Id { get; set; } // "Production", "SmartTools", "Management"

        [JsonProperty("name")]
        public string Name { get; set; } // Display name

        [JsonProperty("stacks")]
        public List<StackLayout> Stacks { get; set; } = new List<StackLayout>();

        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class StackLayout
    {
        [JsonProperty("id")]
        public string Id { get; set; } // GUID for persistence

        [JsonProperty("name")]
        public string Name { get; set; } // "Framing QA", "Wall Tools", etc.

        // Script items with custom icon support
        [JsonProperty("scriptItems")]
        public List<ScriptItem> ScriptItems { get; set; } = new List<ScriptItem>();

        [JsonProperty("stackType")]
        public StackType StackType { get; set; } = StackType.Vertical;

        [JsonProperty("maxItems")]
        public int MaxItems { get; set; } = 3; // PyRevit-style 2-3 button limit

        [JsonProperty("order")]
        public int Order { get; set; } = 0; // Stack ordering within panel

        [JsonProperty("capability")]
        public string Capability { get; set; } // P1, P2, P3 - for auto-grouping fallback


    }

    [JsonObject(MemberSerialization.OptIn)]
    public class LayoutMetadata
    {
        [JsonProperty("userName")]
        public string UserName { get; set; } = Environment.UserName;

        [JsonProperty("machineName")]
        public string MachineName { get; set; } = Environment.MachineName;

        [JsonProperty("tycoonVersion")]
        public string TycoonVersion { get; set; }

        [JsonProperty("revitVersion")]
        public string RevitVersion { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; } = "User-customized ribbon layout";

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();
    }

    public enum LayoutMode
    {
        Auto,           // Pure capability-based auto-grouping
        Manual,         // User-customized layout applied
        Hybrid          // Manual with auto-fallback for new scripts
    }

    public enum StackType
    {
        Vertical,       // PyRevit-style vertical stacking (default)
        Horizontal,     // Side-by-side buttons
        Dropdown,       // PulldownButton for >3 items
        Split           // SplitButton with primary + dropdown
    }

    /// <summary>
    /// 🔧 Layout Validation and Utilities
    /// </summary>
    public static class LayoutValidator
    {
        public static bool IsValidLayout(RibbonLayoutSchema layout)
        {
            if (layout == null) return false;
            if (layout.Version != 1) return false;
            if (layout.Panels == null) return false;

            foreach (var panel in layout.Panels)
            {
                if (string.IsNullOrEmpty(panel.Id)) return false;
                if (panel.Stacks == null) return false;

                foreach (var stack in panel.Stacks)
                {
                    if (string.IsNullOrEmpty(stack.Id)) return false;

                    // A stack is valid if it has ScriptItems
                    bool hasScriptItems = stack.ScriptItems?.Any() ?? false;

                    if (!hasScriptItems) return false;
                    if (stack.MaxItems < 1 || stack.MaxItems > 5) return false;
                }
            }

            return true;
        }

        public static RibbonLayoutSchema CreateDefaultLayout()
        {
            return new RibbonLayoutSchema
            {
                Mode = LayoutMode.Auto,
                Panels = new List<PanelLayout>
                {
                    new PanelLayout
                    {
                        Id = "Production",
                        Name = "🟢 Production",
                        Stacks = new List<StackLayout>()
                    },
                    new PanelLayout
                    {
                        Id = "SmartTools",
                        Name = "🧠 Smart Tools β",
                        Stacks = new List<StackLayout>()
                    },
                    new PanelLayout
                    {
                        Id = "Management",
                        Name = "⚙️ Management",
                        Stacks = new List<StackLayout>()
                    }
                }
            };
        }
    }
}
