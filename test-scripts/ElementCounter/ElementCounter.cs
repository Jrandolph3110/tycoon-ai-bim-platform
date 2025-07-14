using System;
using System.Linq;
using Tycoon.Scripting.Contracts;

namespace TestScripts
{
    /// <summary>
    /// Example script that counts elements by category
    /// Demonstrates the unified script architecture with type-safe API
    /// </summary>
    public class ElementCounter : IScript
    {
        public void Execute(IRevitHost host)
        {
            try
            {
                // Get selected elements first
                var selectedElements = host.GetSelectedElements();
                
                if (selectedElements.Count > 0)
                {
                    // Analyze selected elements
                    var categoryGroups = selectedElements
                        .GroupBy(e => e.Category)
                        .OrderByDescending(g => g.Count())
                        .Take(10);
                    
                    var message = "📊 Selected Elements Analysis:\n\n";
                    foreach (var group in categoryGroups)
                    {
                        message += $"• {group.Key}: {group.Count()} elements\n";
                    }
                    
                    message += $"\n✅ Total Selected: {selectedElements.Count} elements";
                    
                    host.ShowMessage("🎯 Element Counter - Selection Analysis", message);
                }
                else
                {
                    // Count all walls in the model
                    var walls = host.GetElementsByCategory(BuiltInCategoryDto.OST_Walls);
                    var structuralFraming = host.GetElementsByCategory(BuiltInCategoryDto.OST_StructuralFraming);
                    var columns = host.GetElementsByCategory(BuiltInCategoryDto.OST_StructuralColumns);
                    var floors = host.GetElementsByCategory(BuiltInCategoryDto.OST_Floors);
                    
                    var message = "📊 Model Element Count:\n\n" +
                                 $"🧱 Walls: {walls.Count}\n" +
                                 $"🏗️ Structural Framing: {structuralFraming.Count}\n" +
                                 $"🏛️ Structural Columns: {columns.Count}\n" +
                                 $"🏢 Floors: {floors.Count}\n\n" +
                                 $"✅ Total Counted: {walls.Count + structuralFraming.Count + columns.Count + floors.Count} elements\n\n" +
                                 "💡 Tip: Select elements first for detailed analysis!";
                    
                    host.ShowMessage("🎯 Element Counter - Model Analysis", message);
                }
            }
            catch (Exception ex)
            {
                // Error handling - transaction will be automatically rolled back
                host.ShowMessage("❌ Element Counter Error", 
                    $"An error occurred while counting elements:\n\n{ex.Message}\n\n" +
                    "The operation has been safely rolled back.");
                
                // Re-throw to ensure transaction rollback
                throw;
            }
        }
    }
}
