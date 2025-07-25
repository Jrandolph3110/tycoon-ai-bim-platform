using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TycoonRevitAddin.Utils;
using Tycoon.Scripting.Contracts;

namespace TycoonRevitAddin.Scripting
{
    /// <summary>
    /// ScriptProxy - MarshalByRefObject that implements IRevitHost
    /// Executes in the main AppDomain but provides gateway for scripts in isolated AppDomain
    /// Manages all Revit API interactions and transaction lifecycle
    /// </summary>
    public class ScriptProxy : MarshalByRefObject, IRevitHost
    {
        private readonly Logger _logger;
        private Document _document;
        private UIApplication _uiApplication;
        private Transaction _currentTransaction;

        public ScriptProxy()
        {
            // Create logger in the new AppDomain to avoid serialization issues
            _logger = new Logger("ScriptProxy", debugMode: true);

            // Set up assembly resolution for dependencies in isolated AppDomain
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            // Get current Revit context
            // Note: This will be injected properly when integrated with main application
            _logger.Log("🔗 ScriptProxy created in isolated AppDomain");
        }

        /// <summary>
        /// Assembly resolution handler for isolated AppDomain
        /// </summary>
        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                _logger.Log($"🔍 Resolving assembly: {args.Name}");

                // Extract simple name from full assembly name
                string assemblyName = new AssemblyName(args.Name).Name;

                // Special handling for Revit API assemblies
                if (assemblyName == "RevitAPI" || assemblyName == "RevitAPIUI")
                {
                    _logger.Log($"🔧 Handling Revit API assembly: {assemblyName}");

                    // Try to find Revit API assemblies in common locations
                    string[] revitPaths = {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Autodesk", "Revit 2024"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Autodesk", "Revit 2023"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Autodesk", "Revit 2022"),
                        // Also check where TycoonRevitAddin is located (likely same directory)
                        Path.GetDirectoryName(typeof(ScriptProxy).Assembly.Location)
                    };

                    foreach (string revitPath in revitPaths)
                    {
                        string revitAssemblyPath = Path.Combine(revitPath, assemblyName + ".dll");
                        if (File.Exists(revitAssemblyPath))
                        {
                            _logger.Log($"✅ Found Revit API assembly at: {revitAssemblyPath}");
                            return Assembly.LoadFrom(revitAssemblyPath);
                        }
                    }
                }

                // Look for assembly in the same directory as ScriptProxy
                string assemblyPath = Path.Combine(
                    Path.GetDirectoryName(typeof(ScriptProxy).Assembly.Location),
                    assemblyName + ".dll"
                );

                if (File.Exists(assemblyPath))
                {
                    _logger.Log($"✅ Found assembly at: {assemblyPath}");
                    return Assembly.LoadFrom(assemblyPath);
                }

