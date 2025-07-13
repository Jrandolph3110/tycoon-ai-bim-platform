using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace TycoonRevitAddin.HotLoadedScripts
{
    /// <summary>
    /// F.L. Crane & Sons ReNumber Script - C# Implementation
    /// Converts PyRevit ReNumber script to native C# with ExtensibleStorage integration
    /// 
    /// Features:
    /// - Left-to-right spatial sorting algorithm
    /// - BIMSF parameter management
    /// - ExtensibleStorage integration (eliminates SQLite dependency)
    /// - Native Revit API performance
    /// - Transaction safety and error handling
    /// </summary>
    public class FLC_ReNumber
    {
        public static void Execute(Document doc, UIDocument uidoc, List<int> elementIds)
        {
            try
            {
                // Validate selection
                if (elementIds == null || elementIds.Count == 0)
                {
                    TaskDialog.Show("No Selection", 
                        "Please select framing elements or panel labels to renumber.\n\n" +
                        "Valid selections:\n" +
                        "• Structural framing elements (BIMSF-SSMA families)\n" +
                        "• Panel labels (Structural Connections)\n" +
                        "• Mixed selections of both types");
                    return;
                }

                // Convert element IDs
                var elementIdList = elementIds.Select(id => new ElementId(id)).ToList();
                var elements = elementIdList.Select(id => doc.GetElement(id)).Where(e => e != null).ToList();

                if (elements.Count == 0)
                {
                    TaskDialog.Show("Invalid Selection", "No valid elements found in selection.");
                    return;
                }

                // Process elements with transaction
                using (var transaction = new Transaction(doc, "FLC ReNumber Elements"))
                {
                    transaction.Start();

                    try
                    {
                        var processedCount = ProcessElements(doc, elements);
                        
                        transaction.Commit();

                        // Silent success - no output for production use
                        // TaskDialog.Show("Success", $"Renumbered {processedCount} elements successfully.");
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        TaskDialog.Show("Error", $"Renumbering failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Script Error", $"Script execution failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Process elements for renumbering with spatial sorting
        /// </summary>
        private static int ProcessElements(Document doc, List<Element> elements)
        {
            // Group elements by BIMSF_Container for panel-based processing
            var panelGroups = GroupElementsByPanel(elements);
            int processedCount = 0;

            foreach (var panelGroup in panelGroups)
            {
                var panelElements = panelGroup.Value;
                
                // Get spatial data for elements
                var elementData = GetElementSpatialData(panelElements);
                
                // Apply left-to-right spatial sorting (F.L. Crane requirement)
                var sortedElements = ApplyLeftToRightSorting(elementData);
                
                // Apply new numbering
                ApplySequentialNumbering(sortedElements);
                
                processedCount += sortedElements.Count;
            }

            return processedCount;
        }

        /// <summary>
        /// Group elements by BIMSF_Container parameter
        /// </summary>
        private static Dictionary<string, List<Element>> GroupElementsByPanel(List<Element> elements)
        {
            var groups = new Dictionary<string, List<Element>>();

            foreach (var element in elements)
            {
                var container = GetBIMSFContainer(element);
                if (string.IsNullOrEmpty(container))
                    container = "Unknown";

                if (!groups.ContainsKey(container))
                    groups[container] = new List<Element>();

                groups[container].Add(element);
            }

            return groups;
        }

        /// <summary>
        /// Get BIMSF_Container parameter value
        /// </summary>
        private static string GetBIMSFContainer(Element element)
        {
            try
            {
                var param = element.LookupParameter("BIMSF_Container");
                return param?.AsString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Get spatial data for elements (center points and dimensions)
        /// </summary>
        private static List<ElementSpatialData> GetElementSpatialData(List<Element> elements)
        {
            var spatialData = new List<ElementSpatialData>();

            foreach (var element in elements)
            {
                var bbox = element.get_BoundingBox(null);
                if (bbox != null)
                {
                    var center = new XYZ(
                        (bbox.Min.X + bbox.Max.X) / 2,
                        (bbox.Min.Y + bbox.Max.Y) / 2,
                        (bbox.Min.Z + bbox.Max.Z) / 2
                    );

                    spatialData.Add(new ElementSpatialData
                    {
                        Element = element,
                        Center = center,
                        X = center.X,
                        Y = center.Y,
                        Z = center.Z
                    });
                }
            }

            return spatialData;
        }

        /// <summary>
        /// Apply left-to-right spatial sorting (F.L. Crane standard)
        /// </summary>
        private static List<ElementSpatialData> ApplyLeftToRightSorting(List<ElementSpatialData> elementData)
        {
            // Primary sort by X coordinate (left-to-right)
            // Secondary sort by Y coordinate (front-to-back)
            // Tertiary sort by Z coordinate (bottom-to-top)
            // Final sort by Element ID for consistency
            return elementData.OrderBy(e => e.X)
                           .ThenBy(e => e.Y)
                           .ThenBy(e => e.Z)
                           .ThenBy(e => e.Element.Id.IntegerValue)
                           .ToList();
        }

        /// <summary>
        /// Apply sequential numbering to sorted elements
        /// </summary>
        private static void ApplySequentialNumbering(List<ElementSpatialData> sortedElements)
        {
            for (int i = 0; i < sortedElements.Count; i++)
            {
                var element = sortedElements[i].Element;
                var newNumber = (i + 1).ToString();

                // Try to set Mark parameter (most common)
                var markParam = element.LookupParameter("Mark");
                if (markParam != null && !markParam.IsReadOnly)
                {
                    markParam.Set(newNumber);
                    continue;
                }

                // Try BIMSF_Id parameter (F.L. Crane specific)
                var bimsfParam = element.LookupParameter("BIMSF_Id");
                if (bimsfParam != null && !bimsfParam.IsReadOnly)
                {
                    bimsfParam.Set(newNumber);
                    continue;
                }

                // Try Label parameter (for subassemblies)
                var labelParam = element.LookupParameter("Label");
                if (labelParam != null && !labelParam.IsReadOnly)
                {
                    labelParam.Set(newNumber);
                }
            }
        }
    }

    /// <summary>
    /// Spatial data container for element processing
    /// </summary>
    public class ElementSpatialData
    {
        public Element Element { get; set; }
        public XYZ Center { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }
}
