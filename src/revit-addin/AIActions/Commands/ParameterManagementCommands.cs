using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TycoonRevitAddin.AIActions.Events;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.AIActions.Commands
{
    /// <summary>
    /// 🤖 AI Parameter Management Commands
    /// Handles intelligent parameter analysis and modification
    /// </summary>
    public class ParameterManagementCommands
    {
        private readonly ILogger _logger;
        private readonly EventStore _eventStore;
        private readonly FLCScriptBridge _flcBridge;

        public ParameterManagementCommands(ILogger logger, EventStore eventStore)
        {
            _logger = logger;
            _eventStore = eventStore;
            _flcBridge = new FLCScriptBridge(logger);
        }

        /// <summary>
        /// 🌉 FLC Hybrid Command - Phase 0 Implementation
        /// Calls existing FLC scripts OR generates new ones based on request
        /// Implements o3-pro's phased hybrid strategy
        /// </summary>
        public string ExecuteFLCOperation(Document doc, UIDocument uidoc, dynamic payload)
        {
            var operationStartTime = DateTime.UtcNow;

            try
            {
                _logger.Log("🌉 FLC Hybrid Operation starting...");

                // Parse payload
                string operation = payload?.operation?.ToString() ?? "ReNumberPanelElements";
                string direction = payload?.direction?.ToString() ?? "left_to_right";
                string namingConvention = payload?.namingConvention?.ToString() ?? "flc_standard";
                bool dryRun = payload?.dryRun ?? false;

                // Create FLC request
                var flcRequest = new FLCRequest
                {
                    ScriptName = operation,
                    OperationId = $"flc_{operationStartTime:yyyy-MM-ddTHH:mm:ss.fffZ}",
                    Args = new Dictionary<string, string>
                    {
                        ["direction"] = direction,
                        ["namingConvention"] = namingConvention,
                        ["dryRun"] = dryRun.ToString()
                    }
                };

                // Get current selection
                var selection = uidoc.Selection.GetElementIds();
                flcRequest.ElementIds = selection.Select(id => id.IntegerValue).ToList();

                // Execute via FLC bridge with document context (now synchronous)
                var response = _flcBridge.CallExistingScript(flcRequest);

                var operationEndTime = DateTime.UtcNow;
                var totalExecutionTime = (operationEndTime - operationStartTime).TotalMilliseconds;

                // Return rich response
                return JsonConvert.SerializeObject(new
                {
                    success = response.Success,
                    operationId = response.OperationId,
                    status = response.Success ? "Completed" : "Failed",
                    message = response.Message,
                    summary = new
                    {
                        operation = operation,
                        elementsProcessed = selection.Count,
                        executionTimeMs = Math.Round(totalExecutionTime, 2),
                        scriptType = response.ScriptType
                    },
                    flcBridge = new
                    {
                        bridgeVersion = "Phase_0",
                        strategy = "hybrid_ai_script",
                        generatedScript = response.GeneratedScript
                    },
                    performance = new
                    {
                        totalTimeMs = Math.Round(totalExecutionTime, 2),
                        bridgeTimeMs = response.ExecutionTimeMs
                    },
                    data = response.TransformationData
                });
            }
            catch (Exception ex)
            {
                var operationEndTime = DateTime.UtcNow;
                var totalExecutionTime = (operationEndTime - operationStartTime).TotalMilliseconds;

                _logger.LogError("Error in FLC Hybrid Operation", ex);
                return JsonConvert.SerializeObject(new
                {
                    success = false,
                    operationId = $"flc_{operationStartTime:yyyy-MM-ddTHH:mm:ss.fffZ}",
                    status = "Failed",
                    message = ex.Message,
                    summary = new
                    {
                        operation = "unknown",
                        elementsProcessed = 0,
                        executionTimeMs = Math.Round(totalExecutionTime, 2),
                        errorType = ex.GetType().Name
                    },
                    data = (object)null
                });
            }
        }

        /// <summary>
        /// 📊 FLC Script Graduation Analytics - Phase 1 Implementation
        /// Analyze script usage patterns and identify promotion candidates
        /// </summary>
        public string GetFLCScriptGraduationAnalytics(Document doc, UIDocument uidoc, dynamic payload)
        {
            var operationStartTime = DateTime.UtcNow;

            try
            {
                _logger.Log("📊 FLC Script Graduation Analytics starting...");

                // Parse payload
                int minExecutionCount = payload?.minExecutionCount ?? 5;
                bool includeMetrics = payload?.includeMetrics ?? true;
                bool cleanupTempFiles = payload?.cleanupTempFiles ?? false;

                // Get graduation candidates from FLC bridge
                var candidates = _flcBridge.GetScriptGraduationCandidates(minExecutionCount);

                // Cleanup temp files if requested
                if (cleanupTempFiles)
                {
                    _flcBridge.CleanupTempScripts(24); // 24 hours
                }

                var operationEndTime = DateTime.UtcNow;
                var totalExecutionTime = (operationEndTime - operationStartTime).TotalMilliseconds;

                // Return rich analytics response
                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    operationId = $"analytics_{operationStartTime:yyyy-MM-ddTHH:mm:ss.fffZ}",
                    status = "Completed",
                    message = $"Found {candidates.Count} script graduation candidates",
                    summary = new
                    {
                        candidatesFound = candidates.Count,
                        minExecutionThreshold = minExecutionCount,
                        executionTimeMs = Math.Round(totalExecutionTime, 2),
                        tempFilesCleanedUp = cleanupTempFiles
                    },
                    graduationCandidates = candidates.Select(c => new
                    {
                        scriptName = c.ScriptName,
                        executionCount = c.ExecutionCount,
                        averageExecutionTime = Math.Round(c.AverageExecutionTime, 2),
                        lastUsed = c.LastUsed.ToString("yyyy-MM-dd HH:mm:ss"),
                        graduationScore = Math.Round(c.GraduationScore, 3),
                        recommendation = c.GraduationScore > 0.7 ? "High Priority" :
                                       c.GraduationScore > 0.4 ? "Medium Priority" : "Low Priority"
                    }).OrderByDescending(c => c.graduationScore),
                    phase1Analytics = new
                    {
                        hotLoadingEnabled = true,
                        scriptCachingActive = true,
                        graduationThresholds = new
                        {
                            highPriority = 0.7,
                            mediumPriority = 0.4,
                            minExecutions = minExecutionCount
                        }
                    },
                    performance = new
                    {
                        totalTimeMs = Math.Round(totalExecutionTime, 2),
                        analyticsTimeMs = Math.Round(totalExecutionTime, 2)
                    }
                });
            }
            catch (Exception ex)
            {
                var operationEndTime = DateTime.UtcNow;
                var totalExecutionTime = (operationEndTime - operationStartTime).TotalMilliseconds;

                _logger.LogError("Error in FLC Script Graduation Analytics", ex);
                return JsonConvert.SerializeObject(new
                {
                    success = false,
                    operationId = $"analytics_{operationStartTime:yyyy-MM-ddTHH:mm:ss.fffZ}",
                    status = "Failed",
                    message = ex.Message,
                    summary = new
                    {
                        candidatesFound = 0,
                        executionTimeMs = Math.Round(totalExecutionTime, 2),
                        errorType = ex.GetType().Name
                    }
                });
            }
        }

        /// <summary>
        /// Get detailed parameter information for elements
        /// </summary>
        public string GetElementParameters(Document doc, UIDocument uidoc, dynamic payload)
        {
            try
            {
                _logger.Log("🔍 Getting element parameters...");

                var elementIds = payload?.elementIds as List<string>;
                var elements = new List<Element>();

                if (elementIds != null && elementIds.Count > 0)
                {
                    // Get specific elements by ID
                    foreach (var idStr in elementIds)
                    {
                        if (int.TryParse(idStr, out int id))
                        {
                            var element = doc.GetElement(new ElementId(id));
                            if (element != null)
                                elements.Add(element);
                        }
                    }
                }
                else
                {
                    // Get current selection
                    var selection = uidoc.Selection.GetElementIds();
                    elements = selection.Select(id => doc.GetElement(id)).Where(e => e != null).ToList();
                }

                if (elements.Count == 0)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        message = "No elements found or selected",
                        data = new { elements = new List<object>() }
                    });
                }

                var elementData = elements.Select(element => ExtractElementParameters(element)).ToList();

                // Log event
                _eventStore.AddEvent(new AIActionEvent
                {
                    ActionType = "GetElementParameters",
                    ElementIds = elements.Select(e => e.Id.IntegerValue).ToList(),
                    Status = "Success",
                    Details = $"Retrieved parameters for {elements.Count} elements"
                });

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    message = $"Retrieved parameters for {elements.Count} elements",
                    data = new { elements = elementData }
                });
            }
            catch (Exception ex)
            {
                _logger.Log($"❌ Error getting element parameters: {ex.Message}");
                return JsonConvert.SerializeObject(new
                {
                    success = false,
                    message = $"Error getting element parameters: {ex.Message}",
                    data = new { elements = new List<object>() }
                });
            }
        }

        /// <summary>
        /// Modify element parameters based on AI recommendations
        /// </summary>
        public string ModifyParameters(Document doc, UIDocument uidoc, dynamic payload)
        {
            try
            {
                _logger.Log("🔧 Modifying element parameters...");

                var modifications = payload?.modifications as List<dynamic>;
                var createTransaction = payload?.createTransaction ?? true;
                var transactionName = payload?.transactionName ?? "AI Parameter Modifications";

                if (modifications == null || modifications.Count == 0)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        message = "No modifications provided",
                        data = new { applied = 0 }
                    });
                }

                var results = new List<object>();
                var successCount = 0;

                using (var transaction = new Transaction(doc, transactionName))
                {
                    if (createTransaction)
                        transaction.Start();

                    foreach (var mod in modifications)
                    {
                        try
                        {
                            var elementIdStr = mod?.elementId?.ToString();
                            var parameterName = mod?.parameter?.ToString();
                            var newValue = mod?.newValue;

                            if (string.IsNullOrEmpty(elementIdStr) || string.IsNullOrEmpty(parameterName))
                            {
                                results.Add(new
                                {
                                    elementId = elementIdStr,
                                    parameter = parameterName,
                                    success = false,
                                    message = "Missing element ID or parameter name"
                                });
                                continue;
                            }

                            if (int.TryParse(elementIdStr, out int elementId))
                            {
                                var element = doc.GetElement(new ElementId(elementId));
                                if (element != null)
                                {
                                    var result = SetElementParameter(element, parameterName, newValue);
                                    results.Add(new
                                    {
                                        elementId = elementIdStr,
                                        parameter = parameterName,
                                        success = result.success,
                                        message = result.message
                                    });

                                    if (result.success)
                                        successCount++;
                                }
                                else
                                {
                                    results.Add(new
                                    {
                                        elementId = elementIdStr,
                                        parameter = parameterName,
                                        success = false,
                                        message = "Element not found"
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            results.Add(new
                            {
                                elementId = mod?.elementId?.ToString(),
                                parameter = mod?.parameter?.ToString(),
                                success = false,
                                message = ex.Message
                            });
                        }
                    }

                    if (createTransaction)
                        transaction.Commit();
                }

                // Log event
                _eventStore.AddEvent(new AIActionEvent
                {
                    ActionType = "ModifyParameters",
                    ElementIds = modifications.Select(m => 
                    {
                        if (int.TryParse(m?.elementId?.ToString(), out int id))
                            return id;
                        return 0;
                    }).Where(id => id != 0).ToList(),
                    Status = successCount > 0 ? "Success" : "Failed",
                    Details = $"Applied {successCount}/{modifications.Count} parameter modifications"
                });

                return JsonConvert.SerializeObject(new
                {
                    success = successCount > 0,
                    message = $"Applied {successCount}/{modifications.Count} parameter modifications",
                    data = new { applied = successCount, results = results }
                });
            }
            catch (Exception ex)
            {
                _logger.Log($"❌ Error modifying parameters: {ex.Message}");
                return JsonConvert.SerializeObject(new
                {
                    success = false,
                    message = $"Error modifying parameters: {ex.Message}",
                    data = new { applied = 0 }
                });
            }
        }

        /// <summary>
        /// Extract all parameters from an element
        /// </summary>
        private object ExtractElementParameters(Element element)
        {
            var parameters = new Dictionary<string, object>();

            // Get all parameters
            foreach (Parameter param in element.Parameters)
            {
                try
                {
                    var value = GetParameterValue(param);
                    parameters[param.Definition.Name] = value;
                }
                catch (Exception ex)
                {
                    parameters[param.Definition.Name] = $"Error: {ex.Message}";
                }
            }

            return new
            {
                id = element.Id.IntegerValue,
                category = element.Category?.Name ?? "Unknown",
                name = element.Name ?? "Unnamed",
                parameters = parameters
            };
        }

        /// <summary>
        /// Get parameter value as appropriate type
        /// </summary>
        private object GetParameterValue(Parameter param)
        {
            if (param == null || !param.HasValue)
                return null;

            switch (param.StorageType)
            {
                case StorageType.String:
                    return param.AsString();
                case StorageType.Integer:
                    return param.AsInteger();
                case StorageType.Double:
                    return param.AsDouble();
                case StorageType.ElementId:
                    return param.AsElementId().IntegerValue;
                default:
                    return param.AsValueString();
            }
        }

        /// <summary>
        /// Set parameter value on element
        /// </summary>
        private (bool success, string message) SetElementParameter(Element element, string parameterName, object newValue)
        {
            try
            {
                var param = element.LookupParameter(parameterName);
                if (param == null)
                {
                    return (false, $"Parameter '{parameterName}' not found");
                }

                if (param.IsReadOnly)
                {
                    return (false, $"Parameter '{parameterName}' is read-only");
                }

                switch (param.StorageType)
                {
                    case StorageType.String:
                        param.Set(newValue?.ToString() ?? "");
                        break;
                    case StorageType.Integer:
                        if (int.TryParse(newValue?.ToString(), out int intValue))
                            param.Set(intValue);
                        else
                            return (false, $"Cannot convert '{newValue}' to integer");
                        break;
                    case StorageType.Double:
                        if (double.TryParse(newValue?.ToString(), out double doubleValue))
                            param.Set(doubleValue);
                        else
                            return (false, $"Cannot convert '{newValue}' to double");
                        break;
                    case StorageType.ElementId:
                        if (int.TryParse(newValue?.ToString(), out int elementIdValue))
                            param.Set(new ElementId(elementIdValue));
                        else
                            return (false, $"Cannot convert '{newValue}' to ElementId");
                        break;
                    default:
                        return (false, $"Unsupported parameter storage type: {param.StorageType}");
                }

                return (true, "Parameter updated successfully");
            }
            catch (Exception ex)
            {
                return (false, $"Error setting parameter: {ex.Message}");
            }
        }

        /// <summary>
        /// 🎯 AI-powered panel element renaming with FLC standards
        /// </summary>
        public string RenamePanelElements(Document doc, UIDocument uidoc, dynamic payload)
        {
            var operationStartTime = DateTime.UtcNow;
            var transformations = new List<object>();
            var validationResults = new List<object>();

            try
            {
                _logger.Log("🎯 AI renaming panel elements...");

                // Parse payload
                var elementIds = payload?.elementIds as JArray;
                string namingConvention = payload?.namingConvention?.ToString() ?? "flc_standard";
                string direction = payload?.direction?.ToString() ?? "left_to_right";
                bool dryRun = payload?.dryRun ?? true;

                // Get elements to rename
                var elements = new List<Element>();
                if (elementIds != null && elementIds.Count > 0)
                {
                    foreach (var idToken in elementIds)
                    {
                        if (int.TryParse(idToken.ToString(), out int elementId))
                        {
                            var element = doc.GetElement(new ElementId(elementId));
                            if (element != null) elements.Add(element);
                        }
                    }
                }
                else
                {
                    // Use current selection
                    var selection = uidoc.Selection.GetElementIds();
                    foreach (var id in selection)
                    {
                        var element = doc.GetElement(id);
                        if (element != null) elements.Add(element);
                    }
                }

                if (elements.Count == 0)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        message = "No elements found to rename",
                        data = (object)null
                    });
                }

                // Filter structural framing elements
                var framingElements = elements
                    .Where(e => e.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralFraming)
                    .ToList();

                if (framingElements.Count == 0)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        message = "No structural framing elements found",
                        data = (object)null
                    });
                }

                // Sort elements by position (left to right)
                _logger.Log($"🔄 Sorting {framingElements.Count} elements by position ({direction})...");
                var sortedElements = framingElements
                    .OrderBy(e => GetElementCenterX(e))
                    .ToList();

                _logger.Log($"✅ Element sorting complete. Processing {sortedElements.Count} elements...");
                var results = new List<object>();
                int studNumber = 1;
                int processedCount = 0;

                if (!dryRun)
                {
                    // 🚀 Performance optimization: Batch parameter modifications
                    using (Transaction trans = new Transaction(doc, "AI Rename Panel Elements"))
                    {
                        trans.Start();

                        // Pre-cache parameter lookups for better performance
                        var parameterCache = new Dictionary<ElementId, (Parameter label, Parameter bimsf)>();
                        foreach (var element in sortedElements)
                        {
                            parameterCache[element.Id] = (
                                element.LookupParameter("Label"),
                                element.LookupParameter("BIMSF_Label")
                            );
                        }

                        foreach (var element in sortedElements)
                        {
                            try
                            {
                                string newName = GenerateElementName(element, studNumber, namingConvention);
                                var (labelParam, bimsf_labelParam) = parameterCache[element.Id];

                                // 🚀 Optimized parameter setting with validation
                                if (labelParam != null && !labelParam.IsReadOnly)
                                {
                                    labelParam.Set(newName);
                                }
                                if (bimsf_labelParam != null && !bimsf_labelParam.IsReadOnly)
                                {
                                    bimsf_labelParam.Set(newName);
                                }

                                var oldLabel = element.LookupParameter("Label")?.AsString() ?? "Unknown";
                                var oldBIMSFLabel = element.LookupParameter("BIMSF_Label")?.AsString() ?? "Unknown";

                                // Track transformation for rich response
                                transformations.Add(new
                                {
                                    elementId = element.Id.IntegerValue.ToString(),
                                    category = element.Category?.Name ?? "Unknown",
                                    changes = new
                                    {
                                        Label = new { from = oldLabel, to = newName },
                                        BIMSF_Label = new { from = oldBIMSFLabel, to = newName }
                                    },
                                    position = new
                                    {
                                        x = Math.Round(GetElementCenterX(element), 2),
                                        sequence = IsStud(element) ? studNumber + 1 : 0
                                    }
                                });

                                results.Add(new
                                {
                                    elementId = element.Id.IntegerValue,
                                    oldName = oldLabel,
                                    newName = newName,
                                    success = true
                                });

                                if (IsStud(element)) studNumber++;

                                // 📊 Progress tracking for real-time feedback
                                processedCount++;
                                if (processedCount % 2 == 0 || processedCount == sortedElements.Count)
                                {
                                    _logger.Log($"📊 Progress: {processedCount}/{sortedElements.Count} elements processed ({Math.Round((double)processedCount / sortedElements.Count * 100, 1)}%)");
                                }
                            }
                            catch (Exception ex)
                            {
                                processedCount++;
                                results.Add(new
                                {
                                    elementId = element.Id.IntegerValue,
                                    oldName = element.LookupParameter("Label")?.AsString() ?? "Unknown",
                                    newName = "Failed",
                                    success = false,
                                    error = ex.Message
                                });
                                _logger.Log($"❌ Error processing element {element.Id.IntegerValue}: {ex.Message}");
                            }
                        }

                        trans.Commit();
                    }
                }
                else
                {
                    // Dry run - preview only
                    foreach (var element in sortedElements)
                    {
                        string newName = GenerateElementName(element, studNumber, namingConvention);
                        results.Add(new
                        {
                            elementId = element.Id.IntegerValue,
                            oldName = element.LookupParameter("Label")?.AsString() ?? "Unknown",
                            newName = newName,
                            preview = true
                        });

                        if (IsStud(element)) studNumber++;
                    }
                }

                var operationEndTime = DateTime.UtcNow;
                var totalExecutionTime = (operationEndTime - operationStartTime).TotalMilliseconds;

                // Rich response contract with comprehensive data
                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    operationId = $"rename_{operationStartTime:yyyy-MM-ddTHH:mm:ss.fffZ}",
                    status = "Completed",
                    message = $"Processed {results.Count} elements ({(dryRun ? "preview" : "applied")})",
                    summary = new
                    {
                        elementsProcessed = results.Count,
                        elementsModified = transformations.Count,
                        executionTimeMs = Math.Round(totalExecutionTime, 2),
                        operationType = dryRun ? "Preview" : "Applied",
                        namingConvention = namingConvention,
                        direction = direction
                    },
                    transformations = transformations,
                    validation = new
                    {
                        namingConflicts = new object[0], // TODO: Implement conflict detection
                        missingParameters = new object[0], // TODO: Implement parameter validation
                        warnings = validationResults
                    },
                    performance = new
                    {
                        totalTimeMs = Math.Round(totalExecutionTime, 2),
                        averageTimePerElement = transformations.Count > 0 ? Math.Round(totalExecutionTime / transformations.Count, 2) : 0
                    },
                    data = new { elements = results, dryRun = dryRun } // Legacy compatibility
                });
            }
            catch (Exception ex)
            {
                var operationEndTime = DateTime.UtcNow;
                var totalExecutionTime = (operationEndTime - operationStartTime).TotalMilliseconds;

                _logger.LogError("Error in RenamePanelElements", ex);
                return JsonConvert.SerializeObject(new
                {
                    success = false,
                    operationId = $"rename_{operationStartTime:yyyy-MM-ddTHH:mm:ss.fffZ}",
                    status = "Failed",
                    message = ex.Message,
                    summary = new
                    {
                        elementsProcessed = 0,
                        elementsModified = 0,
                        executionTimeMs = Math.Round(totalExecutionTime, 2),
                        errorType = ex.GetType().Name
                    },
                    transformations = new object[0],
                    validation = new
                    {
                        namingConflicts = new object[0],
                        missingParameters = new object[0],
                        warnings = new object[0]
                    },
                    performance = new
                    {
                        totalTimeMs = Math.Round(totalExecutionTime, 2),
                        failurePoint = "Parameter modification"
                    },
                    data = (object)null
                });
            }
        }

        /// <summary>
        /// 🧠 AI-powered panel structure analysis
        /// </summary>
        public string AnalyzePanelStructure(Document doc, UIDocument uidoc, dynamic payload)
        {
            try
            {
                _logger.Log("🧠 AI analyzing panel structure...");

                // Parse payload
                var elementIds = payload?.elementIds as JArray;
                string analysisDepth = payload?.analysisDepth?.ToString() ?? "detailed";
                bool includeRecommendations = payload?.includeRecommendations ?? true;

                // Get elements to analyze
                var elements = new List<Element>();
                if (elementIds != null && elementIds.Count > 0)
                {
                    foreach (var idToken in elementIds)
                    {
                        if (int.TryParse(idToken.ToString(), out int elementId))
                        {
                            var element = doc.GetElement(new ElementId(elementId));
                            if (element != null) elements.Add(element);
                        }
                    }
                }
                else
                {
                    // Use current selection
                    var selection = uidoc.Selection.GetElementIds();
                    foreach (var id in selection)
                    {
                        var element = doc.GetElement(id);
                        if (element != null) elements.Add(element);
                    }
                }

                if (elements.Count == 0)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        message = "No elements found to analyze",
                        data = (object)null
                    });
                }

                // Analyze panel structure
                var analysis = AnalyzePanelComponents(elements);
                var recommendations = includeRecommendations ? GenerateRecommendations(analysis) : new List<object>();

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    message = $"Analyzed {elements.Count} elements",
                    data = new
                    {
                        analysis = analysis,
                        recommendations = recommendations,
                        analysisDepth = analysisDepth
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in AnalyzePanelStructure", ex);
                return JsonConvert.SerializeObject(new
                {
                    success = false,
                    message = ex.Message,
                    data = (object)null
                });
            }
        }

        /// <summary>
        /// Helper method to get element center X coordinate
        /// </summary>
        private double GetElementCenterX(Element element)
        {
            try
            {
                var bbox = element.get_BoundingBox(null);
                if (bbox != null)
                {
                    return (bbox.Min.X + bbox.Max.X) / 2.0;
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Helper method to check if element is a stud
        /// </summary>
        private bool IsStud(Element element)
        {
            var tag = element.LookupParameter("Tag")?.AsString() ?? "";
            var description = element.LookupParameter("BIMSF_Description")?.AsString() ?? "";
            return tag.Contains("EV") || description.Contains("EV") || tag.Contains("E");
        }

        /// <summary>
        /// Generate element name based on FLC standards
        /// </summary>
        private string GenerateElementName(Element element, int studNumber, string namingConvention)
        {
            var tag = element.LookupParameter("Tag")?.AsString() ?? "";
            var description = element.LookupParameter("BIMSF_Description")?.AsString() ?? "";

            if (namingConvention == "flc_standard")
            {
                if (IsStud(element))
                {
                    return $"Stud {studNumber}";
                }
                else if (tag.Contains("TTOP") || description.Contains("TTOP"))
                {
                    return "Top Track";
                }
                else if (tag.Contains("TBOT") || description.Contains("TBOT"))
                {
                    return "Bottom Track";
                }
            }

            return element.LookupParameter("Label")?.AsString() ?? $"Element {studNumber}";
        }

        /// <summary>
        /// Analyze panel components
        /// </summary>
        private object AnalyzePanelComponents(List<Element> elements)
        {
            var studs = elements.Where(IsStud).ToList();
            var tracks = elements.Where(e => !IsStud(e) && e.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralFraming).ToList();

            return new
            {
                totalElements = elements.Count,
                studs = studs.Count,
                tracks = tracks.Count,
                studSpacing = CalculateStudSpacing(studs),
                panelWidth = CalculatePanelWidth(elements),
                missingParameters = FindMissingParameters(elements)
            };
        }

        /// <summary>
        /// Generate AI recommendations
        /// </summary>
        private List<object> GenerateRecommendations(dynamic analysis)
        {
            var recommendations = new List<object>();

            if (analysis.studs < 2)
            {
                recommendations.Add(new { type = "warning", message = "Panel has fewer than 2 studs" });
            }

            if (analysis.tracks < 2)
            {
                recommendations.Add(new { type = "warning", message = "Panel missing top or bottom track" });
            }

            if (analysis.missingParameters.Count > 0)
            {
                recommendations.Add(new { type = "error", message = "Missing BIMSF parameters detected" });
            }

            return recommendations;
        }

        /// <summary>
        /// Calculate stud spacing
        /// </summary>
        private double CalculateStudSpacing(List<Element> studs)
        {
            if (studs.Count < 2) return 0;

            var positions = studs.Select(GetElementCenterX).OrderBy(x => x).ToList();
            var spacings = new List<double>();

            for (int i = 1; i < positions.Count; i++)
            {
                spacings.Add(positions[i] - positions[i - 1]);
            }

            return spacings.Count > 0 ? spacings.Average() : 0;
        }

        /// <summary>
        /// Calculate panel width
        /// </summary>
        private double CalculatePanelWidth(List<Element> elements)
        {
            if (elements.Count == 0) return 0;

            var minX = elements.Min(GetElementCenterX);
            var maxX = elements.Max(GetElementCenterX);
            return maxX - minX;
        }

        /// <summary>
        /// Find missing parameters
        /// </summary>
        private List<string> FindMissingParameters(List<Element> elements)
        {
            var missingParams = new List<string>();
            var requiredParams = new[] { "BIMSF_Container", "BIMSF_Label", "BIMSF_Id" };

            foreach (var element in elements)
            {
                foreach (var paramName in requiredParams)
                {
                    var param = element.LookupParameter(paramName);
                    if (param == null || string.IsNullOrEmpty(param.AsString()))
                    {
                        missingParams.Add($"{element.Id.IntegerValue}: {paramName}");
                    }
                }
            }

            return missingParams.Distinct().ToList();
        }
    }
}
