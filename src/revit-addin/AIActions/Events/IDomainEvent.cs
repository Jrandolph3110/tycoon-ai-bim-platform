using System;
using System.Collections.Generic;

namespace TycoonRevitAddin.AIActions.Events
{
    /// <summary>
    /// Base interface for all domain events in the AI Actions system
    /// Provides immutable event sourcing for complete audit trail and undo/redo capabilities
    /// </summary>
    public interface IDomainEvent
    {
        /// <summary>
        /// Unique identifier for this event
        /// </summary>
        Guid EventId { get; }

        /// <summary>
        /// Type of event (ElementCreated, ParameterChanged, etc.)
        /// </summary>
        string EventType { get; }

        /// <summary>
        /// When the event occurred
        /// </summary>
        DateTime Timestamp { get; }

        /// <summary>
        /// ID of the AI command that generated this event
        /// </summary>
        Guid CommandId { get; }

        /// <summary>
        /// User who initiated the command (for audit trail)
        /// </summary>
        string UserId { get; }

        /// <summary>
        /// Session ID for grouping related events
        /// </summary>
        Guid SessionId { get; }

        /// <summary>
        /// Sequence number within the command (for ordering)
        /// </summary>
        int SequenceNumber { get; }

        /// <summary>
        /// Event payload data (specific to event type)
        /// </summary>
        Dictionary<string, object> Data { get; }

        /// <summary>
        /// Metadata for additional context
        /// </summary>
        Dictionary<string, string> Metadata { get; }

        /// <summary>
        /// Whether this event can be undone
        /// </summary>
        bool CanUndo { get; }

        /// <summary>
        /// Correlation ID for tracking related events across systems
        /// </summary>
        string CorrelationId { get; }
    }

    /// <summary>
    /// Base implementation of domain event with common properties
    /// </summary>
    public abstract class DomainEventBase : IDomainEvent
    {
        public Guid EventId { get; }
        public abstract string EventType { get; }
        public DateTime Timestamp { get; }
        public Guid CommandId { get; }
        public string UserId { get; }
        public Guid SessionId { get; }
        public int SequenceNumber { get; }
        public Dictionary<string, object> Data { get; }
        public Dictionary<string, string> Metadata { get; }
        public virtual bool CanUndo => true;
        public string CorrelationId { get; }

        protected DomainEventBase(
            Guid commandId,
            string userId,
            Guid sessionId,
            int sequenceNumber,
            Dictionary<string, object> data = null,
            Dictionary<string, string> metadata = null,
            string correlationId = null)
        {
            EventId = Guid.NewGuid();
            Timestamp = DateTime.UtcNow;
            CommandId = commandId;
            UserId = userId ?? Environment.UserName;
            SessionId = sessionId;
            SequenceNumber = sequenceNumber;
            Data = data ?? new Dictionary<string, object>();
            Metadata = metadata ?? new Dictionary<string, string>();
            CorrelationId = correlationId ?? Guid.NewGuid().ToString();

            // Add common metadata
            Metadata["MachineName"] = Environment.MachineName;
            Metadata["RevitVersion"] = GetRevitVersion();
            Metadata["TycoonVersion"] = GetTycoonVersion();
        }

