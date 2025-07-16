// ============================================================================
// TYCOON AI-BIM PLATFORM - COMPREHENSIVE SCRIPT TEMPLATE
// ============================================================================
// This C# template demonstrates proper script architecture, coding patterns,
// and integration with the Tycoon AI-BIM Platform. Use this as a reference
// guide for creating new scripts with consistent structure and best practices.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace TycoonScripts
{
    /// <summary>
    /// Comprehensive script template demonstrating all Tycoon AI-BIM Platform features
    /// and integration patterns. This serves as a reference guide for script development.
    /// 
    /// ARCHITECTURE OVERVIEW:
    /// - Inherits from base script interface for platform integration
    /// - Implements standard execution patterns
    /// - Demonstrates error handling and logging
    /// - Shows integration with platform services
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ScriptTemplate
    {
        // ====================================================================
        // SCRIPT METADATA AND CONFIGURATION
        // ====================================================================
        
        /// <summary>
        /// Script name for identification and logging
        /// </summary>
        public static readonly string ScriptName = "Script Template";
        
        /// <summary>
        /// Script version for tracking and compatibility
        /// </summary>
        public static readonly string ScriptVersion = "1.0.0";

        // ====================================================================
        // PLATFORM INTEGRATION PROPERTIES
        // ====================================================================
        
        /// <summary>
        /// Reference to active Revit application
        /// </summary>
        private Application _app;
        
        /// <summary>
        /// Reference to active UI application
        /// </summary>
        private UIApplication _uiApp;
        
        /// <summary>
        /// Reference to active document
        /// </summary>
        private Document _doc;
        
        /// <summary>
        /// Reference to active UI document
        /// </summary>
        private UIDocument _uidoc;

        // ====================================================================
        // MAIN EXECUTION METHOD
        // ====================================================================
        
        /// <summary>
        /// Main script execution method called by UnifiedScriptCommand
        /// 
        /// EXECUTION FLOW:
        /// 1. Initialize platform integration
        /// 2. Validate prerequisites
        /// 3. Execute main script logic
        /// 4. Handle results and cleanup
        /// 
        /// INTEGRATION NOTES:
        /// - Called by TycoonRevitAddin.Commands.UnifiedScriptCommand
        /// - Script name passed via button.ToolTip property
        /// - Platform services available through static references
        /// </summary>
        /// <param name="commandData">Revit command data</param>
        /// <param name="message">Error message output</param>
        /// <param name="elements">Element set for selection</param>
        /// <returns>Result indicating success/failure</returns>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // ============================================================
                // STEP 1: INITIALIZE PLATFORM INTEGRATION
                // ============================================================
                
                if (!InitializePlatformIntegration(commandData))
                {
                    message = "Failed to initialize platform integration";
                    return Result.Failed;
                }
                
                LogInfo("Script Template execution started");
                
                // ============================================================
                // STEP 2: VALIDATE PREREQUISITES
                // ============================================================
                
                if (!ValidatePrerequisites(ref message))
                {
                    return Result.Failed;
                }
                
                // ============================================================
                // STEP 3: EXECUTE MAIN SCRIPT LOGIC
                // ============================================================
                
                var result = ExecuteMainLogic();
                
                // ============================================================
                // STEP 4: HANDLE RESULTS AND CLEANUP
                // ============================================================
                
                if (result.Success)
                {
                    LogInfo($"Script completed successfully: {result.Message}");
                    ShowSuccessMessage(result.Message);
                    return Result.Succeeded;
                }
                else
                {
                    LogError($"Script failed: {result.Message}");
                    message = result.Message;
                    return Result.Failed;
                }
            }
            catch (Exception ex)
            {
                // ============================================================
                // COMPREHENSIVE ERROR HANDLING
                // ============================================================
                
                var errorMessage = $"Unexpected error in {ScriptName}: {ex.Message}";
                LogError(errorMessage, ex);
                message = errorMessage;
                return Result.Failed;
            }
        }

        // ====================================================================
        // PLATFORM INTEGRATION METHODS
        // ====================================================================
        
        /// <summary>
        /// Initialize integration with Tycoon AI-BIM Platform services
        /// </summary>
        private bool InitializePlatformIntegration(ExternalCommandData commandData)
        {
            try
            {
                // Initialize Revit API references
                _uiApp = commandData.Application;
                _app = _uiApp.Application;
                _uidoc = _uiApp.ActiveUIDocument;
                _doc = _uidoc?.Document;
                
                // Validate required references
                if (_app == null || _uiApp == null)
                {
                    LogError("Failed to initialize Revit application references");
                    return false;
                }
                
                LogInfo("Platform integration initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                LogError("Failed to initialize platform integration", ex);
                return false;
            }
        }
        
        /// <summary>
        /// Validate script prerequisites before execution
        /// </summary>
        private bool ValidatePrerequisites(ref string message)
        {
            try
            {
                // Check for active document (if required)
                if (_doc == null)
                {
                    message = "No active document found. Please open a Revit project.";
                    LogWarning(message);
                    return false;
                }
                
                // Check document state
                if (_doc.IsReadOnly)
                {
                    message = "Document is read-only. Cannot execute script.";
                    LogWarning(message);
                    return false;
                }
                
                // Additional validation can be added here
                LogInfo("Prerequisites validation passed");
                return true;
            }
            catch (Exception ex)
            {
                message = $"Prerequisites validation failed: {ex.Message}";
                LogError(message, ex);
                return false;
            }
        }

        // ====================================================================
        // MAIN SCRIPT LOGIC
        // ====================================================================
        
        /// <summary>
        /// Execute the main script logic
        /// This is where you implement your specific script functionality
        /// </summary>
        private ScriptResult ExecuteMainLogic()
        {
            try
            {
                LogInfo("Executing main script logic");
                
                // ============================================================
                // EXAMPLE: ELEMENT SELECTION AND PROCESSING
                // ============================================================
                
                var selectedElements = GetSelectedElements();
                LogInfo($"Processing {selectedElements.Count} selected elements");
                
                // ============================================================
                // EXAMPLE: TRANSACTION MANAGEMENT
                // ============================================================
                
                using (var transaction = new Transaction(_doc, "Script Template Operation"))
                {
                    transaction.Start();
                    
                    try
                    {
                        // Perform operations that modify the model
                        var processedCount = ProcessElements(selectedElements);
                        
                        transaction.Commit();
                        
                        return new ScriptResult
                        {
                            Success = true,
                            Message = $"Successfully processed {processedCount} elements"
                        };
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        throw new Exception($"Transaction failed: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                return new ScriptResult
                {
                    Success = false,
                    Message = $"Main logic execution failed: {ex.Message}"
                };
            }
        }

        // ====================================================================
        // UTILITY METHODS
        // ====================================================================
        
        /// <summary>
        /// Get currently selected elements or prompt for selection
        /// </summary>
        private List<Element> GetSelectedElements()
        {
            try
            {
                var selection = _uidoc.Selection;
                var selectedIds = selection.GetElementIds();
                
                if (selectedIds.Count == 0)
                {
                    LogInfo("No elements selected, prompting user for selection");
                    // Implement selection prompt if needed
                    return new List<Element>();
                }
                
                var elements = selectedIds
                    .Select(id => _doc.GetElement(id))
                    .Where(elem => elem != null)
                    .ToList();
                
                LogInfo($"Retrieved {elements.Count} selected elements");
                return elements;
            }
            catch (Exception ex)
            {
                LogError("Failed to get selected elements", ex);
                return new List<Element>();
            }
        }
        
        /// <summary>
        /// Process elements with your specific logic
        /// </summary>
        private int ProcessElements(List<Element> elements)
        {
            var processedCount = 0;
            
            foreach (var element in elements)
            {
                try
                {
                    // Implement your element processing logic here
                    LogInfo($"Processing element: {element.Name} (ID: {element.Id})");
                    
                    // Example: Modify element parameters
                    // var param = element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                    // if (param != null && !param.IsReadOnly)
                    // {
                    //     param.Set("Processed by Script Template");
                    // }
                    
                    processedCount++;
                }
                catch (Exception ex)
                {
                    LogError($"Failed to process element {element.Id}", ex);
                }
            }
            
            return processedCount;
        }

        // ====================================================================
        // LOGGING AND USER FEEDBACK
        // ====================================================================
        
        /// <summary>
        /// Log informational message
        /// </summary>
        private void LogInfo(string message)
        {
            // Integrate with Tycoon platform logging
            Console.WriteLine($"[INFO] {ScriptName}: {message}");
        }
        
        /// <summary>
        /// Log warning message
        /// </summary>
        private void LogWarning(string message)
        {
            Console.WriteLine($"[WARNING] {ScriptName}: {message}");
        }
        
        /// <summary>
        /// Log error message
        /// </summary>
        private void LogError(string message, Exception ex = null)
        {
            var fullMessage = ex != null ? $"{message}\nException: {ex}" : message;
            Console.WriteLine($"[ERROR] {ScriptName}: {fullMessage}");
        }
        
        /// <summary>
        /// Show success message to user
        /// </summary>
        private void ShowSuccessMessage(string message)
        {
            TaskDialog.Show(ScriptName, message, TaskDialogCommonButtons.Ok);
        }

        // ====================================================================
        // RESULT CLASSES
        // ====================================================================
        
        /// <summary>
        /// Script execution result
        /// </summary>
        private class ScriptResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
        }
    }
}
