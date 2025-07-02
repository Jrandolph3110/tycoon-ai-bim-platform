using System;
using System.Collections.Generic;
using System.Linq;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.Events
{
    /// <summary>
    /// Simple event bus for Layout Manager → ScriptsPlugin communication
    /// Part of Chat's event-driven architecture solution
    /// </summary>
    public class EventBus
    {
        private static EventBus _instance;
        private static readonly object _lock = new object();
        
        private readonly Dictionary<Type, List<object>> _subscribers = new Dictionary<Type, List<object>>();
        private readonly Logger _logger = new Logger("EventBus");
        
        public static EventBus Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new EventBus();
                    }
                }
                return _instance;
            }
        }
        
        private EventBus() { }
        
        /// <summary>
        /// Subscribe to events of type T
        /// </summary>
        public void Subscribe<T>(Action<T> handler)
        {
            var eventType = typeof(T);
            
            if (!_subscribers.ContainsKey(eventType))
            {
                _subscribers[eventType] = new List<object>();
            }
            
            _subscribers[eventType].Add(handler);
            _logger.Log($"📡 Subscribed to {eventType.Name} events");
        }
        
        /// <summary>
        /// Publish event to all subscribers
        /// </summary>
        public void Publish<T>(T eventData)
        {
            var eventType = typeof(T);
            
            if (!_subscribers.ContainsKey(eventType))
            {
                _logger.Log($"📡 No subscribers for {eventType.Name} event");
                return;
            }
            
            var handlers = _subscribers[eventType].Cast<Action<T>>().ToList();
            _logger.Log($"📡 Publishing {eventType.Name} event to {handlers.Count} subscribers");
            
            foreach (var handler in handlers)
            {
                try
                {
                    handler(eventData);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in event handler for {eventType.Name}", ex);
                }
            }
        }
        
        /// <summary>
        /// Unsubscribe from events (for cleanup)
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler)
        {
            var eventType = typeof(T);
            
            if (_subscribers.ContainsKey(eventType))
            {
                _subscribers[eventType].Remove(handler);
                _logger.Log($"📡 Unsubscribed from {eventType.Name} events");
            }
        }
    }
}
