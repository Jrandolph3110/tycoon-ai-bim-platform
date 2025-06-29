using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using TycoonRevitAddin.AIActions.Events;

namespace TycoonRevitAddin.AIActions.Commands
{
    /// <summary>
    /// AI command to create walls with FLC standards
    /// Demonstrates the event-sourced command pattern with full validation
    /// </summary>
    public class CreateWallCommand : AICommandBase
    {
        public override string CommandType => "CreateWall";
        public override string Description => "Create a wall with specified parameters using FLC standards";
        public override int EstimatedBudget => 1; // Creates one wall
        public override TimeSpan MaxExecutionTime => TimeSpan.FromMinutes(1);

        public CreateWallCommand(IEventStore eventStore) : base(eventStore)
        {
        }

        /// <summary>
        /// Initialize command with parameters
        /// </summary>
        public void Initialize(
            XYZ startPoint,
            XYZ endPoint,
            double height,
            string wallTypeName = "FLC_6_Int_DW-FB",
            Level level = null)
        {
            Parameters["StartPoint"] = new double[] { startPoint.X, startPoint.Y, startPoint.Z };
            Parameters["EndPoint"] = new double[] { endPoint.X, endPoint.Y, endPoint.Z };
            Parameters["Height"] = height;
            Parameters["WallTypeName"] = wallTypeName;
            Parameters["LevelId"] = level?.Id?.ToString();
        }

        protected override async Task<ValidationResult> ValidateStaticAsync(CommandContext context)
        {
            // Phase 1: Static validation (schema/type checks)
            var errors = new List<ValidationError>();

            if (!Parameters.ContainsKey("StartPoint"))
                errors.Add(new ValidationError 
                { 
                    Phase = ValidationPhase.Static, 
                    Message = "StartPoint is required", 
                    Property = "StartPoint" 
                });

            if (!Parameters.ContainsKey("EndPoint"))
                errors.Add(new ValidationError 
                { 
                    Phase = ValidationPhase.Static, 
                    Message = "EndPoint is required", 
                    Property = "EndPoint" 
                });

            if (!Parameters.ContainsKey("Height"))
                errors.Add(new ValidationError 
                { 
                    Phase = ValidationPhase.Static, 
                    Message = "Height is required", 
                    Property = "Height" 
                });

            // Validate point arrays
            if (Parameters.ContainsKey("StartPoint"))
            {
                var startArray = Parameters["StartPoint"] as double[];
                if (startArray == null || startArray.Length != 3)
                    errors.Add(new ValidationError 
                    { 
                        Phase = ValidationPhase.Static, 
                        Message = "StartPoint must be array of 3 doubles [X,Y,Z]", 
                        Property = "StartPoint" 
                    });
            }

            if (Parameters.ContainsKey("EndPoint"))
            {
                var endArray = Parameters["EndPoint"] as double[];
                if (endArray == null || endArray.Length != 3)
                    errors.Add(new ValidationError 
                    { 
                        Phase = ValidationPhase.Static, 
                        Message = "EndPoint must be array of 3 doubles [X,Y,Z]", 
                        Property = "EndPoint" 
                    });
            }

            // Validate height
            if (Parameters.ContainsKey("Height"))
            {
                if (!(Parameters["Height"] is double height) || height <= 0)
                    errors.Add(new ValidationError 
                    { 
                        Phase = ValidationPhase.Static, 
                        Message = "Height must be positive number", 
                        Property = "Height" 
                    });
            }

            if (errors.Any())
            {
                return new ValidationResult 
                { 
                    IsValid = false, 
                    FailedPhase = ValidationPhase.Static, 
                    Errors = errors 
                };
            }

            return ValidationResult.Success();
        }

