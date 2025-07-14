using System;
using System.Linq;
using Tycoon.Scripting.Contracts;

namespace HelloWorldScript
{
    /// <summary>
    /// Example script demonstrating the new Tycoon Unified Script Architecture
    /// This script shows how to use the clean IRevitHost API for safe, type-safe script execution
    /// </summary>
    public class HelloWorldScript : IScript
    {
        public void Execute(IRevitHost host)
        {
            try
            {
                // Get selected elements
                var selectedElements = host.GetSelectedElements();
                
                // Build message based on selection
                string message;
                if (selectedElements.Count == 0)
                {
                    message = "🎯 Hello from the Unified Script Architecture!\n\n" +
                             "✅ Script executed successfully using new contracts\n" +
                             "✅ Transaction managed automatically by host\n" +
                             "✅ Type-safe API communication\n" +
                             "✅ AppDomain isolation for hot-reload\n\n" +
                             "No elements selected. Try selecting some elements and running again!";
                }
                else
                {
                    var elementInfo = selectedElements
                        .Take(5) // Show first 5 elements
                        .Select(e => $"• {e.Name} (ID: {e.Id}, Category: {e.Category})")
                        .ToArray();
                    
                    message = $"🎯 Hello from the Unified Script Architecture!\n\n" +
                             $"✅ Found {selectedElements.Count} selected element(s):\n\n" +
                             string.Join("\n", elementInfo) +
                             (selectedElements.Count > 5 ? $"\n... and {selectedElements.Count - 5} more" : "") +
                             "\n\n✅ Script executed successfully using new contracts!";
                }
                
                // Show result to user
                host.ShowMessage("🚀 Unified Script Architecture Demo", message);
            }
            catch (Exception ex)
            {
                // Error handling - host will automatically rollback transaction
                host.ShowMessage("❌ Script Error", 
                    $"An error occurred:\n\n{ex.Message}\n\n" +
                    "The transaction has been automatically rolled back.");
                
                // Re-throw to ensure transaction rollback
                throw;
            }
        }
    }
}
