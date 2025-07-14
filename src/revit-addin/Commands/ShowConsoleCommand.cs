using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.Commands
{
    /// <summary>
    /// Command to show/hide the Tycoon Console window
    /// Provides PyRevit-style console access for script development
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class ShowConsoleCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (TycoonConsoleManager.IsConsoleVisible())
                {
                    TycoonConsoleManager.HideConsole();
                }
                else
                {
                    TycoonConsoleManager.ShowConsole();
                    TycoonConsoleManager.AppendLog("🔥 Console opened manually", LogLevel.Info);
                    TycoonConsoleManager.AppendLog("💡 Tip: Use Shift+Click on script buttons for automatic console display", LogLevel.Info);
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Error toggling console: {ex.Message}";
                return Result.Failed;
            }
        }
    }
}
