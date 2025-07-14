// @capability: P1
// @description: Hello World Test Script - Compilation Test
// @author: F.L. Crane & Sons Development Team
// @version: 1.0.0
// @stack: FLC Workflow
// @stack_order: 1
// @panel: Production

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace TycoonRevitAddin.HotLoadedScripts
{
    /// <summary>
    /// Simple Hello World script to test compilation and execution
    /// </summary>
    public class Hello
    {
        public static void Execute(Document doc, UIDocument uidoc, List<int> elementIds)
        {
            TaskDialog.Show("🎉 Hello from Tycoon!", 
                "🔥 COMPILATION SUCCESS! 🔥\n\n" +
                "✅ C# script compiled successfully!\n" +
                "✅ Revit API is working!\n" +
                "✅ Tycoon platform is operational!\n" +
                "✅ GitHub script loading works!\n\n" +
                $"📄 Document: {doc?.Title ?? "Unknown"}\n" +
                $"📊 Selected Elements: {elementIds?.Count ?? 0}\n" +
                $"⏰ Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n" +
                "🌟 Ready for advanced scripts!");
        }
    }
}
