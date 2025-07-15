using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TycoonRevitAddin.Utils;
using Tycoon.Scripting.Contracts;

namespace TycoonRevitAddin.Scripting
{
    /// <summary>
    /// DirectRevitHost - IRevitHost implementation that runs in main AppDomain
    /// Provides full Revit API access for script execution without isolation
    /// </summary>
    public class DirectRevitHost : IRevitHost
    {
        private readonly UIApplication _uiApplication;
        private readonly Document _document;
        private readonly Logger _logger;

        public DirectRevitHost(UIApplication uiApp, Document doc, Logger logger)
        {
            _uiApplication = uiApp ?? throw new ArgumentNullException(nameof(uiApp));
            _document = doc ?? throw new ArgumentNullException(nameof(doc));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region IRevitHost Implementation

        /// <summary>
        /// Gets a list of elements currently selected by the user in the Revit UI
        /// </summary>
        public List<ElementDto> GetSelectedElements()
        {
            try
            {
                var selection = _uiApplication.ActiveUIDocument.Selection;
                var selectedIds = selection.GetElementIds();
                
                var elements = new List<ElementDto>();
                foreach (var id in selectedIds)
                {
                    var element = _document.GetElement(id);
                    if (element != null)
                    {
                        elements.Add(ConvertToElementDto(element));
                    }
                }
                
                _logger.Log($"📋 Retrieved {elements.Count} selected elements");
                return elements;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to get selected elements", ex);
                throw;
            }
        }

        /// <summary>
        /// Gets all elements belonging to a specific built-in category
        /// </summary>
        public List<ElementDto> GetElementsByCategory(BuiltInCategoryDto category)
        {
            try
            {
                var builtInCategory = ConvertToBuiltInCategory(category);
                var collector = new FilteredElementCollector(_document)
                    .OfCategory(builtInCategory)
                    .WhereElementIsNotElementType();
                
                var elements = collector.Select(ConvertToElementDto).ToList();
                
                _logger.Log($"📋 Retrieved {elements.Count} elements from category {category}");
                return elements;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get elements by category {category}", ex);
                throw;
            }
        }

        /// <summary>
        /// Gets all elements of a specific family and type
        /// </summary>
        public List<ElementDto> GetElementsByType(string familyAndTypeName)
        {
            try
            {
                var collector = new FilteredElementCollector(_document)
                    .WhereElementIsNotElementType()
                    .Where(e => e.Name.Contains(familyAndTypeName) || 
                               (e.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM)?.AsValueString()?.Contains(familyAndTypeName) ?? false));
                
                var elements = collector.Select(ConvertToElementDto).ToList();
                
                _logger.Log($"📋 Retrieved {elements.Count} elements of type '{familyAndTypeName}'");
                return elements;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get elements by type '{familyAndTypeName}'", ex);
                throw;
            }
        }

        /// <summary>
        /// Gets an element by its unique ID
        /// </summary>
        public ElementDto GetElementById(int elementId)
        {
            try
            {
                var element = _document.GetElement(new ElementId(elementId));
                if (element == null)
                {
                    throw new ArgumentException($"Element with ID {elementId} not found");
                }
                
                var elementDto = ConvertToElementDto(element);
                _logger.Log($"📋 Retrieved element: {elementDto.Name} (ID: {elementId})");
                return elementDto;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get element by ID {elementId}", ex);
                throw;
            }
        }

        /// <summary>
        /// Gets all parameters for a given element
        /// </summary>
        public List<ParameterDto> GetElementParameters(int elementId)
        {
            try
            {
                var element = _document.GetElement(new ElementId(elementId));
                if (element == null)
                {
                    throw new ArgumentException($"Element with ID {elementId} not found");
                }

                var parameters = new List<ParameterDto>();
                foreach (Parameter param in element.Parameters)
                {
                    if (param != null)
                    {
                        parameters.Add(ConvertToParameterDto(param));
                    }
                }

                _logger.Log($"📋 Retrieved {parameters.Count} parameters for element {elementId}");
                return parameters;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get parameters for element {elementId}", ex);
                throw;
            }
        }

        /// <summary>
        /// Sets the value of a parameter on a given element
        /// </summary>
        public void SetElementParameter(int elementId, string parameterName, ParameterValueDto value)
        {
            try
            {
                var element = _document.GetElement(new ElementId(elementId));
                if (element == null)
                {
                    throw new ArgumentException($"Element with ID {elementId} not found");
                }

                var parameter = element.LookupParameter(parameterName);
                if (parameter == null)
                {
                    throw new ArgumentException($"Parameter '{parameterName}' not found on element {elementId}");
                }

                if (parameter.IsReadOnly)
                {
                    throw new InvalidOperationException($"Parameter '{parameterName}' is read-only");
                }

                // Set parameter value based on type
                if (value.StringValue != null)
                {
                    parameter.Set(value.StringValue);
                }
                else if (value.DoubleValue.HasValue)
                {
                    parameter.Set(value.DoubleValue.Value);
                }
                else if (value.IntValue.HasValue)
                {
                    parameter.Set(value.IntValue.Value);
                }
                else if (value.ElementIdValue.HasValue)
                {
                    parameter.Set(new ElementId(value.ElementIdValue.Value));
                }
                else
                {
                    throw new ArgumentException("No valid value provided in ParameterValueDto");
                }

                _logger.Log($"✏️ Set parameter '{parameterName}' on element {elementId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to set parameter '{parameterName}' on element {elementId}", ex);
                throw;
            }
        }

