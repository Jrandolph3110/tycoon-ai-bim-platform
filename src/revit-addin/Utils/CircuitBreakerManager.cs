using System;
using System.Threading;
using System.Threading.Tasks;

namespace TycoonRevitAddin.Utils
{
    /// <summary>
    /// Circuit breaker pattern implementation for resilient error handling
    /// Prevents rapid reconnect storms and provides exponential back-off
    /// </summary>
    public class CircuitBreakerManager
    {
        private readonly ILogger _logger;
        private readonly object _lockObject = new object();
        
        // Circuit breaker state
        private CircuitBreakerState _state = CircuitBreakerState.Closed;
        private int _failureCount = 0;
        private DateTime _lastFailureTime = DateTime.MinValue;
        private DateTime _nextAttemptTime = DateTime.MinValue;
        
        // Configuration
        private readonly int _failureThreshold;
        private readonly TimeSpan _timeout;
        private readonly TimeSpan _baseDelay;
        private readonly int _maxRetries;
        private readonly double _backoffMultiplier;

        public CircuitBreakerManager(
            ILogger logger,
            int failureThreshold = 5,
            TimeSpan? timeout = null,
            TimeSpan? baseDelay = null,
            int maxRetries = 10,
            double backoffMultiplier = 2.0)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _failureThreshold = failureThreshold;
            _timeout = timeout ?? TimeSpan.FromMinutes(1);
            _baseDelay = baseDelay ?? TimeSpan.FromSeconds(1);
            _maxRetries = maxRetries;
            _backoffMultiplier = backoffMultiplier;
            
            _logger.Log($"🛡️ Circuit Breaker initialized (Threshold: {_failureThreshold}, Timeout: {_timeout})");
        }

        /// <summary>
        /// Execute an operation through the circuit breaker
        /// </summary>
        public async Task<T> ExecuteAsync<T>(
            Func<Task<T>> operation,
            string operationName = "Operation",
            CancellationToken cancellationToken = default)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            lock (_lockObject)
            {
                // Check if circuit is open and if we should attempt
                if (_state == CircuitBreakerState.Open)
                {
                    if (DateTime.UtcNow < _nextAttemptTime)
                    {
                        var waitTime = _nextAttemptTime - DateTime.UtcNow;
                        _logger.Log($"🚫 Circuit breaker OPEN for {operationName} - " +
                                   $"Next attempt in {waitTime.TotalSeconds:F0}s");
                        throw new CircuitBreakerOpenException(
                            $"Circuit breaker is open for {operationName}. Next attempt at {_nextAttemptTime:HH:mm:ss}");
                    }
                    
                    // Time to try half-open
                    _state = CircuitBreakerState.HalfOpen;
                    _logger.Log($"🔄 Circuit breaker HALF-OPEN for {operationName} - Testing connection");
                }
            }

