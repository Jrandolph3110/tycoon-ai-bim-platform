using System;
using TycoonRevitAddin.Layout;

namespace TycoonRevitAddin.Events
{
    /// <summary>
    /// Event fired when Layout Manager saves layout changes
    /// Part of Chat's event-driven architecture solution
    /// </summary>
    public class LayoutChangedEvent
    {
        public RibbonLayoutSchema Layout { get; set; }
        public string Source { get; set; } = "LayoutManager";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        public LayoutChangedEvent(RibbonLayoutSchema layout)
        {
            Layout = layout;
        }
    }
}
