using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TycoonRevitAddin.AIActions.Commands;
using TycoonRevitAddin.AIActions.Workflow;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.AIActions.Autonomy
{
    /// <summary>
    /// Manages user autonomy levels for AI actions
    /// Implements Advisor/Assistant/Autopilot modes as recommended by o3-pro
    /// </summary>
    public interface IAutonomyManager
    {
        /// <summary>
        /// Current autonomy level
        /// </summary>
        UserAutonomyLevel CurrentLevel { get; }

        /// <summary>
        /// Set autonomy level
        /// </summary>
        void SetAutonomyLevel(UserAutonomyLevel level);

        /// <summary>
        /// Check if command is allowed at current autonomy level
        /// </summary>
        bool IsCommandAllowed(IAICommand command);

        /// <summary>
        /// Get required approval type for command
        /// </summary>
        ApprovalType GetRequiredApproval(IAICommand command);

        /// <summary>
        /// Execute command with appropriate autonomy controls
        /// </summary>
        Task<WorkflowResult> ExecuteWithAutonomyAsync(IAICommand command, CommandContext context);

        /// <summary>
        /// Get autonomy settings
        /// </summary>
        AutonomySettings GetSettings();

        /// <summary>
        /// Update autonomy settings
        /// </summary>
        void UpdateSettings(AutonomySettings settings);

        /// <summary>
        /// Get autonomy statistics
        /// </summary>
        AutonomyStatistics GetStatistics();
    }

    /// <summary>
    /// User autonomy levels with increasing AI freedom
    /// </summary>
    public enum UserAutonomyLevel
    {
        /// <summary>
        /// Analysis only - AI cannot modify the model
        /// </summary>
        Advisor = 0,

        /// <summary>
        /// AI can stage changes but requires user approval before execution
        /// </summary>
        Assistant = 1,

        /// <summary>
        /// AI can execute changes within predefined budgets and constraints
        /// </summary>
        Autopilot = 2
    }

    /// <summary>
    /// Types of approval required
    /// </summary>
    public enum ApprovalType
    {
        None,           // No approval needed
        Preview,        // Show preview, user can approve/reject
        Confirmation,   // Simple yes/no confirmation
        Detailed,       // Detailed review with element-by-element approval
        Manual          // User must execute manually
    }

    /// <summary>
    /// Autonomy settings and constraints
    /// </summary>
    public class AutonomySettings
    {
        public UserAutonomyLevel Level { get; set; } = UserAutonomyLevel.Assistant;
        public int MaxElementsPerCommand { get; set; } = 100;
        public int MaxCommandsPerSession { get; set; } = 50;
        public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromMinutes(5);
        public bool RequireConfirmationForDeletion { get; set; } = true;
        public bool RequireConfirmationForExpensiveOperations { get; set; } = true;
        public bool AllowWorksetModification { get; set; } = false;
        public bool AllowViewModification { get; set; } = false;
        public List<string> AllowedCommandTypes { get; set; } = new List<string>();
        public List<string> BlockedCommandTypes { get; set; } = new List<string>();
        public Dictionary<string, object> CustomConstraints { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Autonomy usage statistics
    /// </summary>
    public class AutonomyStatistics
    {
        public int CommandsExecuted { get; set; }
        public int CommandsBlocked { get; set; }
        public int ApprovalsRequested { get; set; }
        public int ApprovalsGranted { get; set; }
        public int ApprovalsDenied { get; set; }
        public Dictionary<UserAutonomyLevel, int> CommandsByLevel { get; set; } = new Dictionary<UserAutonomyLevel, int>();
        public Dictionary<string, int> CommandsByType { get; set; } = new Dictionary<string, int>();
        public TimeSpan TotalExecutionTime { get; set; }
        public DateTime LastCommandTime { get; set; }
    }

    /// <summary>
    /// Implementation of autonomy manager
    /// </summary>
    public class AutonomyManager : IAutonomyManager
    {
        private readonly IWorkflowEngine _workflowEngine;
        private readonly Logger _logger;
        private readonly object _lock = new object();

        private AutonomySettings _settings;
        private AutonomyStatistics _statistics;

        public UserAutonomyLevel CurrentLevel => _settings.Level;

        public AutonomyManager(IWorkflowEngine workflowEngine, Logger logger)
        {
            _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _settings = new AutonomySettings();
            _statistics = new AutonomyStatistics();

            _logger.Log($"🤖 AutonomyManager initialized at {_settings.Level} level");
        }

        public void SetAutonomyLevel(UserAutonomyLevel level)
        {
            lock (_lock)
            {
                var previousLevel = _settings.Level;
                _settings.Level = level;
                
                _logger.Log($"🔄 Autonomy level changed: {previousLevel} → {level}");
            }
        }

        public bool IsCommandAllowed(IAICommand command)
        {
            if (command == null) return false;

            lock (_lock)
            {
                // Check blocked commands
                if (_settings.BlockedCommandTypes.Contains(command.CommandType))
                {
                    _logger.Log($"🚫 Command {command.CommandType} is blocked");
                    return false;
                }

                // Check allowed commands (if whitelist is specified)
                if (_settings.AllowedCommandTypes.Any() && 
                    !_settings.AllowedCommandTypes.Contains(command.CommandType))
                {
                    _logger.Log($"🚫 Command {command.CommandType} not in allowed list");
                    return false;
                }

                // Check autonomy level constraints
                switch (_settings.Level)
                {
                    case UserAutonomyLevel.Advisor:
                        // Only analysis commands allowed
                        return IsAnalysisCommand(command);

                    case UserAutonomyLevel.Assistant:
                        // All commands allowed but require approval
                        return true;

                    case UserAutonomyLevel.Autopilot:
                        // All commands allowed within budget constraints
                        return command.EstimatedBudget <= _settings.MaxElementsPerCommand;

                    default:
                        return false;
                }
            }
        }

        public ApprovalType GetRequiredApproval(IAICommand command)
        {
            if (command == null) return ApprovalType.Manual;

            lock (_lock)
            {
                switch (_settings.Level)
                {
                    case UserAutonomyLevel.Advisor:
                        return ApprovalType.None; // Analysis only, no approval needed

                    case UserAutonomyLevel.Assistant:
                        // Determine approval type based on command characteristics
                        if (IsDestructiveCommand(command) && _settings.RequireConfirmationForDeletion)
                            return ApprovalType.Detailed;
                        
                        if (IsExpensiveCommand(command) && _settings.RequireConfirmationForExpensiveOperations)
                            return ApprovalType.Detailed;
                        
                        if (command.RequiresConfirmation)
                            return ApprovalType.Preview;
                        
                        return ApprovalType.Confirmation;

                    case UserAutonomyLevel.Autopilot:
                        // Check if within budget constraints
                        if (command.EstimatedBudget <= _settings.MaxElementsPerCommand)
                        {
                            if (IsDestructiveCommand(command) && _settings.RequireConfirmationForDeletion)
                                return ApprovalType.Confirmation;
                            
                            return ApprovalType.None;
                        }
                        else
                        {
                            return ApprovalType.Detailed; // Exceeds budget, needs detailed review
                        }

                    default:
                        return ApprovalType.Manual;
                }
            }
        }

        public async Task<WorkflowResult> ExecuteWithAutonomyAsync(IAICommand command, CommandContext context)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // Check if command is allowed
                if (!IsCommandAllowed(command))
                {
                    lock (_lock)
                    {
                        _statistics.CommandsBlocked++;
                    }

                    return WorkflowResult.CreateFailure(WorkflowStage.Preview,
                        $"Command {command.CommandType} not allowed at {_settings.Level} autonomy level");
                }

                // Set autonomy level in context
                context.AutonomyLevel = (Commands.UserAutonomyLevel)(int)_settings.Level;
                context.OperationBudget = _settings.MaxElementsPerCommand;

                // Get required approval type
                var approvalType = GetRequiredApproval(command);
                
                WorkflowResult result;

                switch (approvalType)
                {
                    case ApprovalType.None:
                        // Execute directly (Autopilot mode within budget)
                        result = await _workflowEngine.ExecuteWorkflowAsync(command, context);
                        break;

                    case ApprovalType.Preview:
                    case ApprovalType.Confirmation:
                    case ApprovalType.Detailed:
                        // Execute Preview-Apply-Commit workflow
                        result = await _workflowEngine.PreviewAsync(command, context);
                        
                        if (result.Success)
                        {
                            lock (_lock)
                            {
                                _statistics.ApprovalsRequested++;
                            }

                            // In a real implementation, this would show UI for user approval
                            // For now, we'll simulate approval based on autonomy level
                            var approved = SimulateUserApproval(command, approvalType, result);
                            
                            if (approved)
                            {
                                lock (_lock)
                                {
                                    _statistics.ApprovalsGranted++;
                                }

                                var applyResult = await _workflowEngine.ApplyAsync(result.WorkflowId, context);
                                if (applyResult.Success)
                                {
                                    result = await _workflowEngine.CommitAsync(result.WorkflowId, context);
                                }
                                else
                                {
                                    result = applyResult;
                                }
                            }
                            else
                            {
                                lock (_lock)
                                {
                                    _statistics.ApprovalsDenied++;
                                }

                                await _workflowEngine.CancelAsync(result.WorkflowId);
                                result = WorkflowResult.CreateFailure(WorkflowStage.Preview, "User denied approval");
                            }
                        }
                        break;

                    case ApprovalType.Manual:
                        result = WorkflowResult.CreateFailure(WorkflowStage.Preview,
                            "Command requires manual execution");
                        break;

                    default:
                        result = WorkflowResult.CreateFailure(WorkflowStage.Preview,
                            "Unknown approval type");
                        break;
                }

                // Update statistics
                lock (_lock)
                {
                    if (result.Success)
                    {
                        _statistics.CommandsExecuted++;
                        _statistics.TotalExecutionTime += DateTime.UtcNow - startTime;
                        _statistics.LastCommandTime = DateTime.UtcNow;

                        if (_statistics.CommandsByLevel.ContainsKey(_settings.Level))
                            _statistics.CommandsByLevel[_settings.Level]++;
                        else
                            _statistics.CommandsByLevel[_settings.Level] = 1;

                        if (_statistics.CommandsByType.ContainsKey(command.CommandType))
                            _statistics.CommandsByType[command.CommandType]++;
                        else
                            _statistics.CommandsByType[command.CommandType] = 1;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error executing command with autonomy: {command.CommandType}", ex);
                return WorkflowResult.CreateFailure(WorkflowStage.Failed, $"Execution failed: {ex.Message}", ex);
            }
        }

        public AutonomySettings GetSettings()
        {
            lock (_lock)
            {
                return new AutonomySettings
                {
                    Level = _settings.Level,
                    MaxElementsPerCommand = _settings.MaxElementsPerCommand,
                    MaxCommandsPerSession = _settings.MaxCommandsPerSession,
                    MaxExecutionTime = _settings.MaxExecutionTime,
                    RequireConfirmationForDeletion = _settings.RequireConfirmationForDeletion,
                    RequireConfirmationForExpensiveOperations = _settings.RequireConfirmationForExpensiveOperations,
                    AllowWorksetModification = _settings.AllowWorksetModification,
                    AllowViewModification = _settings.AllowViewModification,
                    AllowedCommandTypes = new List<string>(_settings.AllowedCommandTypes),
                    BlockedCommandTypes = new List<string>(_settings.BlockedCommandTypes),
                    CustomConstraints = new Dictionary<string, object>(_settings.CustomConstraints)
                };
            }
        }

        public void UpdateSettings(AutonomySettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            lock (_lock)
            {
                _settings = settings;
                _logger.Log($"⚙️ Autonomy settings updated - Level: {settings.Level}, Max Elements: {settings.MaxElementsPerCommand}");
            }
        }

        public AutonomyStatistics GetStatistics()
        {
            lock (_lock)
            {
                return new AutonomyStatistics
                {
                    CommandsExecuted = _statistics.CommandsExecuted,
                    CommandsBlocked = _statistics.CommandsBlocked,
                    ApprovalsRequested = _statistics.ApprovalsRequested,
                    ApprovalsGranted = _statistics.ApprovalsGranted,
                    ApprovalsDenied = _statistics.ApprovalsDenied,
                    CommandsByLevel = new Dictionary<UserAutonomyLevel, int>(_statistics.CommandsByLevel),
                    CommandsByType = new Dictionary<string, int>(_statistics.CommandsByType),
                    TotalExecutionTime = _statistics.TotalExecutionTime,
                    LastCommandTime = _statistics.LastCommandTime
                };
            }
        }

        private bool IsAnalysisCommand(IAICommand command)
        {
            // Commands that only read/analyze data
            var analysisCommands = new[] { "Analyze", "Query", "Report", "Validate", "Inspect" };
            return analysisCommands.Any(ac => command.CommandType.Contains(ac));
        }

        private bool IsDestructiveCommand(IAICommand command)
        {
            // Commands that delete or significantly modify elements
            var destructiveCommands = new[] { "Delete", "Remove", "Clear", "Reset" };
            return destructiveCommands.Any(dc => command.CommandType.Contains(dc));
        }

        private bool IsExpensiveCommand(IAICommand command)
        {
            // Commands that are computationally expensive or affect many elements
            return command.EstimatedBudget > 50 || 
                   command.MaxExecutionTime > TimeSpan.FromMinutes(2);
        }

        private bool SimulateUserApproval(IAICommand command, ApprovalType approvalType, WorkflowResult previewResult)
        {
            // In a real implementation, this would show UI for user approval
            // For simulation purposes, we'll approve based on some criteria
            
            switch (approvalType)
            {
                case ApprovalType.Confirmation:
                    return true; // Auto-approve simple confirmations in simulation

                case ApprovalType.Preview:
                    // Approve if preview looks reasonable
                    return previewResult.Success && previewResult.Diff?.ElementsToCreate <= 10;

                case ApprovalType.Detailed:
                    // More conservative approval for detailed review
                    return previewResult.Success && 
                           previewResult.Diff?.ElementsToCreate <= 5 &&
                           previewResult.Diff?.ElementsToDelete == 0;

                default:
                    return false;
            }
        }
    }
}
