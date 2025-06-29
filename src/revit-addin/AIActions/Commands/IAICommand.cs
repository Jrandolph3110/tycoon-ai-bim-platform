using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using TycoonRevitAddin.AIActions.Events;
using TycoonRevitAddin.AIActions.Autonomy;

namespace TycoonRevitAddin.AIActions.Commands
{
    /// <summary>
    /// Result of command execution with event sourcing
    /// </summary>
    public class CommandResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<IDomainEvent> Events { get; set; } = new List<IDomainEvent>();
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
        public Exception Exception { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public int ElementsAffected { get; set; }

        public static CommandResult Successful(string message = "Command executed successfully")
        {
            return new CommandResult { Success = true, Message = message };
        }

        public static CommandResult Failed(string message, Exception exception = null)
        {
            return new CommandResult { Success = false, Message = message, Exception = exception };
        }
    }

    /// <summary>
    /// Command execution context with safety and audit information
    /// </summary>
    public class CommandContext
    {
        public Guid CommandId { get; set; } = Guid.NewGuid();
        public Guid SessionId { get; set; }
        public string UserId { get; set; } = Environment.UserName;
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
        public Document Document { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public IProgress<string> Progress { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public UserAutonomyLevel AutonomyLevel { get; set; } = UserAutonomyLevel.Assistant;
        public int OperationBudget { get; set; } = 100; // Max elements to affect
        public bool RequiresConfirmation { get; set; } = true;
        public bool DryRun { get; set; } = false; // Preview mode
    }

    /// <summary>
    /// User autonomy levels for AI actions (Commands namespace)
    /// </summary>
    public enum UserAutonomyLevel
    {
        /// <summary>
        /// Analysis only - no modifications allowed
        /// </summary>
        Advisor = 0,

        /// <summary>
        /// Can stage changes but requires user approval
        /// </summary>
        Assistant = 1,

        /// <summary>
        /// Can commit changes within operation budgets
        /// </summary>
        Autopilot = 2
    }



    /// <summary>
    /// Interface for AI commands with event sourcing and safety features
    /// Based on o3-pro recommendations for enterprise-grade AI actions
    /// </summary>
    public interface IAICommand
    {
        /// <summary>
        /// Unique identifier for this command type
        /// </summary>
        string CommandType { get; }

        /// <summary>
        /// Human-readable description of what this command does
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Command parameters (validated before execution)
        /// </summary>
        Dictionary<string, object> Parameters { get; }

        /// <summary>
        /// Whether this command requires user confirmation
        /// </summary>
        bool RequiresConfirmation { get; }

        /// <summary>
        /// Whether this command can be undone
        /// </summary>
        bool CanUndo { get; }

        /// <summary>
        /// Estimated operation budget (number of elements affected)
        /// </summary>
        int EstimatedBudget { get; }

        /// <summary>
        /// Maximum execution time before timeout
        /// </summary>
        TimeSpan MaxExecutionTime { get; }

        /// <summary>
        /// Validate command parameters and context (Static + Contextual + Semantic)
        /// </summary>
        Task<ValidationResult> ValidateAsync(CommandContext context);

        /// <summary>
        /// Preview what the command will do (dry run)
        /// </summary>
        Task<CommandResult> PreviewAsync(CommandContext context);

        /// <summary>
        /// Execute the command and emit domain events
        /// </summary>
        Task<CommandResult> ExecuteAsync(CommandContext context);

        /// <summary>
        /// Undo the command using event history
        /// </summary>
        Task<CommandResult> UndoAsync(CommandContext context, IEnumerable<IDomainEvent> events);
    }

    /// <summary>
    /// Validation result with three-phase validation as recommended by o3-pro
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidationError> Errors { get; set; } = new List<ValidationError>();
        public ValidationPhase FailedPhase { get; set; }

        public static ValidationResult Success()
        {
            return new ValidationResult { IsValid = true };
        }

        public static ValidationResult Failure(ValidationPhase phase, string message, string property = null)
        {
            return new ValidationResult
            {
                IsValid = false,
                FailedPhase = phase,
                Errors = new List<ValidationError>
                {
                    new ValidationError { Phase = phase, Message = message, Property = property }
                }
            };
        }
    }

