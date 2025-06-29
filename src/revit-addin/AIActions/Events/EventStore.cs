using System;
using System.Collections.Generic;
using System.Linq;

namespace TycoonRevitAddin.AIActions.Events
{
    /// <summary>
    /// 📝 Event Store for AI Actions
    /// Tracks all AI operations for audit trail and analytics
    /// </summary>
    public class EventStore
    {
        private readonly List<AIActionEvent> _events;
        private readonly object _lock = new object();

        public EventStore()
        {
            _events = new List<AIActionEvent>();
        }

        /// <summary>
        /// Add a new AI action event
        /// </summary>
        public void AddEvent(AIActionEvent actionEvent)
        {
            lock (_lock)
            {
                actionEvent.Id = Guid.NewGuid().ToString();
                actionEvent.Timestamp = DateTime.UtcNow;
                _events.Add(actionEvent);
            }
        }

        /// <summary>
        /// Get all events
        /// </summary>
        public List<AIActionEvent> GetAllEvents()
        {
            lock (_lock)
            {
                return _events.ToList();
            }
        }

        /// <summary>
        /// Get events by action type
        /// </summary>
        public List<AIActionEvent> GetEventsByType(string actionType)
        {
            lock (_lock)
            {
                return _events.Where(e => e.ActionType == actionType).ToList();
            }
        }

        /// <summary>
        /// Get events for specific elements
        /// </summary>
        public List<AIActionEvent> GetEventsForElements(List<int> elementIds)
        {
            lock (_lock)
            {
                return _events.Where(e => e.ElementIds != null && 
                    e.ElementIds.Any(id => elementIds.Contains(id))).ToList();
            }
        }

        /// <summary>
        /// Get recent events
        /// </summary>
        public List<AIActionEvent> GetRecentEvents(int count = 10)
        {
            lock (_lock)
            {
                return _events.OrderByDescending(e => e.Timestamp).Take(count).ToList();
            }
        }

        /// <summary>
        /// Clear all events
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _events.Clear();
            }
        }
    }

    /// <summary>
    /// 🎯 AI Action Event
    /// Represents a single AI action performed on elements
    /// </summary>
    public class AIActionEvent
    {
        public string Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string ActionType { get; set; }
        public List<int> ElementIds { get; set; }
        public string Status { get; set; }
        public string Details { get; set; }
        public string UserContext { get; set; }
        public Dictionary<string, object> Metadata { get; set; }

        public AIActionEvent()
        {
            ElementIds = new List<int>();
            Metadata = new Dictionary<string, object>();
        }
    }
}