        /// <summary>
        /// Creates a new family instance in the model
        /// </summary>
        public int CreateFamilyInstance(FamilyInstanceCreationDto creationData)
        {
            try
            {
                // Find the family symbol by name
                var familySymbol = new FilteredElementCollector(_document)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(fs => $"{fs.Family.Name}: {fs.Name}" == creationData.FamilyAndTypeName);

                if (familySymbol == null)
                {
                    throw new ArgumentException($"Family symbol '{creationData.FamilyAndTypeName}' not found");
                }

                // Activate the symbol if not already active
                if (!familySymbol.IsActive)
                {
                    familySymbol.Activate();
                }

                // Get the level
                var level = _document.GetElement(new ElementId(creationData.LevelId)) as Level;
                if (level == null)
                {
                    throw new ArgumentException($"Level with ID {creationData.LevelId} not found");
                }

                // Create the instance
                var location = new XYZ(creationData.Origin.X, creationData.Origin.Y, creationData.Origin.Z);
                var instance = _document.Create.NewFamilyInstance(location, familySymbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                // Apply rotation if specified
                if (Math.Abs(creationData.RotationAngleRadians) > 1e-6)
                {
                    var axis = Line.CreateBound(location, location + XYZ.BasisZ);
                    ElementTransformUtils.RotateElement(_document, instance.Id, axis, creationData.RotationAngleRadians);
                }

                _logger.Log($"🏗️ Created family instance: {creationData.FamilyAndTypeName} (ID: {instance.Id.IntegerValue})");
                return instance.Id.IntegerValue;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create family instance '{creationData.FamilyAndTypeName}'", ex);
                throw;
            }
        }

        /// <summary>
        /// Displays a simple task dialog to the user
        /// </summary>
        public void ShowMessage(string title, string message)
        {
            try
            {
                TaskDialog.Show(title, message);
                _logger.Log($"💬 Showed message dialog: {title}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to show message dialog: {title}", ex);
                throw;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Convert Revit Element to ElementDto
        /// </summary>
        private ElementDto ConvertToElementDto(Element element)
        {
            return new ElementDto
            {
                Id = element.Id.IntegerValue,
                Name = element.Name ?? "Unnamed",
                Category = element.Category?.Name ?? "Unknown",
                LevelId = element.LevelId?.IntegerValue ?? -1
            };
        }

        /// <summary>
        /// Convert Revit Parameter to ParameterDto
        /// </summary>
        private ParameterDto ConvertToParameterDto(Parameter parameter)
        {
            var paramDto = new ParameterDto
            {
                Name = parameter.Definition.Name,
                StorageType = parameter.StorageType.ToString(),
                IsReadOnly = parameter.IsReadOnly,
                Value = new ParameterValueDto()
            };

            // Set value based on storage type
            switch (parameter.StorageType)
            {
                case StorageType.String:
                    paramDto.Value.StringValue = parameter.AsString();
                    break;
                case StorageType.Double:
                    paramDto.Value.DoubleValue = parameter.AsDouble();
                    break;
                case StorageType.Integer:
                    paramDto.Value.IntValue = parameter.AsInteger();
                    break;
                case StorageType.ElementId:
                    paramDto.Value.ElementIdValue = parameter.AsElementId()?.IntegerValue;
                    break;
            }

            return paramDto;
        }

        /// <summary>
        /// Convert BuiltInCategoryDto to BuiltInCategory
        /// </summary>
        private BuiltInCategory ConvertToBuiltInCategory(BuiltInCategoryDto category)
        {
            return category switch
            {
                BuiltInCategoryDto.OST_Walls => BuiltInCategory.OST_Walls,
                BuiltInCategoryDto.OST_Floors => BuiltInCategory.OST_Floors,
                BuiltInCategoryDto.OST_StructuralFraming => BuiltInCategory.OST_StructuralFraming,
                BuiltInCategoryDto.OST_StructuralColumns => BuiltInCategory.OST_StructuralColumns,
                BuiltInCategoryDto.OST_Doors => BuiltInCategory.OST_Doors,
                BuiltInCategoryDto.OST_Windows => BuiltInCategory.OST_Windows,
                BuiltInCategoryDto.OST_GenericModel => BuiltInCategory.OST_GenericModel,
                _ => throw new ArgumentException($"Unsupported category: {category}")
            };
        }

        #endregion
    }
}