            try
            {
                var result = await operation();
                
                // Success - reset circuit breaker
                lock (_lockObject)
                {
                    if (_state == CircuitBreakerState.HalfOpen || _failureCount > 0)
                    {
                        _logger.Log($"✅ Circuit breaker CLOSED for {operationName} - Connection restored");
                    }
                    
                    _state = CircuitBreakerState.Closed;
                    _failureCount = 0;
                    _lastFailureTime = DateTime.MinValue;
                    _nextAttemptTime = DateTime.MinValue;
                }
                
                return result;
            }
            catch (Exception ex)
            {
                lock (_lockObject)
                {
                    _failureCount++;
                    _lastFailureTime = DateTime.UtcNow;
                    
                    _logger.LogError($"Circuit breaker failure #{_failureCount} for {operationName}", ex);
                    
                    if (_failureCount >= _failureThreshold)
                    {
                        _state = CircuitBreakerState.Open;
                        _nextAttemptTime = CalculateNextAttemptTime();
                        
                        _logger.Log($"⚠️ Circuit breaker OPENED for {operationName} - " +
                                   $"Too many failures ({_failureCount}). Next attempt: {_nextAttemptTime:HH:mm:ss}");
                    }
                }
                
                throw;
            }
        }

        /// <summary>
        /// Execute with automatic retry and exponential backoff
        /// </summary>
        public async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            string operationName = "Operation",
            CancellationToken cancellationToken = default)
        {
            var attempt = 0;
            Exception lastException = null;
            
            while (attempt < _maxRetries)
            {
                try
                {
                    return await ExecuteAsync(operation, operationName, cancellationToken);
                }
                catch (CircuitBreakerOpenException)
                {
                    // Circuit is open, don't retry immediately
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attempt++;
                    
                    if (attempt >= _maxRetries)
                    {
                        _logger.LogError($"Max retries ({_maxRetries}) exceeded for {operationName}", ex);
                        break;
                    }
                    
                    var delay = CalculateRetryDelay(attempt);
                    _logger.Log($"🔄 Retry {attempt}/{_maxRetries} for {operationName} in {delay.TotalSeconds:F1}s");
                    
                    try
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Log($"Retry cancelled for {operationName}");
                        throw;
                    }
                }
            }
            
            throw lastException ?? new InvalidOperationException($"Failed to execute {operationName} after {_maxRetries} attempts");
        }

        /// <summary>
        /// Get current circuit breaker status
        /// </summary>
        public CircuitBreakerStatus GetStatus()
        {
            lock (_lockObject)
            {
                return new CircuitBreakerStatus
                {
                    State = _state,
                    FailureCount = _failureCount,
                    LastFailureTime = _lastFailureTime,
                    NextAttemptTime = _nextAttemptTime,
                    IsHealthy = _state == CircuitBreakerState.Closed && _failureCount == 0
                };
            }
        }

        /// <summary>
        /// Manually reset the circuit breaker
        /// </summary>
        public void Reset(string reason = "Manual reset")
        {
            lock (_lockObject)
            {
                var previousState = _state;
                _state = CircuitBreakerState.Closed;
                _failureCount = 0;
                _lastFailureTime = DateTime.MinValue;
                _nextAttemptTime = DateTime.MinValue;
                
                _logger.Log($"🔄 Circuit breaker RESET - {reason} (was {previousState})");
            }
        }

        /// <summary>
        /// Manually trip the circuit breaker
        /// </summary>
        public void Trip(string reason = "Manual trip")
        {
            lock (_lockObject)
            {
                _state = CircuitBreakerState.Open;
                _failureCount = _failureThreshold;
                _lastFailureTime = DateTime.UtcNow;
                _nextAttemptTime = CalculateNextAttemptTime();
                
                _logger.Log($"⚠️ Circuit breaker TRIPPED - {reason}. Next attempt: {_nextAttemptTime:HH:mm:ss}");
            }
        }

        private DateTime CalculateNextAttemptTime()
        {
            // Exponential backoff with jitter
            var backoffFactor = Math.Min(Math.Pow(_backoffMultiplier, _failureCount - _failureThreshold), 64);
            var delay = TimeSpan.FromTicks((long)(_baseDelay.Ticks * backoffFactor));
            
            // Add jitter (±25%)
            var jitter = new Random().NextDouble() * 0.5 - 0.25; // -25% to +25%
            delay = TimeSpan.FromTicks((long)(delay.Ticks * (1 + jitter)));
            
            // Cap at maximum timeout
            delay = delay > _timeout ? _timeout : delay;
            
            return DateTime.UtcNow.Add(delay);
        }

        private TimeSpan CalculateRetryDelay(int attempt)
        {
            var delay = TimeSpan.FromTicks((long)(_baseDelay.Ticks * Math.Pow(_backoffMultiplier, attempt - 1)));
            
            // Add jitter
            var jitter = new Random().NextDouble() * 0.3; // 0% to 30%
            delay = TimeSpan.FromTicks((long)(delay.Ticks * (1 + jitter)));
            
            // Cap at reasonable maximum
            var maxDelay = TimeSpan.FromMinutes(5);
            return delay > maxDelay ? maxDelay : delay;
        }
    }

    public enum CircuitBreakerState
    {
        Closed,   // Normal operation
        Open,     // Failing, blocking requests
        HalfOpen  // Testing if service is back
    }

    public class CircuitBreakerStatus
    {
        public CircuitBreakerState State { get; set; }
        public int FailureCount { get; set; }
        public DateTime LastFailureTime { get; set; }
        public DateTime NextAttemptTime { get; set; }
        public bool IsHealthy { get; set; }
    }

    public class CircuitBreakerOpenException : Exception
    {
        public CircuitBreakerOpenException(string message) : base(message) { }
        public CircuitBreakerOpenException(string message, Exception innerException) : base(message, innerException) { }
    }
}
