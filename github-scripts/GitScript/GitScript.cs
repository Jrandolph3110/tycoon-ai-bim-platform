// ============================================================================
// GIT SCRIPT - GITHUB REFRESH TEST SCRIPT
// ============================================================================
// Simple test script to verify GitHub script refresh functionality works
// correctly in the Tycoon AI-BIM Platform.
// ============================================================================

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace GitScript
{
    /// <summary>
    /// Simple test script from GitHub repository to verify refresh functionality
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class GitScript
    {
        /// <summary>
        /// Main script execution method
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Show simple success dialog
                TaskDialog.Show(
                    "🌐 Git Script Test",
                    "✅ SUCCESS!\n\n" +
                    "This script was loaded from the GitHub repository!\n\n" +
                    "🔄 GitHub script refresh functionality is working correctly.\n" +
                    "🌐 Scripts marked with globe icon are from GitHub.\n\n" +
                    "Script Details:\n" +
                    "• Name: Git Script\n" +
                    "• Source: GitHub Repository\n" +
                    "• Panel: Smart Tools\n" +
                    "• Version: 1.0.0"
                );

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Git Script failed: {ex.Message}";
                return Result.Failed;
            }
        }
    }
}
