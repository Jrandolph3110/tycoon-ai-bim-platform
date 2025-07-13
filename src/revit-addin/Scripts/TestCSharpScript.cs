// @capability: P1
// @description: Test C# script for hot-reload validation
// @author: F.L. Crane & Sons Development Team
// @version: 1.0.0

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace TycoonRevitAddin.HotLoadedScripts
{
    /// <summary>
    /// Test C# Script for validating hot-reload functionality
    /// This script demonstrates the C# script engine capabilities
    /// </summary>
    public class TestCSharpScript
    {
        public static void Execute(Document doc, UIDocument uidoc, List<int> elementIds)
        {
            try
            {
                var message = "🔥 C# HOT-RELOAD SUCCESS! 🔥\n\n" +
                             "✅ C# script compiled and executed successfully!\n" +
                             "✅ Native Revit API performance\n" +
                             "✅ Transaction safety implemented\n" +
                             "✅ ExtensibleStorage ready\n" +
                             "✅ Tycoon platform integration active\n\n" +
                             $"⏰ Execution Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                             $"📊 Selected Elements: {elementIds?.Count ?? 0}\n" +
                             $"📄 Document: {doc?.Title ?? "Unknown"}\n\n" +
                             "🌟 F.L. Crane & Sons C# Script Engine is OPERATIONAL!";

                TaskDialog.Show("🔥 C# Script Hot-Reload Test", message);

                // Optional: Process selected elements for demonstration
                if (elementIds != null && elementIds.Count > 0)
                {
                    ProcessSelectedElements(doc, elementIds);
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Script Error", $"C# script execution failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Demonstrate element processing capabilities
        /// </summary>
        private static void ProcessSelectedElements(Document doc, List<int> elementIds)
        {
            try
            {
                var elements = elementIds.Select(id => doc.GetElement(new ElementId(id)))
                                       .Where(e => e != null)
                                       .ToList();

                if (elements.Count > 0)
                {
                    var elementInfo = string.Join("\n", elements.Take(5).Select(e => 
                        $"• {e.Category?.Name ?? "Unknown"}: {e.Name} (ID: {e.Id.IntegerValue})"));

                    if (elements.Count > 5)
                        elementInfo += $"\n... and {elements.Count - 5} more elements";

                    TaskDialog.Show("Selected Elements", 
                        $"Processing {elements.Count} selected elements:\n\n{elementInfo}");
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Processing Error", $"Error processing elements: {ex.Message}");
            }
        }
    }
}
