using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using TycoonRevitAddin.AIActions.Commands;

namespace TycoonRevitAddin.AIActions.Validation
{
    /// <summary>
    /// Three-phase validation engine as recommended by o3-pro
    /// Implements fail-fast validation with Static → Contextual → Semantic phases
    /// </summary>
    public interface IValidationEngine
    {
        /// <summary>
        /// Run complete three-phase validation
        /// </summary>
        Task<ValidationResult> ValidateAsync(IAICommand command, CommandContext context);

        /// <summary>
        /// Run only static validation (schema/type checks)
        /// </summary>
        Task<ValidationResult> ValidateStaticAsync(IAICommand command, CommandContext context);

        /// <summary>
        /// Run only contextual validation (element existence, worksets, etc.)
        /// </summary>
        Task<ValidationResult> ValidateContextualAsync(IAICommand command, CommandContext context);

        /// <summary>
        /// Run only semantic validation (discipline-specific rules)
        /// </summary>
        Task<ValidationResult> ValidateSemanticAsync(IAICommand command, CommandContext context);

        /// <summary>
        /// Register a custom validator
        /// </summary>
        void RegisterValidator(IValidator validator);

        /// <summary>
        /// Get validation statistics
        /// </summary>
        ValidationStatistics GetStatistics();
    }

    /// <summary>
    /// Interface for individual validators
    /// </summary>
    public interface IValidator
    {
        /// <summary>
        /// Validation phase this validator runs in
        /// </summary>
        ValidationPhase Phase { get; }

        /// <summary>
        /// Command types this validator applies to (null = all commands)
        /// </summary>
        string[] ApplicableCommandTypes { get; }

        /// <summary>
        /// Priority (lower numbers run first)
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Validate the command
        /// </summary>
        Task<ValidationResult> ValidateAsync(IAICommand command, CommandContext context);
    }

    /// <summary>
    /// Validation statistics for monitoring
    /// </summary>
    public class ValidationStatistics
    {
        public int TotalValidations { get; set; }
        public int StaticFailures { get; set; }
        public int ContextualFailures { get; set; }
        public int SemanticFailures { get; set; }
        public Dictionary<string, int> FailuresByCommand { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> FailuresByValidator { get; set; } = new Dictionary<string, int>();
        public TimeSpan AverageValidationTime { get; set; }
    }

    /// <summary>
    /// Implementation of three-phase validation engine
    /// </summary>
    public class ValidationEngine : IValidationEngine
    {
        private readonly List<IValidator> _validators = new List<IValidator>();
        private readonly ValidationStatistics _statistics = new ValidationStatistics();
        private readonly object _lock = new object();

        public ValidationEngine()
        {
            // Register built-in validators
            RegisterBuiltInValidators();
        }

        public async Task<ValidationResult> ValidateAsync(IAICommand command, CommandContext context)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                lock (_lock)
                {
                    _statistics.TotalValidations++;
                }

                // Phase 1: Static validation (fail fast)
                var staticResult = await ValidateStaticAsync(command, context);
                if (!staticResult.IsValid)
                {
                    lock (_lock)
                    {
                        _statistics.StaticFailures++;
                        IncrementCommandFailure(command.CommandType);
                    }
                    return staticResult;
                }

                // Phase 2: Contextual validation (fail fast)
                var contextualResult = await ValidateContextualAsync(command, context);
                if (!contextualResult.IsValid)
                {
                    lock (_lock)
                    {
                        _statistics.ContextualFailures++;
                        IncrementCommandFailure(command.CommandType);
                    }
                    return contextualResult;
                }

                // Phase 3: Semantic validation (fail fast)
                var semanticResult = await ValidateSemanticAsync(command, context);
                if (!semanticResult.IsValid)
                {
                    lock (_lock)
                    {
                        _statistics.SemanticFailures++;
                        IncrementCommandFailure(command.CommandType);
                    }
                    return semanticResult;
                }

                return ValidationResult.Success();
            }
            finally
            {
                var elapsed = DateTime.UtcNow - startTime;
                lock (_lock)
                {
                    // Update average validation time
                    var totalTime = _statistics.AverageValidationTime.TotalMilliseconds * (_statistics.TotalValidations - 1);
                    totalTime += elapsed.TotalMilliseconds;
                    _statistics.AverageValidationTime = TimeSpan.FromMilliseconds(totalTime / _statistics.TotalValidations);
                }
            }
        }