    /// <summary>
    /// Three-phase validation as recommended by o3-pro
    /// </summary>
    public enum ValidationPhase
    {
        /// <summary>
        /// Schema and type checks on JSON parameters
        /// </summary>
        Static = 1,

        /// <summary>
        /// Element existence, workset ownership, view constraints
        /// </summary>
        Contextual = 2,

        /// <summary>
        /// Discipline-specific rules (FLC steel framing, clearances, etc.)
        /// </summary>
        Semantic = 3
    }

    /// <summary>
    /// Validation error details
    /// </summary>
    public class ValidationError
    {
        public ValidationPhase Phase { get; set; }
        public string Message { get; set; }
        public string Property { get; set; }
        public string Code { get; set; }
        public object Value { get; set; }
    }

    /// <summary>
    /// Base implementation of AI command with event sourcing
    /// </summary>
    public abstract class AICommandBase : IAICommand
    {
        protected readonly IEventStore _eventStore;
        protected int _sequenceNumber = 0;

        public abstract string CommandType { get; }
        public abstract string Description { get; }
        public Dictionary<string, object> Parameters { get; protected set; } = new Dictionary<string, object>();
        public virtual bool RequiresConfirmation => true;
        public virtual bool CanUndo => true;
        public virtual int EstimatedBudget => 10;
        public virtual TimeSpan MaxExecutionTime => TimeSpan.FromMinutes(5);

        protected AICommandBase(IEventStore eventStore)
        {
            _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        }

        public virtual async Task<ValidationResult> ValidateAsync(CommandContext context)
        {
            // Phase 1: Static validation (schema/type checks)
            var staticResult = await ValidateStaticAsync(context);
            if (!staticResult.IsValid) return staticResult;

            // Phase 2: Contextual validation (element existence, worksets, etc.)
            var contextualResult = await ValidateContextualAsync(context);
            if (!contextualResult.IsValid) return contextualResult;

            // Phase 3: Semantic validation (discipline-specific rules)
            var semanticResult = await ValidateSemanticAsync(context);
            return semanticResult;
        }

        protected virtual Task<ValidationResult> ValidateStaticAsync(CommandContext context)
        {
            // Override in derived classes for parameter validation
            return Task.FromResult(ValidationResult.Success());
        }

        protected virtual Task<ValidationResult> ValidateContextualAsync(CommandContext context)
        {
            // Override in derived classes for context validation
            if (context.Document == null)
            {
                return Task.FromResult(ValidationResult.Failure(
                    ValidationPhase.Contextual, 
                    "No active document available"));
            }

            return Task.FromResult(ValidationResult.Success());
        }

        protected virtual Task<ValidationResult> ValidateSemanticAsync(CommandContext context)
        {
            // Override in derived classes for semantic validation
            return Task.FromResult(ValidationResult.Success());
        }

        public abstract Task<CommandResult> PreviewAsync(CommandContext context);
        public abstract Task<CommandResult> ExecuteAsync(CommandContext context);

        public virtual async Task<CommandResult> UndoAsync(CommandContext context, IEnumerable<IDomainEvent> events)
        {
            // Default undo implementation - override for custom undo logic
            var result = new CommandResult();
            
            try
            {
                var eventList = events.ToList();
                for (int i = eventList.Count - 1; i >= 0; i--)
                {
                    var domainEvent = eventList[i];
                    if (domainEvent.CanUndo)
                    {
                        await UndoEventAsync(context, domainEvent);
                    }
                }

                result.Success = true;
                result.Message = "Command undone successfully";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Undo failed: {ex.Message}";
                result.Exception = ex;
            }

            return result;
        }

        protected virtual Task UndoEventAsync(CommandContext context, IDomainEvent domainEvent)
        {
            // Override in derived classes for specific undo logic
            return Task.CompletedTask;
        }

        /// <summary>
        /// Helper to emit domain events during command execution
        /// </summary>
        protected async Task EmitEventAsync(IDomainEvent domainEvent)
        {
            await _eventStore.AppendEventAsync(domainEvent);
        }

        /// <summary>
        /// Helper to create events with proper sequencing
        /// </summary>
        protected T CreateEvent<T>(Func<int, T> eventFactory) where T : IDomainEvent
        {
            return eventFactory(++_sequenceNumber);
        }
    }
}
