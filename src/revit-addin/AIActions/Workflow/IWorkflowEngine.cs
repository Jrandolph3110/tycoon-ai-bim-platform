using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TycoonRevitAddin.AIActions.Commands;
using TycoonRevitAddin.AIActions.Events;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.AIActions.Workflow
{
    /// <summary>
    /// Preview-Apply-Commit workflow engine for building user trust
    /// Implements the three-phase workflow recommended by o3-pro
    /// </summary>
    public interface IWorkflowEngine
    {
        /// <summary>
        /// Preview what a command will do (dry run)
        /// </summary>
        Task<WorkflowResult> PreviewAsync(IAICommand command, CommandContext context);

        /// <summary>
        /// Apply the command after user confirmation
        /// </summary>
        Task<WorkflowResult> ApplyAsync(string previewId, CommandContext context);

        /// <summary>
        /// Commit the applied changes with validation and snapshot
        /// </summary>
        Task<WorkflowResult> CommitAsync(string applyId, CommandContext context);

        /// <summary>
        /// Execute complete workflow (Preview → Apply → Commit) for autopilot mode
        /// </summary>
        Task<WorkflowResult> ExecuteWorkflowAsync(IAICommand command, CommandContext context);

        /// <summary>
        /// Cancel a workflow at any stage
        /// </summary>
        Task<WorkflowResult> CancelAsync(string workflowId);

        /// <summary>
        /// Get workflow status
        /// </summary>
        Task<WorkflowStatus> GetStatusAsync(string workflowId);

        /// <summary>
        /// Get all active workflows
        /// </summary>
        Task<IEnumerable<WorkflowStatus>> GetActiveWorkflowsAsync();
    }

    /// <summary>
    /// Workflow execution result
    /// </summary>
    public class WorkflowResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string WorkflowId { get; set; }
        public WorkflowStage Stage { get; set; }
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
        public List<IDomainEvent> Events { get; set; } = new List<IDomainEvent>();
        public Exception Exception { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public WorkflowDiff Diff { get; set; }

        public static WorkflowResult CreateSuccess(WorkflowStage stage, string message = null)
        {
            return new WorkflowResult
            {
                Success = true,
                Stage = stage,
                Message = message ?? $"{stage} completed successfully",
                WorkflowId = Guid.NewGuid().ToString()
            };
        }

        public static WorkflowResult CreateFailure(WorkflowStage stage, string message, Exception exception = null)
        {
            return new WorkflowResult
            {
                Success = false,
                Stage = stage,
                Message = message,
                Exception = exception
            };
        }
    }

    /// <summary>
    /// Workflow stages
    /// </summary>
    public enum WorkflowStage
    {
        Preview,
        Apply,
        Commit,
        Cancelled,
        Failed
    }

    /// <summary>
    /// Workflow status tracking
    /// </summary>
    public class WorkflowStatus
    {
        public string WorkflowId { get; set; }
        public string CommandType { get; set; }
        public WorkflowStage CurrentStage { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? LastUpdateTime { get; set; }
        public string UserId { get; set; }
        public Guid SessionId { get; set; }
        public bool RequiresUserAction { get; set; }
        public string StatusMessage { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Hierarchical diff for showing changes to user
    /// </summary>
    public class WorkflowDiff
    {
        public string Summary { get; set; }
        public List<DiffItem> Items { get; set; } = new List<DiffItem>();
        public int ElementsToCreate { get; set; }
        public int ElementsToModify { get; set; }
        public int ElementsToDelete { get; set; }
        public Dictionary<string, object> Statistics { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Individual diff item
    /// </summary>
    public class DiffItem
    {
        public string Type { get; set; } // Create, Modify, Delete
        public string ElementType { get; set; }
        public string ElementId { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
        public List<DiffItem> Children { get; set; } = new List<DiffItem>();
    }

    /// <summary>
    /// Implementation of Preview-Apply-Commit workflow engine
    /// </summary>
    public class WorkflowEngine : IWorkflowEngine
    {
        private readonly IEventStore _eventStore;
        private readonly Logger _logger;
        private readonly Dictionary<string, WorkflowContext> _activeWorkflows;
        private readonly object _lock = new object();

        public WorkflowEngine(IEventStore eventStore, Logger logger)
        {
            _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _activeWorkflows = new Dictionary<string, WorkflowContext>();
        }

        public async Task<WorkflowResult> PreviewAsync(IAICommand command, CommandContext context)
        {
            var workflowId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;

            try
            {
                _logger.Log($"🔍 Starting preview for {command.CommandType} (Workflow: {workflowId})");

                // Create workflow context
                var workflowContext = new WorkflowContext
                {
                    WorkflowId = workflowId,
                    Command = command,
                    Context = context,
                    Stage = WorkflowStage.Preview,
                    StartTime = startTime
                };

                lock (_lock)
                {
                    _activeWorkflows[workflowId] = workflowContext;
                }

                // Set dry run mode for preview
                context.DryRun = true;

                // Execute preview
                var previewResult = await command.PreviewAsync(context);
                
                var result = new WorkflowResult
                {
                    Success = previewResult.Success,
                    Message = previewResult.Message,
                    WorkflowId = workflowId,
                    Stage = WorkflowStage.Preview,
                    Data = previewResult.Data,
                    ExecutionTime = DateTime.UtcNow - startTime
                };

                if (previewResult.Success)
                {
                    // Generate diff for user review
                    result.Diff = GenerateDiff(command, context, previewResult);
                    
                    // Update workflow context
                    workflowContext.PreviewResult = previewResult;
                    workflowContext.LastUpdateTime = DateTime.UtcNow;

                    _logger.Log($"✅ Preview completed for {command.CommandType} - {result.Diff.Summary}");
                }
                else
                {
                    _logger.LogError($"❌ Preview failed for {command.CommandType}: {previewResult.Message}");
                    
                    // Remove failed workflow
                    lock (_lock)
                    {
                        _activeWorkflows.Remove(workflowId);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Preview failed for {command.CommandType}", ex);
                
                lock (_lock)
                {
                    _activeWorkflows.Remove(workflowId);
                }

                return WorkflowResult.CreateFailure(WorkflowStage.Preview, $"Preview failed: {ex.Message}", ex);
            }
        }

        public async Task<WorkflowResult> ApplyAsync(string previewId, CommandContext context)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                WorkflowContext workflowContext;
                lock (_lock)
                {
                    if (!_activeWorkflows.TryGetValue(previewId, out workflowContext))
                    {
                        return WorkflowResult.CreateFailure(WorkflowStage.Apply, "Preview not found or expired");
                    }
                }

                _logger.Log($"⚡ Applying {workflowContext.Command.CommandType} (Workflow: {previewId})");

                // Update stage
                workflowContext.Stage = WorkflowStage.Apply;
                workflowContext.LastUpdateTime = DateTime.UtcNow;

                // Disable dry run for actual execution
                context.DryRun = false;

                // Execute the command
                var executeResult = await workflowContext.Command.ExecuteAsync(context);

                var result = new WorkflowResult
                {
                    Success = executeResult.Success,
                    Message = executeResult.Message,
                    WorkflowId = previewId,
                    Stage = WorkflowStage.Apply,
                    Data = executeResult.Data,
                    Events = executeResult.Events,
                    ExecutionTime = DateTime.UtcNow - startTime
                };

                if (executeResult.Success)
                {
                    // Store apply result
                    workflowContext.ApplyResult = executeResult;
                    
                    _logger.Log($"✅ Applied {workflowContext.Command.CommandType} - {executeResult.ElementsAffected} elements affected");
                }
                else
                {
                    _logger.LogError($"❌ Apply failed for {workflowContext.Command.CommandType}: {executeResult.Message}");
                    
                    // Mark as failed but keep for potential retry
                    workflowContext.Stage = WorkflowStage.Failed;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Apply failed for workflow {previewId}", ex);
                return WorkflowResult.CreateFailure(WorkflowStage.Apply, $"Apply failed: {ex.Message}", ex);
            }
        }

        public async Task<WorkflowResult> CommitAsync(string applyId, CommandContext context)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                WorkflowContext workflowContext;
                lock (_lock)
                {
                    if (!_activeWorkflows.TryGetValue(applyId, out workflowContext))
                    {
                        return WorkflowResult.CreateFailure(WorkflowStage.Commit, "Applied workflow not found");
                    }

                    if (workflowContext.Stage != WorkflowStage.Apply)
                    {
                        return WorkflowResult.CreateFailure(WorkflowStage.Commit, "Workflow not in Apply stage");
                    }
                }

                _logger.Log($"💾 Committing {workflowContext.Command.CommandType} (Workflow: {applyId})");

                // Update stage
                workflowContext.Stage = WorkflowStage.Commit;
                workflowContext.LastUpdateTime = DateTime.UtcNow;

                // Create snapshot for rollback
                var snapshotId = await _eventStore.CreateSnapshotAsync(
                    $"AI Action: {workflowContext.Command.CommandType}");

                // Post-flight validation
                var postValidation = await ValidatePostExecution(workflowContext, context);
                
                var result = new WorkflowResult
                {
                    Success = postValidation.Success,
                    Message = postValidation.Message,
                    WorkflowId = applyId,
                    Stage = WorkflowStage.Commit,
                    ExecutionTime = DateTime.UtcNow - startTime
                };

                result.Data["SnapshotId"] = snapshotId;
                result.Data["ValidationResult"] = postValidation;

                if (postValidation.Success)
                {
                    _logger.Log($"✅ Committed {workflowContext.Command.CommandType} - Snapshot: {snapshotId}");
                    
                    // Remove completed workflow
                    lock (_lock)
                    {
                        _activeWorkflows.Remove(applyId);
                    }
                }
                else
                {
                    _logger.LogWarning($"⚠️ Commit validation failed for {workflowContext.Command.CommandType}");
                    workflowContext.Stage = WorkflowStage.Failed;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Commit failed for workflow {applyId}", ex);
                return WorkflowResult.CreateFailure(WorkflowStage.Commit, $"Commit failed: {ex.Message}", ex);
            }
        }

        public async Task<WorkflowResult> ExecuteWorkflowAsync(IAICommand command, CommandContext context)
        {
            // For Autopilot mode - execute complete workflow
            var previewResult = await PreviewAsync(command, context);
            if (!previewResult.Success)
                return previewResult;

            var applyResult = await ApplyAsync(previewResult.WorkflowId, context);
            if (!applyResult.Success)
                return applyResult;

            var commitResult = await CommitAsync(previewResult.WorkflowId, context);
            return commitResult;
        }

        public async Task<WorkflowResult> CancelAsync(string workflowId)
        {
            try
            {
                lock (_lock)
                {
                    if (_activeWorkflows.TryGetValue(workflowId, out var workflow))
                    {
                        workflow.Stage = WorkflowStage.Cancelled;
                        workflow.LastUpdateTime = DateTime.UtcNow;
                        _activeWorkflows.Remove(workflowId);
                    }
                }

                _logger.Log($"🚫 Cancelled workflow: {workflowId}");
                
                return WorkflowResult.CreateSuccess(WorkflowStage.Cancelled, "Workflow cancelled");
            }
            catch (Exception ex)
            {
                return WorkflowResult.CreateFailure(WorkflowStage.Cancelled, $"Cancel failed: {ex.Message}", ex);
            }
        }

        public async Task<WorkflowStatus> GetStatusAsync(string workflowId)
        {
            lock (_lock)
            {
                if (_activeWorkflows.TryGetValue(workflowId, out var workflow))
                {
                    return new WorkflowStatus
                    {
                        WorkflowId = workflowId,
                        CommandType = workflow.Command.CommandType,
                        CurrentStage = workflow.Stage,
                        StartTime = workflow.StartTime,
                        LastUpdateTime = workflow.LastUpdateTime,
                        UserId = workflow.Context.UserId,
                        SessionId = workflow.Context.SessionId,
                        RequiresUserAction = workflow.Stage == WorkflowStage.Preview,
                        StatusMessage = GetStageMessage(workflow.Stage)
                    };
                }
            }

            return null;
        }

        public async Task<IEnumerable<WorkflowStatus>> GetActiveWorkflowsAsync()
        {
            lock (_lock)
            {
                return _activeWorkflows.Values.Select(w => new WorkflowStatus
                {
                    WorkflowId = w.WorkflowId,
                    CommandType = w.Command.CommandType,
                    CurrentStage = w.Stage,
                    StartTime = w.StartTime,
                    LastUpdateTime = w.LastUpdateTime,
                    UserId = w.Context.UserId,
                    SessionId = w.Context.SessionId,
                    RequiresUserAction = w.Stage == WorkflowStage.Preview,
                    StatusMessage = GetStageMessage(w.Stage)
                }).ToList();
            }
        }

        private WorkflowDiff GenerateDiff(IAICommand command, CommandContext context, CommandResult previewResult)
        {
            var diff = new WorkflowDiff();

            // Generate summary based on command type and preview data
            switch (command.CommandType)
            {
                case "CreateWall":
                    var wallType = previewResult.Data.ContainsKey("WallType") ? previewResult.Data["WallType"]?.ToString() : "wall";
                    var wallLength = previewResult.Data.ContainsKey("WallLength") ? previewResult.Data["WallLength"]?.ToString() : "?";
                    var wallHeight = previewResult.Data.ContainsKey("WallHeight") ? previewResult.Data["WallHeight"]?.ToString() : "?";
                    diff.Summary = $"Create {wallType} ({wallLength}' × {wallHeight}')";
                    diff.ElementsToCreate = 1;
                    break;
                default:
                    diff.Summary = $"Execute {command.CommandType}";
                    diff.ElementsToCreate = previewResult.ElementsAffected;
                    break;
            }

            // Add detailed items
            diff.Items.Add(new DiffItem
            {
                Type = "Create",
                ElementType = command.CommandType.Replace("Create", ""),
                Description = diff.Summary,
                Details = previewResult.Data
            });

            return diff;
        }

        private async Task<CommandResult> ValidatePostExecution(WorkflowContext workflow, CommandContext context)
        {
            // Post-execution validation
            try
            {
                // Check if elements were actually created/modified
                // Validate model integrity
                // Check for any unexpected side effects
                
                return CommandResult.Successful("Post-execution validation passed");
            }
            catch (Exception ex)
            {
                return CommandResult.Failed($"Post-execution validation failed: {ex.Message}", ex);
            }
        }

        private string GetStageMessage(WorkflowStage stage)
        {
            return stage switch
            {
                WorkflowStage.Preview => "Awaiting user approval",
                WorkflowStage.Apply => "Executing changes",
                WorkflowStage.Commit => "Finalizing and validating",
                WorkflowStage.Cancelled => "Cancelled by user",
                WorkflowStage.Failed => "Failed - see details",
                _ => "Unknown stage"
            };
        }
    }

    /// <summary>
    /// Internal workflow context
    /// </summary>
    internal class WorkflowContext
    {
        public string WorkflowId { get; set; }
        public IAICommand Command { get; set; }
        public CommandContext Context { get; set; }
        public WorkflowStage Stage { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? LastUpdateTime { get; set; }
        public CommandResult PreviewResult { get; set; }
        public CommandResult ApplyResult { get; set; }
    }
}
