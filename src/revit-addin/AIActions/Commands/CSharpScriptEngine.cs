using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.CSharp;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.AIActions.Commands
{
    /// <summary>
    /// C# Script Compilation and Execution Engine
    /// Provides in-memory compilation and execution of C# scripts with Revit API support
    /// Supports F.L. Crane & Sons workflow integration with ExtensibleStorage and Tycoon services
    /// </summary>
    public class CSharpScriptEngine
    {
        private readonly ILogger _logger;
        private readonly CSharpCodeProvider _compiler;

        public CSharpScriptEngine(ILogger logger)
        {
            _logger = logger;
            _compiler = new CSharpCodeProvider();
        }

        /// <summary>
        /// Compile and execute C# script with transaction safety
        /// </summary>
        public async Task<HotLoadResult> CompileAndExecuteAsync(
            string scriptContent,
            string scriptName,
            Document doc,
            UIDocument uidoc,
            List<int> elementIds,
            string operationId)
        {
            var startTime = DateTime.UtcNow;
            _logger.Log($"🔧 [OP:{operationId}] Compiling C# script: {scriptName}");

            try
            {
                // Compile the script
                var assembly = CompileScript(scriptContent, scriptName);
                if (assembly == null)
                {
                    return new HotLoadResult
                    {
                        Success = false,
                        OperationId = operationId,
                        Message = "Failed to compile C# script",
                        ScriptName = scriptName
                    };
                }

                // Execute with transaction safety
                return await ExecuteCompiledScript(assembly, scriptName, doc, uidoc, elementIds, operationId, startTime);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error compiling/executing C# script '{scriptName}': {ex.Message}", ex);
                return new HotLoadResult
                {
                    Success = false,
                    OperationId = operationId,
                    Message = ex.Message,
                    ScriptName = scriptName
                };
            }
        }

        /// <summary>
        /// Compile C# script to assembly
        /// </summary>
        private Assembly CompileScript(string scriptContent, string scriptName)
        {
            try
            {
                // Prepare compilation parameters
                var parameters = new CompilerParameters
                {
                    GenerateInMemory = true,
                    GenerateExecutable = false,
                    IncludeDebugInformation = false,
                    TreatWarningsAsErrors = false
                };

                // Add required references
                AddRequiredReferences(parameters);

                // Wrap script content in a class if needed
                var wrappedScript = WrapScriptContent(scriptContent, scriptName);

                _logger.Log($"🔧 Compiling script with {parameters.ReferencedAssemblies.Count} references");

                // Compile the script
                var results = _compiler.CompileAssemblyFromSource(parameters, wrappedScript);

                // Check for compilation errors
                if (results.Errors.HasErrors)
                {
                    var errors = string.Join("\n", results.Errors.Cast<CompilerError>()
                        .Select(e => $"Line {e.Line}: {e.ErrorText}"));
                    _logger.LogError($"Compilation errors in script '{scriptName}':\n{errors}");
                    return null;
                }

                _logger.Log($"🔧 Successfully compiled script: {scriptName}");
                return results.CompiledAssembly;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error compiling script '{scriptName}': {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Add required assembly references for Revit API and Tycoon platform
        /// </summary>
        private void AddRequiredReferences(CompilerParameters parameters)
        {
            // Core .NET assemblies
            parameters.ReferencedAssemblies.Add("System.dll");
            parameters.ReferencedAssemblies.Add("System.Core.dll");
            parameters.ReferencedAssemblies.Add("System.Data.dll");
            parameters.ReferencedAssemblies.Add("System.Xml.dll");
            parameters.ReferencedAssemblies.Add("System.Windows.Forms.dll");

            // Revit API assemblies
            var revitPath = @"C:\Program Files\Autodesk\Revit 2024";
            parameters.ReferencedAssemblies.Add(Path.Combine(revitPath, "RevitAPI.dll"));
            parameters.ReferencedAssemblies.Add(Path.Combine(revitPath, "RevitAPIUI.dll"));

            // Current assembly (for Tycoon platform integration)
            parameters.ReferencedAssemblies.Add(Assembly.GetExecutingAssembly().Location);
        }

        /// <summary>
        /// Wrap script content in proper class structure if needed
        /// </summary>
        private string WrapScriptContent(string scriptContent, string scriptName)
        {
            // Check if script already has a class structure
            if (scriptContent.Contains("public class") || scriptContent.Contains("class "))
            {
                // Script already has class structure, just add using statements if missing
                return EnsureUsingStatements(scriptContent);
            }

            // Wrap in a class structure for F.L. Crane scripts
            var className = SanitizeClassName(scriptName);
            var wrappedScript = $@"
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.HotLoadedScripts
{{
    public class {className}
    {{
        public static void Execute(Document doc, UIDocument uidoc, List<int> elementIds)
        {{
{IndentCode(scriptContent, 12)}
        }}
    }}
}}";

            return wrappedScript;
        }

        /// <summary>
        /// Ensure required using statements are present
        /// </summary>
        private string EnsureUsingStatements(string scriptContent)
        {
            var requiredUsings = new[]
            {
                "using System;",
                "using System.Collections.Generic;",
                "using System.Linq;",
                "using Autodesk.Revit.DB;",
                "using Autodesk.Revit.UI;"
            };

            var lines = scriptContent.Split('\n').ToList();
            var usingLines = lines.Where(l => l.Trim().StartsWith("using ")).ToList();
            
            foreach (var requiredUsing in requiredUsings)
            {
                if (!usingLines.Any(u => u.Trim().StartsWith(requiredUsing.Substring(0, requiredUsing.Length - 1))))
                {
                    lines.Insert(0, requiredUsing);
                }
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Sanitize script name for use as class name
        /// </summary>
        private string SanitizeClassName(string scriptName)
        {
            var className = scriptName.Replace(" ", "").Replace("-", "_").Replace(".", "_");
            if (char.IsDigit(className[0]))
                className = "Script_" + className;
            return className;
        }

        /// <summary>
        /// Indent code for proper class wrapping
        /// </summary>
        private string IndentCode(string code, int spaces)
        {
            var indent = new string(' ', spaces);
            return string.Join("\n", code.Split('\n').Select(line => indent + line));
        }

        /// <summary>
        /// Execute compiled script with transaction safety
        /// </summary>
        private async Task<HotLoadResult> ExecuteCompiledScript(
            Assembly assembly,
            string scriptName,
            Document doc,
            UIDocument uidoc,
            List<int> elementIds,
            string operationId,
            DateTime startTime)
        {
            try
            {
                _logger.Log($"🔧 [OP:{operationId}] Executing compiled script with transaction safety");

                using (var transactionGroup = new TransactionGroup(doc, $"Hot-loaded C# Script: {scriptName}"))
                {
                    transactionGroup.Start();

                    try
                    {
                        // Find and invoke the Execute method
                        var success = await InvokeScriptMethod(assembly, doc, uidoc, elementIds);

                        var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                        if (success)
                        {
                            transactionGroup.Assimilate(); // Commit changes
                            _logger.Log($"🔧 [OP:{operationId}] C# script executed successfully in {executionTime}ms");

                            return new HotLoadResult
                            {
                                Success = true,
                                OperationId = operationId,
                                Message = $"C# script '{scriptName}' executed successfully",
                                ScriptName = scriptName,
                                ExecutionTimeMs = executionTime,
                                ElementsProcessed = elementIds.Count,
                                ScriptPath = $"compiled_{operationId}.cs"
                            };
                        }
                        else
                        {
                            transactionGroup.RollBack(); // Rollback on failure
                            return new HotLoadResult
                            {
                                Success = false,
                                OperationId = operationId,
                                Message = $"C# script '{scriptName}' execution failed",
                                ScriptName = scriptName,
                                ExecutionTimeMs = executionTime
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        transactionGroup.RollBack(); // Rollback on exception
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error executing compiled script '{scriptName}': {ex.Message}", ex);
                return new HotLoadResult
                {
                    Success = false,
                    OperationId = operationId,
                    Message = ex.Message,
                    ScriptName = scriptName
                };
            }
        }

        /// <summary>
        /// Invoke the script method from compiled assembly
        /// </summary>
        private async Task<bool> InvokeScriptMethod(Assembly assembly, Document doc, UIDocument uidoc, List<int> elementIds)
        {
            try
            {
                // Find the script class and Execute method
                var scriptType = assembly.GetTypes().FirstOrDefault(t => t.Name.Contains("Script") || t.GetMethods().Any(m => m.Name == "Execute"));
                if (scriptType == null)
                {
                    _logger.LogError("No script class found in compiled assembly");
                    return false;
                }

                var executeMethod = scriptType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static);
                if (executeMethod == null)
                {
                    _logger.LogError("No Execute method found in script class");
                    return false;
                }

                // Invoke the Execute method
                executeMethod.Invoke(null, new object[] { doc, uidoc, elementIds });
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error invoking script method: {ex.Message}", ex);
                return false;
            }
        }
    }
}