        private string GetRevitVersion()
        {
            try
            {
                // Static access to Revit version
                return "2024"; // Placeholder - would need application context for actual version
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetTycoonVersion()
        {
            try
            {
                return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }
    }

    /// <summary>
    /// Event fired when an element is created by AI
    /// </summary>
    public class ElementCreatedEvent : DomainEventBase
    {
        public override string EventType => "ElementCreated";

        public ElementCreatedEvent(
            Guid commandId,
            string userId,
            Guid sessionId,
            int sequenceNumber,
            string elementId,
            string elementType,
            string categoryName,
            Dictionary<string, object> parameters = null,
            string correlationId = null)
            : base(commandId, userId, sessionId, sequenceNumber, 
                  CreateData(elementId, elementType, categoryName, parameters), 
                  null, correlationId)
        {
        }

        private static Dictionary<string, object> CreateData(
            string elementId, 
            string elementType, 
            string categoryName, 
            Dictionary<string, object> parameters)
        {
            var data = new Dictionary<string, object>
            {
                ["ElementId"] = elementId,
                ["ElementType"] = elementType,
                ["CategoryName"] = categoryName,
                ["Parameters"] = parameters ?? new Dictionary<string, object>()
            };
            return data;
        }
    }

    /// <summary>
    /// Event fired when element parameters are modified by AI
    /// </summary>
    public class ParameterChangedEvent : DomainEventBase
    {
        public override string EventType => "ParameterChanged";

        public ParameterChangedEvent(
            Guid commandId,
            string userId,
            Guid sessionId,
            int sequenceNumber,
            string elementId,
            string parameterName,
            object oldValue,
            object newValue,
            string correlationId = null)
            : base(commandId, userId, sessionId, sequenceNumber,
                  CreateData(elementId, parameterName, oldValue, newValue),
                  null, correlationId)
        {
        }

        private static Dictionary<string, object> CreateData(
            string elementId,
            string parameterName,
            object oldValue,
            object newValue)
        {
            return new Dictionary<string, object>
            {
                ["ElementId"] = elementId,
                ["ParameterName"] = parameterName,
                ["OldValue"] = oldValue,
                ["NewValue"] = newValue
            };
        }
    }

    /// <summary>
    /// Event fired when elements are deleted by AI
    /// </summary>
    public class ElementDeletedEvent : DomainEventBase
    {
        public override string EventType => "ElementDeleted";
        public override bool CanUndo => true; // Deletion can be undone

        public ElementDeletedEvent(
            Guid commandId,
            string userId,
            Guid sessionId,
            int sequenceNumber,
            string elementId,
            string elementType,
            Dictionary<string, object> elementSnapshot,
            string correlationId = null)
            : base(commandId, userId, sessionId, sequenceNumber,
                  CreateData(elementId, elementType, elementSnapshot),
                  null, correlationId)
        {
        }

        private static Dictionary<string, object> CreateData(
            string elementId,
            string elementType,
            Dictionary<string, object> elementSnapshot)
        {
            return new Dictionary<string, object>
            {
                ["ElementId"] = elementId,
                ["ElementType"] = elementType,
                ["ElementSnapshot"] = elementSnapshot // For potential restoration
            };
        }
    }

    /// <summary>
    /// Event fired when a transaction is started by AI
    /// </summary>
    public class TransactionStartedEvent : DomainEventBase
    {
        public override string EventType => "TransactionStarted";
        public override bool CanUndo => false; // Transaction events are not undoable

        public TransactionStartedEvent(
            Guid commandId,
            string userId,
            Guid sessionId,
            int sequenceNumber,
            string transactionName,
            string correlationId = null)
            : base(commandId, userId, sessionId, sequenceNumber,
                  new Dictionary<string, object> { ["TransactionName"] = transactionName },
                  null, correlationId)
        {
        }
    }

    /// <summary>
    /// Event fired when a transaction is committed by AI
    /// </summary>
    public class TransactionCommittedEvent : DomainEventBase
    {
        public override string EventType => "TransactionCommitted";
        public override bool CanUndo => false;

        public TransactionCommittedEvent(
            Guid commandId,
            string userId,
            Guid sessionId,
            int sequenceNumber,
            string transactionName,
            int elementsAffected,
            string correlationId = null)
            : base(commandId, userId, sessionId, sequenceNumber,
                  CreateData(transactionName, elementsAffected),
                  null, correlationId)
        {
        }

        private static Dictionary<string, object> CreateData(string transactionName, int elementsAffected)
        {
            return new Dictionary<string, object>
            {
                ["TransactionName"] = transactionName,
                ["ElementsAffected"] = elementsAffected
            };
        }
    }

    /// <summary>
    /// Event fired when a transaction is rolled back by AI
    /// </summary>
    public class TransactionRolledBackEvent : DomainEventBase
    {
        public override string EventType => "TransactionRolledBack";
        public override bool CanUndo => false;

        public TransactionRolledBackEvent(
            Guid commandId,
            string userId,
            Guid sessionId,
            int sequenceNumber,
            string transactionName,
            string reason,
            string correlationId = null)
            : base(commandId, userId, sessionId, sequenceNumber,
                  CreateData(transactionName, reason),
                  null, correlationId)
        {
        }

        private static Dictionary<string, object> CreateData(string transactionName, string reason)
        {
            return new Dictionary<string, object>
            {
                ["TransactionName"] = transactionName,
                ["Reason"] = reason
            };
        }
    }
}