                _logger.Log($"❌ Assembly not found: {assemblyName}");
                return null; // Let default loader continue
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error resolving assembly {args.Name}", ex);
                return null;
            }
        }

        /// <summary>
        /// Initialize proxy - gets Revit context from current application
        /// Note: In isolated AppDomain, this will be limited. Consider using main AppDomain for Revit API access.
        /// </summary>
        public void Initialize()
        {
            try
            {
                // In isolated AppDomain, we can't easily access the main Revit application
                // For now, we'll note this limitation and execute scripts without full Revit context
                _logger.Log("🔗 ScriptProxy initialized in isolated AppDomain (limited Revit API access)");

                // TODO: Implement bridge pattern for Revit API calls back to main AppDomain
                _uiApplication = null; // Will be null in isolated AppDomain
                _document = null; // Will be null in isolated AppDomain
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize ScriptProxy", ex);
                throw;
            }
        }

        /// <summary>
        /// Execute a script with automatic transaction management
        /// </summary>
        public bool ExecuteScript(ScriptDefinition scriptDefinition)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.Log($"🚀 Executing script: {scriptDefinition.Name}");

                // Load script assembly
                var assembly = Assembly.LoadFrom(scriptDefinition.AssemblyPath);
                var scriptType = assembly.GetType(scriptDefinition.ClassName);

                if (scriptType == null)
                {
                    throw new InvalidOperationException($"Script type '{scriptDefinition.ClassName}' not found in assembly");
                }

                // Create script instance
                var scriptInstance = Activator.CreateInstance(scriptType) as IScript;
                if (scriptInstance == null)
                {
                    throw new InvalidOperationException($"Script type '{scriptDefinition.ClassName}' does not implement IScript");
                }

                // Execute with transaction management
                ExecuteWithTransaction(scriptInstance, scriptDefinition.Name);
                
                stopwatch.Stop();
                _logger.Log($"✅ Script '{scriptDefinition.Name}' executed successfully in {stopwatch.ElapsedMilliseconds}ms");

                return true;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError($"❌ Script '{scriptDefinition.Name}' failed", ex);

                return false;
            }
        }

        /// <summary>
        /// Execute script with automatic transaction management
        /// </summary>
        private void ExecuteWithTransaction(IScript script, string scriptName)
        {
            // Check if we have Revit context (may be null in isolated AppDomain)
            if (_document == null)
            {
                _logger.Log($"⚠️ No Revit document context - executing script without transaction management");
                // Execute script directly without transaction (limited functionality)
                script.Execute(this);
                return;
            }

            using (var transaction = new Transaction(_document, $"Tycoon Script: {scriptName}"))
            {
                _currentTransaction = transaction;
                
                try
                {
                    transaction.Start();
                    _logger.Log($"🔄 Transaction started for script: {scriptName}");
                    
                    // Execute script - any exceptions will cause rollback
                    script.Execute(this);
                    
                    transaction.Commit();
                    _logger.Log($"✅ Transaction committed for script: {scriptName}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Script execution failed, rolling back transaction", ex);
                    
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        transaction.RollBack();
                        _logger.Log($"🔄 Transaction rolled back for script: {scriptName}");
                    }
                    
                    throw; // Re-throw to be handled by caller
                }
                finally
                {
                    _currentTransaction = null;
                }
            }
        }

        #region IRevitHost Implementation

        /// <summary>
        /// Gets a list of elements currently selected by the user in the Revit UI
        /// </summary>
        public List<ElementDto> GetSelectedElements()
        {
            try
            {
                // Check if we have Revit context (may be null in isolated AppDomain)
                if (_uiApplication == null || _document == null)
                {
                    _logger.Log("⚠️ No Revit context available - returning empty selection");
                    throw new InvalidOperationException("Revit API access not available in isolated AppDomain. Consider using main AppDomain for full Revit functionality.");
                }

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
                // Check if we have Revit context (may be null in isolated AppDomain)
                if (_document == null)
                {
                    _logger.Log($"⚠️ No Revit document context - cannot get elements by category {category}");
                    throw new InvalidOperationException("Revit API access not available in isolated AppDomain. Consider using main AppDomain for full Revit functionality.");
                }

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
                // This is a simplified implementation - in production, you'd parse the family and type name
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
        /// Retrieves all parameters for a given element
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
                    if (param != null && param.HasValue)
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
                SetParameterValue(parameter, value);
                
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
                // This is a simplified implementation - in production, you'd need more robust family/type lookup
                throw new NotImplementedException("CreateFamilyInstance will be implemented in next iteration");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create family instance", ex);
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
                // In isolated AppDomain, TaskDialog might not work properly
                // For now, just log the message - in production, implement bridge pattern
                if (_uiApplication == null)
                {
                    _logger.Log($"💬 [ISOLATED APPDOMAIN] {title}: {message}");
                    return;
                }

                TaskDialog.Show(title, message);
                _logger.Log($"💬 Showed message dialog: {title}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to show message dialog: {title}", ex);
                // In isolated AppDomain, fall back to logging
                _logger.Log($"💬 [FALLBACK] {title}: {message}");
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
        /// Convert BuiltInCategoryDto to Revit BuiltInCategory
        /// </summary>
        private BuiltInCategory ConvertToBuiltInCategory(BuiltInCategoryDto category)
        {
            return category switch
            {
                BuiltInCategoryDto.OST_Walls => BuiltInCategory.OST_Walls,
                BuiltInCategoryDto.OST_StructuralFraming => BuiltInCategory.OST_StructuralFraming,
                BuiltInCategoryDto.OST_StructuralColumns => BuiltInCategory.OST_StructuralColumns,
                BuiltInCategoryDto.OST_Floors => BuiltInCategory.OST_Floors,
                BuiltInCategoryDto.OST_Doors => BuiltInCategory.OST_Doors,
                BuiltInCategoryDto.OST_Windows => BuiltInCategory.OST_Windows,
                BuiltInCategoryDto.OST_GenericModel => BuiltInCategory.OST_GenericModel,
                _ => throw new ArgumentException($"Unsupported category: {category}")
            };
        }

        /// <summary>
        /// Convert Revit Parameter to ParameterDto
        /// </summary>
        private ParameterDto ConvertToParameterDto(Parameter param)
        {
            var value = new ParameterValueDto();
            
            switch (param.StorageType)
            {
                case StorageType.String:
                    value.StringValue = param.AsString();
                    break;
                case StorageType.Double:
                    value.DoubleValue = param.AsDouble();
                    break;
                case StorageType.Integer:
                    value.IntValue = param.AsInteger();
                    break;
                case StorageType.ElementId:
                    value.ElementIdValue = param.AsElementId()?.IntegerValue;
                    break;
            }
            
            return new ParameterDto
            {
                Name = param.Definition.Name,
                Value = value,
                StorageType = param.StorageType.ToString(),
                IsReadOnly = param.IsReadOnly
            };
        }

        /// <summary>
        /// Set parameter value from ParameterValueDto
        /// </summary>
        private void SetParameterValue(Parameter param, ParameterValueDto value)
        {
            switch (param.StorageType)
            {
                case StorageType.String:
                    if (value.StringValue != null)
                        param.Set(value.StringValue);
                    break;
                case StorageType.Double:
                    if (value.DoubleValue.HasValue)
                        param.Set(value.DoubleValue.Value);
                    break;
                case StorageType.Integer:
                    if (value.IntValue.HasValue)
                        param.Set(value.IntValue.Value);
                    break;
                case StorageType.ElementId:
                    if (value.ElementIdValue.HasValue)
                        param.Set(new ElementId(value.ElementIdValue.Value));
                    break;
                default:
                    throw new ArgumentException($"Unsupported parameter storage type: {param.StorageType}");
            }
        }

        #endregion

        /// <summary>
        /// Override to control object lifetime in AppDomain
        /// </summary>
        public override object InitializeLifetimeService()
        {
            // Return null to give the object infinite lifetime
            return null;
        }
    }
}