        protected override async Task<ValidationResult> ValidateContextualAsync(CommandContext context)
        {
            var baseResult = await base.ValidateContextualAsync(context);
            if (!baseResult.IsValid) return baseResult;

            var errors = new List<ValidationError>();

            // Check if wall type exists
            var wallTypeName = Parameters.ContainsKey("WallTypeName") ? Parameters["WallTypeName"] as string : "FLC_6_Int_DW-FB";
            var wallType = new FilteredElementCollector(context.Document)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(wt => wt.Name == wallTypeName);

            if (wallType == null)
            {
                errors.Add(new ValidationError 
                { 
                    Phase = ValidationPhase.Contextual, 
                    Message = $"Wall type '{wallTypeName}' not found in document", 
                    Property = "WallTypeName" 
                });
            }

            // Check if level exists (if specified)
            if (Parameters.ContainsKey("LevelId") && Parameters["LevelId"] != null)
            {
                var levelIdStr = Parameters["LevelId"] as string;
                if (ElementId.TryParse(levelIdStr, out var levelId))
                {
                    var level = context.Document.GetElement(levelId) as Level;
                    if (level == null)
                    {
                        errors.Add(new ValidationError 
                        { 
                            Phase = ValidationPhase.Contextual, 
                            Message = $"Level with ID '{levelIdStr}' not found", 
                            Property = "LevelId" 
                        });
                    }
                }
            }

            // Check points are different
            var startArray = Parameters["StartPoint"] as double[];
            var endArray = Parameters["EndPoint"] as double[];
            if (startArray != null && endArray != null)
            {
                var start = new XYZ(startArray[0], startArray[1], startArray[2]);
                var end = new XYZ(endArray[0], endArray[1], endArray[2]);
                
                if (start.DistanceTo(end) < 0.1) // Less than 0.1 feet
                {
                    errors.Add(new ValidationError 
                    { 
                        Phase = ValidationPhase.Contextual, 
                        Message = "Start and end points are too close (minimum 0.1 feet)", 
                        Property = "Points" 
                    });
                }
            }

            if (errors.Any())
            {
                return new ValidationResult 
                { 
                    IsValid = false, 
                    FailedPhase = ValidationPhase.Contextual, 
                    Errors = errors 
                };
            }

            return ValidationResult.Success();
        }

        protected override async Task<ValidationResult> ValidateSemanticAsync(CommandContext context)
        {
            var errors = new List<ValidationError>();

            // FLC-specific validation rules
            var height = (double)Parameters["Height"];
            
            // Check FLC standard heights (8', 9', 10', 12')
            var standardHeights = new[] { 8.0, 9.0, 10.0, 12.0 };
            if (!standardHeights.Any(h => Math.Abs(h - height) < 0.1))
            {
                errors.Add(new ValidationError 
                { 
                    Phase = ValidationPhase.Semantic, 
                    Message = $"Height {height}' is not FLC standard (8', 9', 10', or 12')", 
                    Property = "Height",
                    Code = "FLC_HEIGHT_STANDARD" 
                });
            }

            // Check wall length for FLC panel standards
            var startArray = Parameters["StartPoint"] as double[];
            var endArray = Parameters["EndPoint"] as double[];
            var start = new XYZ(startArray[0], startArray[1], startArray[2]);
            var end = new XYZ(endArray[0], endArray[1], endArray[2]);
            var length = start.DistanceTo(end);

            if (length > 40.0) // 40 feet max panel length
            {
                errors.Add(new ValidationError 
                { 
                    Phase = ValidationPhase.Semantic, 
                    Message = $"Wall length {length:F1}' exceeds FLC maximum panel length (40')", 
                    Property = "Length",
                    Code = "FLC_PANEL_LENGTH" 
                });
            }

            if (errors.Any())
            {
                return new ValidationResult 
                { 
                    IsValid = false, 
                    FailedPhase = ValidationPhase.Semantic, 
                    Errors = errors 
                };
            }

            return ValidationResult.Success();
        }

        public override async Task<CommandResult> PreviewAsync(CommandContext context)
        {
            var result = new CommandResult();
            
            try
            {
                // Validation first
                var validation = await ValidateAsync(context);
                if (!validation.IsValid)
                {
                    result.Success = false;
                    result.Message = $"Validation failed at {validation.FailedPhase} phase: " + 
                                   string.Join(", ", validation.Errors.Select(e => e.Message));
                    return result;
                }

                // Create preview data
                var startArray = Parameters["StartPoint"] as double[];
                var endArray = Parameters["EndPoint"] as double[];
                var height = (double)Parameters["Height"];
                var wallTypeName = Parameters.ContainsKey("WallTypeName") ? Parameters["WallTypeName"] as string : "FLC_6_Int_DW-FB";

                var start = new XYZ(startArray[0], startArray[1], startArray[2]);
                var end = new XYZ(endArray[0], endArray[1], endArray[2]);
                var length = start.DistanceTo(end);

                result.Data["WallLength"] = length;
                result.Data["WallHeight"] = height;
                result.Data["WallType"] = wallTypeName;
                result.Data["StartPoint"] = $"({start.X:F2}, {start.Y:F2}, {start.Z:F2})";
                result.Data["EndPoint"] = $"({end.X:F2}, {end.Y:F2}, {end.Z:F2})";
                result.Data["Area"] = length * height;

                result.Success = true;
                result.Message = $"Will create {wallTypeName} wall: {length:F1}' long × {height:F1}' high";
                result.ElementsAffected = 1;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Preview failed: {ex.Message}";
                result.Exception = ex;
            }

            return result;
        }

