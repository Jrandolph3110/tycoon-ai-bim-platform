using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.AIActions.Threading
{
    /// <summary>
    /// Task queue for marshaling AI commands to Revit's UI thread
    /// Implements safe multi-threaded operations as recommended by o3-pro
    /// </summary>
    public interface ITaskQueue
    {
        /// <summary>
        /// Queue a task for execution on the UI thread
        /// </summary>
        Task<T> EnqueueAsync<T>(Func<T> task, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queue a task for execution on the UI thread
        /// </summary>
        Task EnqueueAsync(Action task, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queue a task with priority
        /// </summary>
        Task<T> EnqueueAsync<T>(Func<T> task, TaskPriority priority, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get queue statistics
        /// </summary>
        TaskQueueStatistics GetStatistics();

        /// <summary>
        /// Start the task queue
        /// </summary>
        void Start();

        /// <summary>
        /// Stop the task queue
        /// </summary>
        void Stop();

        /// <summary>
        /// Pause task processing
        /// </summary>
        void Pause();

        /// <summary>
        /// Resume task processing
        /// </summary>
        void Resume();
    }

    /// <summary>
    /// Task priority levels
    /// </summary>
    public enum TaskPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Task queue statistics
    /// </summary>
    public class TaskQueueStatistics
    {
        public int QueuedTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int FailedTasks { get; set; }
        public TimeSpan AverageExecutionTime { get; set; }
        public DateTime? LastTaskTime { get; set; }
        public bool IsRunning { get; set; }
        public bool IsPaused { get; set; }
        public Dictionary<TaskPriority, int> TasksByPriority { get; set; } = new Dictionary<TaskPriority, int>();
    }

    /// <summary>
    /// Queued task wrapper
    /// </summary>
    internal class QueuedTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public Func<object> Task { get; set; }
        public TaskPriority Priority { get; set; }
        public DateTime QueueTime { get; set; } = DateTime.UtcNow;
        public CancellationToken CancellationToken { get; set; }
        public TaskCompletionSource<object> CompletionSource { get; set; }
        public Type ReturnType { get; set; }
    }

    /// <summary>
    /// Implementation of task queue using Revit's ExternalEvent system
    /// Ensures all Revit API calls happen on the UI thread
    /// </summary>
    public class RevitTaskQueue : ITaskQueue, IExternalEventHandler
    {
        private readonly Logger _logger;
        private readonly UIApplication _uiApplication;
        private readonly ExternalEvent _externalEvent;
        private readonly ConcurrentQueue<QueuedTask> _taskQueue;
        private readonly object _lock = new object();
        private readonly TaskQueueStatistics _statistics;

        private bool _isRunning = false;
        private bool _isPaused = false;
        private volatile bool _processingTask = false;

        public string GetName() => "Tycoon AI Actions Task Queue";

        public RevitTaskQueue(UIApplication uiApplication, Logger logger)
        {
            _uiApplication = uiApplication ?? throw new ArgumentNullException(nameof(uiApplication));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _taskQueue = new ConcurrentQueue<QueuedTask>();
            _statistics = new TaskQueueStatistics();
            
            // Create external event for UI thread marshaling
            _externalEvent = ExternalEvent.Create(this);
            
            _logger.Log("🔄 RevitTaskQueue initialized");
        }

        public async Task<T> EnqueueAsync<T>(Func<T> task, CancellationToken cancellationToken = default)
        {
            return await EnqueueAsync(task, TaskPriority.Normal, cancellationToken);
        }

        public async Task EnqueueAsync(Action task, CancellationToken cancellationToken = default)
        {
            await EnqueueAsync<object>(() => { task(); return null; }, TaskPriority.Normal, cancellationToken);
        }

        public async Task<T> EnqueueAsync<T>(Func<T> task, TaskPriority priority, CancellationToken cancellationToken = default)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));

            if (!_isRunning)
                throw new InvalidOperationException("Task queue is not running");

            var queuedTask = new QueuedTask
            {
                Task = () => task(),
                Priority = priority,
                CancellationToken = cancellationToken,
                CompletionSource = new TaskCompletionSource<object>(),
                ReturnType = typeof(T)
            };

            // Add to queue
            _taskQueue.Enqueue(queuedTask);

            lock (_lock)
            {
                _statistics.QueuedTasks++;
                if (_statistics.TasksByPriority.ContainsKey(priority))
                    _statistics.TasksByPriority[priority]++;
                else
                    _statistics.TasksByPriority[priority] = 1;
            }

            _logger.Log($"📋 Queued task {queuedTask.Id} with priority {priority}");

            // Trigger external event to process queue
            var eventResult = _externalEvent.Raise();
            if (eventResult != ExternalEventRequest.Accepted)
            {
                _logger.LogWarning($"External event not accepted: {eventResult}");
            }

            // Wait for completion
            var result = await queuedTask.CompletionSource.Task;
            
            if (result is T typedResult)
                return typedResult;
            
            return default(T);
        }

        public void Execute(UIApplication app)
        {
            if (_isPaused || _processingTask)
                return;

            _processingTask = true;

            try
            {
                // Process tasks by priority
                var tasksToProcess = new List<QueuedTask>();
                
                // Collect all tasks and sort by priority
                while (_taskQueue.TryDequeue(out var task))
                {
                    if (!task.CancellationToken.IsCancellationRequested)
                    {
                        tasksToProcess.Add(task);
                    }
                    else
                    {
                        task.CompletionSource.SetCanceled();
                    }
                }

                // Sort by priority (highest first)
                tasksToProcess.Sort((a, b) => b.Priority.CompareTo(a.Priority));

                // Execute tasks
                foreach (var task in tasksToProcess)
                {
                    if (task.CancellationToken.IsCancellationRequested)
                    {
                        task.CompletionSource.SetCanceled();
                        continue;
                    }

                    try
                    {
                        var startTime = DateTime.UtcNow;
                        var result = task.Task();
                        var executionTime = DateTime.UtcNow - startTime;

                        task.CompletionSource.SetResult(result);

                        lock (_lock)
                        {
                            _statistics.CompletedTasks++;
                            _statistics.LastTaskTime = DateTime.UtcNow;
                            
                            // Update average execution time
                            var totalTime = _statistics.AverageExecutionTime.TotalMilliseconds * (_statistics.CompletedTasks - 1);
                            totalTime += executionTime.TotalMilliseconds;
                            _statistics.AverageExecutionTime = TimeSpan.FromMilliseconds(totalTime / _statistics.CompletedTasks);
                        }

                        _logger.Log($"✅ Completed task {task.Id} in {executionTime.TotalMilliseconds:F0}ms");
                    }
                    catch (Exception ex)
                    {
                        task.CompletionSource.SetException(ex);
                        
                        lock (_lock)
                        {
                            _statistics.FailedTasks++;
                        }

                        _logger.LogError($"❌ Task {task.Id} failed", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error processing task queue", ex);
            }
            finally
            {
                _processingTask = false;
            }
        }

        public void Start()
        {
            lock (_lock)
            {
                _isRunning = true;
                _statistics.IsRunning = true;
            }
            
            _logger.Log("▶️ Task queue started");
        }

        public void Stop()
        {
            lock (_lock)
            {
                _isRunning = false;
                _statistics.IsRunning = false;
            }

            // Cancel all pending tasks
            while (_taskQueue.TryDequeue(out var task))
            {
                task.CompletionSource.SetCanceled();
            }

            _logger.Log("⏹️ Task queue stopped");
        }

        public void Pause()
        {
            lock (_lock)
            {
                _isPaused = true;
                _statistics.IsPaused = true;
            }
            
            _logger.Log("⏸️ Task queue paused");
        }

        public void Resume()
        {
            lock (_lock)
            {
                _isPaused = false;
                _statistics.IsPaused = false;
            }
            
            _logger.Log("▶️ Task queue resumed");
        }

        public TaskQueueStatistics GetStatistics()
        {
            lock (_lock)
            {
                return new TaskQueueStatistics
                {
                    QueuedTasks = _statistics.QueuedTasks,
                    CompletedTasks = _statistics.CompletedTasks,
                    FailedTasks = _statistics.FailedTasks,
                    AverageExecutionTime = _statistics.AverageExecutionTime,
                    LastTaskTime = _statistics.LastTaskTime,
                    IsRunning = _statistics.IsRunning,
                    IsPaused = _statistics.IsPaused,
                    TasksByPriority = new Dictionary<TaskPriority, int>(_statistics.TasksByPriority)
                };
            }
        }

        public void Dispose()
        {
            Stop();
            _externalEvent?.Dispose();
            _logger.Log("🗑️ Task queue disposed");
        }
    }

    /// <summary>
    /// Alternative implementation using UIApplication.Idling event
    /// For scenarios where ExternalEvent is not suitable
    /// </summary>
    public class IdlingTaskQueue : ITaskQueue
    {
        private readonly Logger _logger;
        private readonly UIApplication _uiApplication;
        private readonly ConcurrentQueue<QueuedTask> _taskQueue;
        private readonly TaskQueueStatistics _statistics;
        private readonly object _lock = new object();

        private bool _isRunning = false;
        private bool _isPaused = false;
        private bool _isSubscribed = false;

        public IdlingTaskQueue(UIApplication uiApplication, Logger logger)
        {
            _uiApplication = uiApplication ?? throw new ArgumentNullException(nameof(uiApplication));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _taskQueue = new ConcurrentQueue<QueuedTask>();
            _statistics = new TaskQueueStatistics();
            
            _logger.Log("🔄 IdlingTaskQueue initialized");
        }

        public async Task<T> EnqueueAsync<T>(Func<T> task, CancellationToken cancellationToken = default)
        {
            return await EnqueueAsync(task, TaskPriority.Normal, cancellationToken);
        }

        public async Task EnqueueAsync(Action task, CancellationToken cancellationToken = default)
        {
            await EnqueueAsync<object>(() => { task(); return null; }, TaskPriority.Normal, cancellationToken);
        }

        public async Task<T> EnqueueAsync<T>(Func<T> task, TaskPriority priority, CancellationToken cancellationToken = default)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));

            if (!_isRunning)
                throw new InvalidOperationException("Task queue is not running");

            var queuedTask = new QueuedTask
            {
                Task = () => task(),
                Priority = priority,
                CancellationToken = cancellationToken,
                CompletionSource = new TaskCompletionSource<object>(),
                ReturnType = typeof(T)
            };

            _taskQueue.Enqueue(queuedTask);

            lock (_lock)
            {
                _statistics.QueuedTasks++;
            }

            _logger.Log($"📋 Queued task {queuedTask.Id} for idling execution");

            var result = await queuedTask.CompletionSource.Task;
            
            if (result is T typedResult)
                return typedResult;
            
            return default(T);
        }

        private void OnIdling(object sender, Autodesk.Revit.UI.Events.IdlingEventArgs e)
        {
            if (_isPaused || !_taskQueue.TryDequeue(out var task))
                return;

            try
            {
                if (task.CancellationToken.IsCancellationRequested)
                {
                    task.CompletionSource.SetCanceled();
                    return;
                }

                var startTime = DateTime.UtcNow;
                var result = task.Task();
                var executionTime = DateTime.UtcNow - startTime;

                task.CompletionSource.SetResult(result);

                lock (_lock)
                {
                    _statistics.CompletedTasks++;
                    _statistics.LastTaskTime = DateTime.UtcNow;
                }

                _logger.Log($"✅ Completed idling task {task.Id} in {executionTime.TotalMilliseconds:F0}ms");
            }
            catch (Exception ex)
            {
                task.CompletionSource.SetException(ex);
                
                lock (_lock)
                {
                    _statistics.FailedTasks++;
                }

                _logger.LogError($"❌ Idling task {task.Id} failed", ex);
            }
        }

        public void Start()
        {
            lock (_lock)
            {
                _isRunning = true;
                _statistics.IsRunning = true;

                if (!_isSubscribed)
                {
                    _uiApplication.Idling += OnIdling;
                    _isSubscribed = true;
                }
            }
            
            _logger.Log("▶️ Idling task queue started");
        }

        public void Stop()
        {
            lock (_lock)
            {
                _isRunning = false;
                _statistics.IsRunning = false;

                if (_isSubscribed)
                {
                    _uiApplication.Idling -= OnIdling;
                    _isSubscribed = false;
                }
            }

            // Cancel all pending tasks
            while (_taskQueue.TryDequeue(out var task))
            {
                task.CompletionSource.SetCanceled();
            }

            _logger.Log("⏹️ Idling task queue stopped");
        }

        public void Pause()
        {
            lock (_lock)
            {
                _isPaused = true;
                _statistics.IsPaused = true;
            }
            
            _logger.Log("⏸️ Idling task queue paused");
        }

        public void Resume()
        {
            lock (_lock)
            {
                _isPaused = false;
                _statistics.IsPaused = false;
            }
            
            _logger.Log("▶️ Idling task queue resumed");
        }

        public TaskQueueStatistics GetStatistics()
        {
            lock (_lock)
            {
                return new TaskQueueStatistics
                {
                    QueuedTasks = _statistics.QueuedTasks,
                    CompletedTasks = _statistics.CompletedTasks,
                    FailedTasks = _statistics.FailedTasks,
                    AverageExecutionTime = _statistics.AverageExecutionTime,
                    LastTaskTime = _statistics.LastTaskTime,
                    IsRunning = _statistics.IsRunning,
                    IsPaused = _statistics.IsPaused,
                    TasksByPriority = new Dictionary<TaskPriority, int>(_statistics.TasksByPriority)
                };
            }
        }
    }
}