        public async Task<ValidationResult> ValidateStaticAsync(IAICommand command, CommandContext context)
        {
            return await RunValidatorsForPhase(ValidationPhase.Static, command, context);
        }

        public async Task<ValidationResult> ValidateContextualAsync(IAICommand command, CommandContext context)
        {
            return await RunValidatorsForPhase(ValidationPhase.Contextual, command, context);
        }

        public async Task<ValidationResult> ValidateSemanticAsync(IAICommand command, CommandContext context)
        {
            return await RunValidatorsForPhase(ValidationPhase.Semantic, command, context);
        }

        private async Task<ValidationResult> RunValidatorsForPhase(ValidationPhase phase, IAICommand command, CommandContext context)
        {
            var applicableValidators = _validators
                .Where(v => v.Phase == phase)
                .Where(v => v.ApplicableCommandTypes == null || v.ApplicableCommandTypes.Contains(command.CommandType))
                .OrderBy(v => v.Priority)
                .ToList();

            var allErrors = new List<ValidationError>();

            foreach (var validator in applicableValidators)
            {
                try
                {
                    var result = await validator.ValidateAsync(command, context);
                    if (!result.IsValid)
                    {
                        allErrors.AddRange(result.Errors);
                        
                        lock (_lock)
                        {
                            IncrementValidatorFailure(validator.GetType().Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    allErrors.Add(new ValidationError
                    {
                        Phase = phase,
                        Message = $"Validator {validator.GetType().Name} failed: {ex.Message}",
                        Code = "VALIDATOR_EXCEPTION"
                    });
                }
            }

            if (allErrors.Any())
            {
                return new ValidationResult
                {
                    IsValid = false,
                    FailedPhase = phase,
                    Errors = allErrors
                };
            }

            return ValidationResult.Success();
        }

        public void RegisterValidator(IValidator validator)
        {
            if (validator == null) throw new ArgumentNullException(nameof(validator));
            
            _validators.Add(validator);
        }

        public ValidationStatistics GetStatistics()
        {
            lock (_lock)
            {
                return new ValidationStatistics
                {
                    TotalValidations = _statistics.TotalValidations,
                    StaticFailures = _statistics.StaticFailures,
                    ContextualFailures = _statistics.ContextualFailures,
                    SemanticFailures = _statistics.SemanticFailures,
                    FailuresByCommand = new Dictionary<string, int>(_statistics.FailuresByCommand),
                    FailuresByValidator = new Dictionary<string, int>(_statistics.FailuresByValidator),
                    AverageValidationTime = _statistics.AverageValidationTime
                };
            }
        }

        private void IncrementCommandFailure(string commandType)
        {
            if (_statistics.FailuresByCommand.ContainsKey(commandType))
                _statistics.FailuresByCommand[commandType]++;
            else
                _statistics.FailuresByCommand[commandType] = 1;
        }

        private void IncrementValidatorFailure(string validatorName)
        {
            if (_statistics.FailuresByValidator.ContainsKey(validatorName))
                _statistics.FailuresByValidator[validatorName]++;
            else
                _statistics.FailuresByValidator[validatorName] = 1;
        }

        private void RegisterBuiltInValidators()
        {
            // Register core validators
            RegisterValidator(new DocumentValidator());
            RegisterValidator(new ParameterSchemaValidator());
            RegisterValidator(new WorksetValidator());
            RegisterValidator(new FLCStandardsValidator());
            RegisterValidator(new GeometryValidator());
            RegisterValidator(new BudgetValidator());
        }
    }

    /// <summary>
    /// Base validator class with common functionality
    /// </summary>
    public abstract class ValidatorBase : IValidator
    {
        public abstract ValidationPhase Phase { get; }
        public virtual string[] ApplicableCommandTypes => null; // Apply to all commands
        public virtual int Priority => 100;

        public abstract Task<ValidationResult> ValidateAsync(IAICommand command, CommandContext context);

        protected ValidationResult Success()
        {
            return ValidationResult.Success();
        }

        protected ValidationResult Failure(string message, string property = null, string code = null)
        {
            return ValidationResult.Failure(Phase, message, property);
        }

        protected ValidationResult Failures(params ValidationError[] errors)
        {
            return new ValidationResult
            {
                IsValid = false,
                FailedPhase = Phase,
                Errors = errors.ToList()
            };
        }
    }

    /// <summary>
    /// Validates that a document is available and not read-only
    /// </summary>
    public class DocumentValidator : ValidatorBase
    {
        public override ValidationPhase Phase => ValidationPhase.Contextual;
        public override int Priority => 1; // Run first

        public override Task<ValidationResult> ValidateAsync(IAICommand command, CommandContext context)
        {
            if (context.Document == null)
                return Task.FromResult(Failure("No active document available"));

            if (context.Document.IsReadOnly)
                return Task.FromResult(Failure("Document is read-only"));

            if (context.Document.IsModifiable == false)
                return Task.FromResult(Failure("Document is not modifiable"));

            return Task.FromResult(Success());
        }
    }

    /// <summary>
    /// Validates parameter schemas and types
    /// </summary>
    public class ParameterSchemaValidator : ValidatorBase
    {
        public override ValidationPhase Phase => ValidationPhase.Static;
        public override int Priority => 1;

        public override Task<ValidationResult> ValidateAsync(IAICommand command, CommandContext context)
        {
            var errors = new List<ValidationError>();

            // Validate required parameters exist
            foreach (var param in command.Parameters)
            {
                if (param.Value == null)
                {
                    errors.Add(new ValidationError
                    {
                        Phase = Phase,
                        Message = $"Parameter '{param.Key}' cannot be null",
                        Property = param.Key,
                        Code = "NULL_PARAMETER"
                    });
                }
            }

            if (errors.Any())
                return Task.FromResult(Failures(errors.ToArray()));

            return Task.FromResult(Success());
        }
    }

    /// <summary>
    /// Validates workset ownership and borrowing
    /// </summary>
    public class WorksetValidator : ValidatorBase
    {
        public override ValidationPhase Phase => ValidationPhase.Contextual;
        public override int Priority => 10;

        public override Task<ValidationResult> ValidateAsync(IAICommand command, CommandContext context)
        {
            // Skip if not a workshared document
            if (!context.Document.IsWorkshared)
                return Task.FromResult(Success());

            // TODO: Implement workset validation logic
            // - Check if user can borrow required worksets
            // - Validate element ownership
            // - Check for conflicts

            return Task.FromResult(Success());
        }
    }

    /// <summary>
    /// Validates FLC steel framing standards
    /// </summary>
    public class FLCStandardsValidator : ValidatorBase
    {
        public override ValidationPhase Phase => ValidationPhase.Semantic;
        public override string[] ApplicableCommandTypes => new[] { "CreateWall", "CreateFraming", "ModifyWall" };

        public override Task<ValidationResult> ValidateAsync(IAICommand command, CommandContext context)
        {
            // FLC-specific validation implemented in individual commands
            // This is a placeholder for global FLC rules
            return Task.FromResult(Success());
        }
    }

    /// <summary>
    /// Validates geometry constraints and limits
    /// </summary>
    public class GeometryValidator : ValidatorBase
    {
        public override ValidationPhase Phase => ValidationPhase.Semantic;

        public override Task<ValidationResult> ValidateAsync(IAICommand command, CommandContext context)
        {
            // TODO: Implement geometry validation
            // - Check for valid coordinates
            // - Validate dimensions are within reasonable limits
            // - Check for geometric conflicts

            return Task.FromResult(Success());
        }
    }

    /// <summary>
    /// Validates operation budgets and limits
    /// </summary>
    public class BudgetValidator : ValidatorBase
    {
        public override ValidationPhase Phase => ValidationPhase.Contextual;
        public override int Priority => 5;

        public override Task<ValidationResult> ValidateAsync(IAICommand command, CommandContext context)
        {
            if (command.EstimatedBudget > context.OperationBudget)
            {
                return Task.FromResult(Failure(
                    $"Command budget ({command.EstimatedBudget}) exceeds allowed budget ({context.OperationBudget})",
                    "Budget",
                    "BUDGET_EXCEEDED"));
            }

            return Task.FromResult(Success());
        }
    }
}
