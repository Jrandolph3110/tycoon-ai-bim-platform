using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.AIActions.Events
{
    /// <summary>
    /// Interface for event store that persists domain events
    /// Provides complete audit trail and enables event replay for undo/redo
    /// </summary>
    public interface IEventStore
    {
        /// <summary>
        /// Append a single event to the store
        /// </summary>
        Task AppendEventAsync(IDomainEvent domainEvent);

        /// <summary>
        /// Append multiple events atomically
        /// </summary>
        Task AppendEventsAsync(IEnumerable<IDomainEvent> events);

        /// <summary>
        /// Get all events for a specific command
        /// </summary>
        Task<IEnumerable<IDomainEvent>> GetEventsByCommandAsync(Guid commandId);

        /// <summary>
        /// Get all events for a session
        /// </summary>
        Task<IEnumerable<IDomainEvent>> GetEventsBySessionAsync(Guid sessionId);

        /// <summary>
        /// Get events by correlation ID (for tracking across systems)
        /// </summary>
        Task<IEnumerable<IDomainEvent>> GetEventsByCorrelationAsync(string correlationId);

        /// <summary>
        /// Get events within a time range
        /// </summary>
        Task<IEnumerable<IDomainEvent>> GetEventsByTimeRangeAsync(DateTime start, DateTime end);

        /// <summary>
        /// Get events by type
        /// </summary>
        Task<IEnumerable<IDomainEvent>> GetEventsByTypeAsync(string eventType);

        /// <summary>
        /// Get all undoable events for a command (in reverse order for undo)
        /// </summary>
        Task<IEnumerable<IDomainEvent>> GetUndoableEventsAsync(Guid commandId);

        /// <summary>
        /// Create a snapshot of current state for rollback
        /// </summary>
        Task<string> CreateSnapshotAsync(string description);

        /// <summary>
        /// Get available snapshots
        /// </summary>
        Task<IEnumerable<EventSnapshot>> GetSnapshotsAsync();

        /// <summary>
        /// Clear events older than specified date (for cleanup)
        /// </summary>
        Task CleanupEventsAsync(DateTime olderThan);
    }

    /// <summary>
    /// Event snapshot for rollback points
    /// </summary>
    public class EventSnapshot
    {
        public string Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
        public int EventCount { get; set; }
        public string UserId { get; set; }
    }

    /// <summary>
    /// In-memory event store implementation with ExtensibleStorage persistence
    /// Optimized for Revit's single-threaded nature
    /// </summary>
    public class RevitEventStore : IEventStore
    {
        private readonly Logger _logger;
        private readonly List<IDomainEvent> _events;
        private readonly List<EventSnapshot> _snapshots;
        private readonly object _lock = new object();

        public RevitEventStore(Logger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _events = new List<IDomainEvent>();
            _snapshots = new List<EventSnapshot>();
        }

        public Task AppendEventAsync(IDomainEvent domainEvent)
        {
            if (domainEvent == null) throw new ArgumentNullException(nameof(domainEvent));

            lock (_lock)
            {
                _events.Add(domainEvent);
                _logger.Log($"📝 Event stored: {domainEvent.EventType} (ID: {domainEvent.EventId})");
            }

            // TODO: Persist to ExtensibleStorage
            return Task.CompletedTask;
        }

        public Task AppendEventsAsync(IEnumerable<IDomainEvent> events)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));

            var eventList = events.ToList();
            if (!eventList.Any()) return Task.CompletedTask;

            lock (_lock)
            {
                _events.AddRange(eventList);
                _logger.Log($"📝 Batch stored: {eventList.Count} events");
            }

            // TODO: Persist to ExtensibleStorage
            return Task.CompletedTask;
        }

        public Task<IEnumerable<IDomainEvent>> GetEventsByCommandAsync(Guid commandId)
        {
            lock (_lock)
            {
                var result = _events
                    .Where(e => e.CommandId == commandId)
                    .OrderBy(e => e.SequenceNumber)
                    .ToList();

                return Task.FromResult<IEnumerable<IDomainEvent>>(result);
            }
        }

        public Task<IEnumerable<IDomainEvent>> GetEventsBySessionAsync(Guid sessionId)
        {
            lock (_lock)
            {
                var result = _events
                    .Where(e => e.SessionId == sessionId)
                    .OrderBy(e => e.Timestamp)
                    .ThenBy(e => e.SequenceNumber)
                    .ToList();

                return Task.FromResult<IEnumerable<IDomainEvent>>(result);
            }
        }

        public Task<IEnumerable<IDomainEvent>> GetEventsByCorrelationAsync(string correlationId)
        {
            if (string.IsNullOrEmpty(correlationId)) 
                return Task.FromResult<IEnumerable<IDomainEvent>>(new List<IDomainEvent>());

            lock (_lock)
            {
                var result = _events
                    .Where(e => e.CorrelationId == correlationId)
                    .OrderBy(e => e.Timestamp)
                    .ToList();

                return Task.FromResult<IEnumerable<IDomainEvent>>(result);
            }
        }

        public Task<IEnumerable<IDomainEvent>> GetEventsByTimeRangeAsync(DateTime start, DateTime end)
        {
            lock (_lock)
            {
                var result = _events
                    .Where(e => e.Timestamp >= start && e.Timestamp <= end)
                    .OrderBy(e => e.Timestamp)
                    .ToList();

                return Task.FromResult<IEnumerable<IDomainEvent>>(result);
            }
        }

        public Task<IEnumerable<IDomainEvent>> GetEventsByTypeAsync(string eventType)
        {
            if (string.IsNullOrEmpty(eventType))
                return Task.FromResult<IEnumerable<IDomainEvent>>(new List<IDomainEvent>());

            lock (_lock)
            {
                var result = _events
                    .Where(e => e.EventType == eventType)
                    .OrderBy(e => e.Timestamp)
                    .ToList();

                return Task.FromResult<IEnumerable<IDomainEvent>>(result);
            }
        }

        public Task<IEnumerable<IDomainEvent>> GetUndoableEventsAsync(Guid commandId)
        {
            lock (_lock)
            {
                var result = _events
                    .Where(e => e.CommandId == commandId && e.CanUndo)
                    .OrderByDescending(e => e.SequenceNumber) // Reverse order for undo
                    .ToList();

                return Task.FromResult<IEnumerable<IDomainEvent>>(result);
            }
        }

        public Task<string> CreateSnapshotAsync(string description)
        {
            var snapshot = new EventSnapshot
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Description = description ?? "AI Actions Snapshot",
                EventCount = _events.Count,
                UserId = Environment.UserName
            };

            lock (_lock)
            {
                _snapshots.Add(snapshot);
                _logger.Log($"📸 Snapshot created: {snapshot.Description} ({snapshot.EventCount} events)");
            }

            return Task.FromResult(snapshot.Id);
        }

        public Task<IEnumerable<EventSnapshot>> GetSnapshotsAsync()
        {
            lock (_lock)
            {
                var result = _snapshots
                    .OrderByDescending(s => s.Timestamp)
                    .ToList();

                return Task.FromResult<IEnumerable<EventSnapshot>>(result);
            }
        }

        public Task CleanupEventsAsync(DateTime olderThan)
        {
            lock (_lock)
            {
                var countBefore = _events.Count;
                _events.RemoveAll(e => e.Timestamp < olderThan);
                var countAfter = _events.Count;

                var removed = countBefore - countAfter;
                if (removed > 0)
                {
                    _logger.Log($"🧹 Cleaned up {removed} old events (older than {olderThan:yyyy-MM-dd})");
                }

                // Also cleanup old snapshots
                var snapshotsBefore = _snapshots.Count;
                _snapshots.RemoveAll(s => s.Timestamp < olderThan);
                var snapshotsAfter = _snapshots.Count;

                var snapshotsRemoved = snapshotsBefore - snapshotsAfter;
                if (snapshotsRemoved > 0)
                {
                    _logger.Log($"🧹 Cleaned up {snapshotsRemoved} old snapshots");
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Get event statistics for monitoring
        /// </summary>
        public EventStoreStatistics GetStatistics()
        {
            lock (_lock)
            {
                var stats = new EventStoreStatistics
                {
                    TotalEvents = _events.Count,
                    TotalSnapshots = _snapshots.Count,
                    EventsByType = _events.GroupBy(e => e.EventType)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    OldestEvent = _events.Any() ? _events.Min(e => e.Timestamp) : (DateTime?)null,
                    NewestEvent = _events.Any() ? _events.Max(e => e.Timestamp) : (DateTime?)null,
                    UniqueCommands = _events.Select(e => e.CommandId).Distinct().Count(),
                    UniqueSessions = _events.Select(e => e.SessionId).Distinct().Count()
                };

                return stats;
            }
        }
    }

    /// <summary>
    /// Event store statistics for monitoring and diagnostics
    /// </summary>
    public class EventStoreStatistics
    {
        public int TotalEvents { get; set; }
        public int TotalSnapshots { get; set; }
        public Dictionary<string, int> EventsByType { get; set; }
        public DateTime? OldestEvent { get; set; }
        public DateTime? NewestEvent { get; set; }
        public int UniqueCommands { get; set; }
        public int UniqueSessions { get; set; }
    }
}
