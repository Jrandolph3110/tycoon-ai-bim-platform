using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.Utils
{
    /// <summary>
    /// Deferred Transaction Manager - Expert Optimization
    /// Batch parameter writes with TransactionGroup.Assimilate() for better performance
    /// Based on o3 pro expert recommendations for BIM automation
    /// </summary>
    public class DeferredTransactionManager : IDisposable
    {
        private readonly Document _document;
        private readonly ILogger _logger;
        private TransactionGroup _currentGroup;
        private readonly List<Transaction> _pendingTransactions;
        private readonly object _lockObject = new object();
        private bool _disposed = false;
        private int _transactionCount = 0;

        public DeferredTransactionManager(Document document, ILogger logger)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pendingTransactions = new List<Transaction>();
        }

        /// <summary>
        /// Start a new deferred transaction group
        /// </summary>
        public void StartGroup(string groupName)
        {
            lock (_lockObject)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(DeferredTransactionManager));

                if (_currentGroup != null)
                {
                    _logger.Log("⚠️ Starting new group while previous group is active - committing previous group");
                    CommitGroup();
                }

                _currentGroup = new TransactionGroup(_document, groupName);
                _currentGroup.Start();
                _transactionCount = 0;
                
                _logger.Log($"🚀 Started deferred transaction group: {groupName}");
            }
        }

        /// <summary>
        /// Execute an action within a deferred transaction
        /// </summary>
        public void ExecuteInTransaction(string transactionName, Action action)
        {
            lock (_lockObject)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(DeferredTransactionManager));

                if (_currentGroup == null)
                {
                    _logger.Log("⚠️ No active group - creating default group");
                    StartGroup("Deferred Operations");
                }

                using (var transaction = new Transaction(_document, transactionName))
                {
                    try
                    {
                        transaction.Start();
                        action();
                        transaction.Commit();
                        
                        _transactionCount++;
                        _logger.Log($"✅ Deferred transaction completed: {transactionName} (#{_transactionCount})");
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        _logger.LogError($"❌ Deferred transaction failed: {transactionName} - {ex.Message}");
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Execute parameter update with ElementBind short-circuit optimization
        /// </summary>
        public void UpdateParameter(Element element, string parameterName, object newValue, string reason = "")
        {
            ExecuteInTransaction($"Update {parameterName}", () =>
            {
                try
                {
                    // ElementBind Short-circuit - Check if target value already matches (expert optimization)
                    var parameter = element.LookupParameter(parameterName);
                    if (parameter == null)
                    {
                        _logger.Log($"⚠️ Parameter not found: {parameterName} on element {element.Id}");
                        return;
                    }

                    // Check current value to avoid unnecessary writes
                    string currentValue = GetParameterValueAsString(parameter);
                    string targetValue = newValue?.ToString() ?? "";

                    if (currentValue == targetValue)
                    {
                        _logger.Log($"⚡ Short-circuit: {parameterName} already has target value '{targetValue}'");
                        return;
                    }

                    // Set the new value
                    SetParameterValue(parameter, newValue);
                    
                    string logReason = !string.IsNullOrEmpty(reason) ? $" ({reason})" : "";
                    _logger.Log($"🔧 Updated {parameterName}: '{currentValue}' → '{targetValue}'{logReason}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Failed to update parameter {parameterName}: {ex.Message}");
                    throw;
                }
            });
        }

        /// <summary>
        /// Commit the current transaction group using Assimilate() for optimal performance
        /// </summary>
        public void CommitGroup()
        {
            lock (_lockObject)
            {
                if (_disposed || _currentGroup == null)
                    return;

                try
                {
                    // Use Assimilate() for better performance (expert recommendation)
                    _currentGroup.Assimilate();
                    _logger.Log($"✅ Committed deferred transaction group with {_transactionCount} transactions using Assimilate()");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Failed to commit transaction group: {ex.Message}");
                    _currentGroup.RollBack();
                    throw;
                }
                finally
                {
                    _currentGroup?.Dispose();
                    _currentGroup = null;
                    _transactionCount = 0;
                }
            }
        }

        /// <summary>
        /// Rollback the current transaction group
        /// </summary>
        public void RollbackGroup()
        {
            lock (_lockObject)
            {
                if (_disposed || _currentGroup == null)
                    return;

                try
                {
                    _currentGroup.RollBack();
                    _logger.Log($"🔄 Rolled back deferred transaction group with {_transactionCount} transactions");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Failed to rollback transaction group: {ex.Message}");
                }
                finally
                {
                    _currentGroup?.Dispose();
                    _currentGroup = null;
                    _transactionCount = 0;
                }
            }
        }

        /// <summary>
        /// Get parameter value as string for comparison
        /// </summary>
        private string GetParameterValueAsString(Parameter parameter)
        {
            if (parameter == null || !parameter.HasValue)
                return "";

            switch (parameter.StorageType)
            {
                case StorageType.String:
                    return parameter.AsString() ?? "";
                case StorageType.Integer:
                    return parameter.AsInteger().ToString();
                case StorageType.Double:
                    return parameter.AsDouble().ToString("F6");
                case StorageType.ElementId:
                    return parameter.AsElementId()?.IntegerValue.ToString() ?? "";
                default:
                    return parameter.AsValueString() ?? "";
            }
        }

        /// <summary>
        /// Set parameter value based on storage type
        /// </summary>
        private void SetParameterValue(Parameter parameter, object value)
        {
            if (parameter == null || parameter.IsReadOnly)
                return;

            switch (parameter.StorageType)
            {
                case StorageType.String:
                    parameter.Set(value?.ToString() ?? "");
                    break;
                case StorageType.Integer:
                    if (int.TryParse(value?.ToString(), out int intValue))
                        parameter.Set(intValue);
                    break;
                case StorageType.Double:
                    if (double.TryParse(value?.ToString(), out double doubleValue))
                        parameter.Set(doubleValue);
                    break;
                case StorageType.ElementId:
                    if (value is ElementId elementId)
                        parameter.Set(elementId);
                    else if (int.TryParse(value?.ToString(), out int idValue))
                        parameter.Set(new ElementId(idValue));
                    break;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            lock (_lockObject)
            {
                if (_currentGroup != null)
                {
                    _logger.Log("🧹 Disposing with active group - committing...");
                    CommitGroup();
                }
            }

            _logger.Log("✅ DeferredTransactionManager disposed");
        }
    }
}