        public override async Task<CommandResult> ExecuteAsync(CommandContext context)
        {
            var result = new CommandResult();
            var startTime = DateTime.UtcNow;

            try
            {
                // Validation first
                var validation = await ValidateAsync(context);
                if (!validation.IsValid)
                {
                    result.Success = false;
                    result.Message = $"Validation failed: " + 
                                   string.Join(", ", validation.Errors.Select(e => e.Message));
                    return result;
                }

                // Extract parameters
                var startArray = Parameters["StartPoint"] as double[];
                var endArray = Parameters["EndPoint"] as double[];
                var height = (double)Parameters["Height"];
                var wallTypeName = Parameters.ContainsKey("WallTypeName") ? Parameters["WallTypeName"] as string : "FLC_6_Int_DW-FB";

                var start = new XYZ(startArray[0], startArray[1], startArray[2]);
                var end = new XYZ(endArray[0], endArray[1], endArray[2]);

                // Get wall type
                var wallType = new FilteredElementCollector(context.Document)
                    .OfClass(typeof(WallType))
                    .Cast<WallType>()
                    .First(wt => wt.Name == wallTypeName);

                // Get level (use active view's level if not specified)
                Level level = null;
                if (Parameters.ContainsKey("LevelId") && Parameters["LevelId"] != null)
                {
                    var levelIdStr = Parameters["LevelId"] as string;
                    if (ElementId.TryParse(levelIdStr, out var levelId))
                    {
                        level = context.Document.GetElement(levelId) as Level;
                    }
                }
                level ??= context.Document.ActiveView.GenLevel;

                // Emit transaction started event
                var transactionEvent = CreateEvent<TransactionStartedEvent>(seq => 
                    new TransactionStartedEvent(
                        context.CommandId, context.UserId, context.SessionId, seq, 
                        "Create Wall", context.CorrelationId));
                await EmitEventAsync(transactionEvent);

                Wall wall = null;
                using (var transaction = new Transaction(context.Document, "AI: Create Wall"))
                {
                    transaction.Start();

                    try
                    {
                        // Create the wall
                        var line = Line.CreateBound(start, end);
                        wall = Wall.Create(context.Document, line, wallType.Id, level.Id, height, 0, false, false);

                        // Emit element created event
                        var createdEvent = CreateEvent<ElementCreatedEvent>(seq =>
                            new ElementCreatedEvent(
                                context.CommandId, context.UserId, context.SessionId, seq,
                                wall.Id.ToString(), wall.GetType().Name, wall.Category.Name,
                                new Dictionary<string, object>
                                {
                                    ["WallType"] = wallTypeName,
                                    ["Height"] = height,
                                    ["Length"] = start.DistanceTo(end),
                                    ["Level"] = level.Name
                                },
                                context.CorrelationId));
                        await EmitEventAsync(createdEvent);

                        transaction.Commit();

                        // Emit transaction committed event
                        var committedEvent = CreateEvent<TransactionCommittedEvent>(seq =>
                            new TransactionCommittedEvent(
                                context.CommandId, context.UserId, context.SessionId, seq,
                                "Create Wall", 1, context.CorrelationId));
                        await EmitEventAsync(committedEvent);
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();

                        // Emit rollback event
                        var rollbackEvent = CreateEvent<TransactionRolledBackEvent>(seq =>
                            new TransactionRolledBackEvent(
                                context.CommandId, context.UserId, context.SessionId, seq,
                                "Create Wall", ex.Message, context.CorrelationId));
                        await EmitEventAsync(rollbackEvent);

                        throw;
                    }
                }

                result.Success = true;
                result.Message = $"Wall created successfully (ID: {wall.Id})";
                result.ElementsAffected = 1;
                result.Data["WallId"] = wall.Id.ToString();
                result.Data["WallType"] = wallTypeName;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Failed to create wall: {ex.Message}";
                result.Exception = ex;
            }
            finally
            {
                result.ExecutionTime = DateTime.UtcNow - startTime;
            }

            return result;
        }

        protected override async Task UndoEventAsync(CommandContext context, IDomainEvent domainEvent)
        {
            if (domainEvent is ElementCreatedEvent createdEvent)
            {
                // Delete the created wall
                if (ElementId.TryParse(createdEvent.Data["ElementId"].ToString(), out var elementId))
                {
                    var element = context.Document.GetElement(elementId);
                    if (element != null)
                    {
                        using (var transaction = new Transaction(context.Document, "AI: Undo Create Wall"))
                        {
                            transaction.Start();
                            context.Document.Delete(elementId);
                            transaction.Commit();
                        }

                        // Emit deletion event
                        var deletedEvent = CreateEvent<ElementDeletedEvent>(seq =>
                            new ElementDeletedEvent(
                                context.CommandId, context.UserId, context.SessionId, seq,
                                elementId.ToString(), element.GetType().Name,
                                new Dictionary<string, object> { ["Reason"] = "Undo" },
                                context.CorrelationId));
                        await EmitEventAsync(deletedEvent);
                    }
                }
            }
        }
    }
}
