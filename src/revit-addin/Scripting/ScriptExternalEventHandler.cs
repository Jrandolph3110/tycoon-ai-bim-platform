using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TycoonRevitAddin.Utils;
using Tycoon.Scripting.Contracts;

namespace TycoonRevitAddin.Scripting
{
    /// <summary>
    /// External Event Handler for executing scripts in proper Revit context
    /// This ensures scripts can modify the document and have full Revit API access
    /// </summary>
    public class ScriptExternalEventHandler : IExternalEventHandler
    {
        private readonly ScriptInfo _script;
        private readonly UIApplication _uiApplication;
        private readonly Document _document;
        private readonly Logger _logger;
        private readonly TaskCompletionSource<ScriptExecutionResult> _taskCompletionSource;

        public ScriptExternalEventHandler(
            ScriptInfo script, 
            UIApplication uiApp, 
            Document doc, 
            Logger logger, 
            TaskCompletionSource<ScriptExecutionResult> tcs)
        {
            _script = script ?? throw new ArgumentNullException(nameof(script));
            _uiApplication = uiApp ?? throw new ArgumentNullException(nameof(uiApp));
            _document = doc ?? throw new ArgumentNullException(nameof(doc));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _taskCompletionSource = tcs ?? throw new ArgumentNullException(nameof(tcs));
        }

        /// <summary>
        /// Execute the script in proper Revit context with transaction management
        /// </summary>
        public void Execute(UIApplication app)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.Log($"🔧 Loading script assembly: {_script.AssemblyPath}");
                
                // Load script assembly
                var assembly = Assembly.LoadFrom(_script.AssemblyPath);
                var scriptType = assembly.GetType(_script.Manifest.EntryType);
                
                if (scriptType == null)
                {
                    throw new InvalidOperationException($"Script type '{_script.Manifest.EntryType}' not found in assembly");
                }
                
                // Create script instance
                var scriptInstance = Activator.CreateInstance(scriptType) as IScript;
                if (scriptInstance == null)
                {
                    throw new InvalidOperationException($"Script type '{_script.Manifest.EntryType}' does not implement IScript");
                }
                
                // Create host with full Revit context
                var host = new DirectRevitHost(_uiApplication, _document, _logger);
                
                // Execute with transaction management in proper Revit context
                ExecuteScriptWithTransaction(scriptInstance, host, _script.Manifest.Name, _document);
                
                stopwatch.Stop();
                _logger.Log($"✅ Script '{_script.Manifest.Name}' executed successfully in {stopwatch.ElapsedMilliseconds}ms");
                
                // Signal successful completion
                _taskCompletionSource.SetResult(new ScriptExecutionResult
                {
                    Success = true,
                    ExecutionTime = stopwatch.Elapsed
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError($"❌ Script '{_script.Manifest.Name}' failed", ex);
                
                // Signal failure
                _taskCompletionSource.SetResult(new ScriptExecutionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ExecutionTime = stopwatch.Elapsed
                });
            }
        }

        /// <summary>
        /// Execute script with automatic transaction management in proper Revit context
        /// </summary>
        private void ExecuteScriptWithTransaction(IScript script, IRevitHost host, string scriptName, Document document)
        {
            // Check if we have Revit context
            if (document == null)
            {
                _logger.Log($"⚠️ No Revit document context - executing script without transaction management");
                script.Execute(host);
                return;
            }
            
            using (var transaction = new Transaction(document, $"Tycoon Script: {scriptName}"))
            {
                try
                {
                    transaction.Start();
                    _logger.Log($"🔄 Transaction started for script: {scriptName}");
                    
                    // Execute script - any exceptions will cause rollback
                    script.Execute(host);
                    
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
            }
        }

        /// <summary>
        /// Name of this external event handler
        /// </summary>
        public string GetName()
        {
            return $"Tycoon Script Executor: {_script.Manifest.Name}";
        }
    }
}
