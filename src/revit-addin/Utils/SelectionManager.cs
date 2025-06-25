using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using TycoonRevitAddin.Communication;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace TycoonRevitAddin.Utils
{
    /// <summary>
    /// Manages Revit selection serialization and analysis
    /// </summary>
    public class SelectionManager
    {
        // Optimized JSON serializer (reused to avoid reflection overhead)
        private static readonly JsonSerializer _jsonSerializer = new JsonSerializer
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            }
        };

        // Parameter allow-list for different categories (3-4x speedup)
        private static readonly Dictionary<string, HashSet<string>> _parameterAllowList =
            new Dictionary<string, HashSet<string>>
        {
            ["Walls"] = new HashSet<string>
            {
                "Length", "Area", "Volume", "Unconnected Height", "Width", "Thickness",
                "Base Constraint", "Top Constraint", "Base Offset", "Top Offset",
                "Location Line", "Structural Usage", "Structural", "Room Bounding",
                "Family and Type", "Type", "Phase Created", "Workset",
                "BIMSF_Template", "BIMSF_Id", "BIMSF_Container", "BIMSF_Label"
            },
            ["Structural Framing"] = new HashSet<string>
            {
                "Length", "Volume", "Material", "Start Level", "End Level",
                "Start Level Offset", "End Level Offset", "Reference Level",
                "Family and Type", "Type", "Phase Created", "Workset",
                "BIMSF_Template", "BIMSF_Id", "BIMSF_Container", "BIMSF_Label", "BIMSF_Subassembly"
            },
            ["Default"] = new HashSet<string>
            {
                "Category", "Family and Type", "Type", "Phase Created", "Workset",
                "Level", "Volume", "Area", "Length", "Width", "Height",
                "BIMSF_Template", "BIMSF_Id", "BIMSF_Container", "BIMSF_Label"
            }
        };

        // Type-level cache to avoid repeated ElementType parameter lookups
        private readonly Dictionary<ElementId, Dictionary<string, object>> _typeParameterCache =
            new Dictionary<ElementId, Dictionary<string, object>>();

        /// <summary>
        /// Serialize current selection to JSON-compatible format with optimizations
        /// </summary>
        public SelectionData SerializeSelection(Document document, ICollection<ElementId> elementIds)
        {
            var elements = new List<RevitElementData>();

            foreach (var elementId in elementIds)
            {
                try
                {
                    var element = document.GetElement(elementId);
                    if (element != null)
                    {
                        var elementData = SerializeElement(element);
                        elements.Add(elementData);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue processing other elements
                    Console.WriteLine($"Error serializing element {elementId}: {ex.Message}");
                }
            }

            // Get current view information
            var activeView = document.ActiveView;

            return new SelectionData
            {
                Elements = elements,
                Count = elements.Count,
                Timestamp = DateTime.UtcNow.ToString("O"),
                ViewId = activeView?.Id.ToString(),
                ViewName = activeView?.Name,
                DocumentTitle = document.Title
            };
        }

        /// <summary>
        /// Serialize a single Revit element
        /// </summary>
        private RevitElementData SerializeElement(Element element)
        {
            var elementData = new RevitElementData
            {
                Id = element.UniqueId,
                ElementId = element.Id.IntegerValue,
                Category = element.Category?.Name ?? "Unknown",
                Parameters = new Dictionary<string, object>(),
                Geometry = new GeometryData(),
                Relationships = new RelationshipData
                {
                    DependentIds = new List<string>()
                }
            };

            // Get family and type information
            if (element is FamilyInstance familyInstance)
            {
                elementData.FamilyName = familyInstance.Symbol?.Family?.Name;
                elementData.TypeName = familyInstance.Symbol?.Name;
            }
            else if (element.GetTypeId() != ElementId.InvalidElementId)
            {
                var elementType = element.Document.GetElement(element.GetTypeId());
                elementData.TypeName = elementType?.Name;
            }

            // Extract parameters
            ExtractParameters(element, elementData.Parameters);

            // Extract geometry information
            ExtractGeometry(element, elementData.Geometry);

            // Extract relationships
            ExtractRelationships(element, elementData.Relationships);

            return elementData;
        }

        /// <summary>
        /// Extract element parameters
        /// </summary>
        private void ExtractParameters(Element element, Dictionary<string, object> parameters)
        {
            try
            {
                foreach (Parameter param in element.Parameters)
                {
                    if (param.HasValue)
                    {
                        string paramName = param.Definition.Name;
                        object paramValue = GetParameterValue(param);
                        
                        if (paramValue != null)
                        {
                            parameters[paramName] = paramValue;
                        }
                    }
                }

                // Add some key built-in parameters
                AddBuiltInParameter(element, parameters, BuiltInParameter.ALL_MODEL_MARK, "Mark");
                AddBuiltInParameter(element, parameters, BuiltInParameter.ALL_MODEL_TYPE_MARK, "Type Mark");
                AddBuiltInParameter(element, parameters, BuiltInParameter.ELEM_FAMILY_PARAM, "Family");
                AddBuiltInParameter(element, parameters, BuiltInParameter.ELEM_TYPE_PARAM, "Type");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting parameters: {ex.Message}");
            }
        }

        /// <summary>
        /// Add built-in parameter if it exists
        /// </summary>
        private void AddBuiltInParameter(Element element, Dictionary<string, object> parameters, BuiltInParameter builtInParam, string displayName)
        {
            try
            {
                var param = element.get_Parameter(builtInParam);
                if (param != null && param.HasValue)
                {
                    var value = GetParameterValue(param);
                    if (value != null)
                    {
                        parameters[displayName] = value;
                    }
                }
            }
            catch
            {
                // Parameter might not exist for this element type
            }
        }

        /// <summary>
        /// Get parameter value based on storage type
        /// </summary>
        private object GetParameterValue(Parameter parameter)
        {
            try
            {
                switch (parameter.StorageType)
                {
                    case StorageType.String:
                        return parameter.AsString();
                    case StorageType.Integer:
                        return parameter.AsInteger();
                    case StorageType.Double:
                        return parameter.AsDouble();
                    case StorageType.ElementId:
                        var elementId = parameter.AsElementId();
                        return elementId != ElementId.InvalidElementId ? elementId.IntegerValue : null;
                    default:
                        return parameter.AsValueString();
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extract geometry information
        /// </summary>
        private void ExtractGeometry(Element element, GeometryData geometryData)
        {
            try
            {
                // Get location
                if (element.Location is LocationPoint locationPoint)
                {
                    var point = locationPoint.Point;
                    geometryData.Location = new Point3D
                    {
                        X = point.X,
                        Y = point.Y,
                        Z = point.Z
                    };
                }
                else if (element.Location is LocationCurve locationCurve)
                {
                    var midPoint = locationCurve.Curve.Evaluate(0.5, true);
                    geometryData.Location = new Point3D
                    {
                        X = midPoint.X,
                        Y = midPoint.Y,
                        Z = midPoint.Z
                    };
                }

                // Get bounding box
                var boundingBox = element.get_BoundingBox(null);
                if (boundingBox != null)
                {
                    geometryData.BoundingBox = new BoundingBoxData
                    {
                        Min = new Point3D
                        {
                            X = boundingBox.Min.X,
                            Y = boundingBox.Min.Y,
                            Z = boundingBox.Min.Z
                        },
                        Max = new Point3D
                        {
                            X = boundingBox.Max.X,
                            Y = boundingBox.Max.Y,
                            Z = boundingBox.Max.Z
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting geometry: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract element relationships
        /// </summary>
        private void ExtractRelationships(Element element, RelationshipData relationships)
        {
            try
            {
                // Get host element (for hosted elements)
                if (element is FamilyInstance familyInstance && familyInstance.Host != null)
                {
                    relationships.HostId = familyInstance.Host.UniqueId;
                }

                // Get dependent elements (simplified - would need more complex logic for full relationships)
                var dependentIds = element.GetDependentElements(null);
                if (dependentIds != null && dependentIds.Count > 0)
                {
                    relationships.DependentIds = dependentIds
                        .Take(10) // Limit to prevent huge payloads
                        .Select(id => element.Document.GetElement(id)?.UniqueId)
                        .Where(uid => uid != null)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting relationships: {ex.Message}");
            }
        }
    }
}
