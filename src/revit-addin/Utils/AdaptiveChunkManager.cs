using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TycoonRevitAddin.Utils
{
    /// <summary>
    /// Adaptive chunk manager with PID-style feedback loop for dynamic window sizing
    /// Implements recommendations for real-time throughput and memory pressure monitoring
    /// </summary>
    public class AdaptiveChunkManager
    {
        private readonly ILogger _logger;
        private readonly PerformanceCounter _memoryCounter;
        private readonly PerformanceCounter _cpuCounter;
        
        // PID Controller parameters
        private double _proportionalGain = 0.5;
        private double _integralGain = 0.1;
        private double _derivativeGain = 0.05;
        
        // State tracking
        private double _previousError = 0;
        private double _integralSum = 0;
        private int _currentChunkSize = 1000;
        private readonly object _lockObject = new object();
        
        // Performance metrics
        private readonly Queue<PerformanceMetric> _performanceHistory = new Queue<PerformanceMetric>();
        private const int MaxHistorySize = 20;
        
        // Adaptive boundaries
        private const int MinChunkSize = 100;
        private const int MaxChunkSize = 8000;
        private const double TargetMemoryUsagePercent = 70.0; // Target 70% memory usage
        private const double TargetThroughputElementsPerSecond = 5000.0;

        public AdaptiveChunkManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            try
            {
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _logger.Log("📊 Adaptive Chunk Manager initialized with performance monitoring");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize performance counters", ex);
                // Continue without performance counters if they fail
            }
        }

        /// <summary>
        /// Get optimal chunk size based on current system conditions
        /// </summary>
        public int GetOptimalChunkSize(int totalElements, TimeSpan? lastProcessingTime = null)
        {
            lock (_lockObject)
            {
                try
                {
                    var memoryPressure = GetMemoryPressure();
                    var cpuUsage = GetCpuUsage();
                    var throughput = CalculateThroughput(lastProcessingTime);
                    
                    // Calculate error from target throughput
                    var error = TargetThroughputElementsPerSecond - throughput;
                    
                    // PID Controller calculation
                    var proportional = _proportionalGain * error;
                    _integralSum += error;
                    var integral = _integralGain * _integralSum;
                    var derivative = _derivativeGain * (error - _previousError);
                    
                    var pidOutput = proportional + integral + derivative;
                    _previousError = error;
                    
                    // Adjust chunk size based on PID output and system conditions
                    var adjustment = (int)(pidOutput * 0.1); // Scale factor
                    
                    // Apply memory pressure constraints
                    if (memoryPressure > 80.0)
                    {
                        adjustment = Math.Min(adjustment, -100); // Reduce chunk size under memory pressure
                    }
                    else if (memoryPressure < 50.0 && cpuUsage < 70.0)
                    {
                        adjustment = Math.Max(adjustment, 50); // Increase chunk size when resources available
                    }
                    
                    _currentChunkSize = Math.Max(MinChunkSize, 
                                       Math.Min(MaxChunkSize, _currentChunkSize + adjustment));
                    
                    // Don't exceed total elements
                    var optimalSize = Math.Min(_currentChunkSize, totalElements);
                    
                    _logger.Log($"🎯 Optimal chunk size: {optimalSize:N0} " +
                               $"(Memory: {memoryPressure:F1}%, CPU: {cpuUsage:F1}%, " +
                               $"Throughput: {throughput:F0} elem/s)");
                    
                    return optimalSize;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error calculating optimal chunk size", ex);
                    return Math.Min(1000, totalElements); // Safe fallback
                }
            }
        }

        /// <summary>
        /// Record performance metrics for adaptive learning
        /// </summary>
        public void RecordPerformance(int elementsProcessed, TimeSpan processingTime, long memoryUsed)
        {
            lock (_lockObject)
            {
                var metric = new PerformanceMetric
                {
                    Timestamp = DateTime.UtcNow,
                    ElementsProcessed = elementsProcessed,
                    ProcessingTime = processingTime,
                    MemoryUsed = memoryUsed,
                    ChunkSize = _currentChunkSize,
                    Throughput = elementsProcessed / processingTime.TotalSeconds
                };
                
                _performanceHistory.Enqueue(metric);
                
                // Keep only recent history
                while (_performanceHistory.Count > MaxHistorySize)
                {
                    _performanceHistory.Dequeue();
                }
                
                _logger.Log($"📈 Performance recorded: {elementsProcessed:N0} elements in " +
                           $"{processingTime.TotalSeconds:F2}s ({metric.Throughput:F0} elem/s)");
            }
        }

        /// <summary>
        /// Get performance statistics for monitoring
        /// </summary>
        public PerformanceStats GetPerformanceStats()
        {
            lock (_lockObject)
            {
                if (_performanceHistory.Count == 0)
                {
                    return new PerformanceStats();
                }
                
                var metrics = _performanceHistory.ToArray();
                
                return new PerformanceStats
                {
                    AverageThroughput = metrics.Average(m => m.Throughput),
                    MaxThroughput = metrics.Max(m => m.Throughput),
                    MinThroughput = metrics.Min(m => m.Throughput),
                    AverageChunkSize = metrics.Average(m => m.ChunkSize),
                    CurrentChunkSize = _currentChunkSize,
                    TotalSamples = metrics.Length,
                    MemoryPressure = GetMemoryPressure(),
                    CpuUsage = GetCpuUsage()
                };
            }
        }

        /// <summary>
        /// Calculate chunks with adaptive sizing
        /// </summary>
        public IEnumerable<ChunkInfo> CalculateAdaptiveChunks<T>(IList<T> items)
        {
            var chunks = new List<ChunkInfo>();
            var totalElements = items.Count;
            var processedElements = 0;
            var chunkIndex = 0;
            
            while (processedElements < totalElements)
            {
                var remainingElements = totalElements - processedElements;
                var chunkSize = GetOptimalChunkSize(remainingElements);
                
                // Ensure we don't exceed remaining elements
                chunkSize = Math.Min(chunkSize, remainingElements);
                
                chunks.Add(new ChunkInfo
                {
                    Index = chunkIndex++,
                    StartIndex = processedElements,
                    Size = chunkSize,
                    TotalElements = totalElements,
                    IsLast = processedElements + chunkSize >= totalElements
                });
                
                processedElements += chunkSize;
            }
            
            _logger.Log($"📊 Calculated {chunks.Count} adaptive chunks for {totalElements:N0} elements");
            return chunks;
        }

        private double GetMemoryPressure()
        {
            try
            {
                if (_memoryCounter != null)
                {
                    var availableMemoryMB = _memoryCounter.NextValue();
                    var totalMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024) + availableMemoryMB;
                    var usedMemoryPercent = ((totalMemoryMB - availableMemoryMB) / totalMemoryMB) * 100;
                    return Math.Max(0, Math.Min(100, usedMemoryPercent));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting memory pressure", ex);
            }
            
            // Fallback: use GC memory pressure
            var gcMemory = GC.GetTotalMemory(false);
            return Math.Min(100, (gcMemory / (1024.0 * 1024 * 1024)) * 25); // Rough estimate
        }

        private double GetCpuUsage()
        {
            try
            {
                return _cpuCounter?.NextValue() ?? 0.0;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting CPU usage", ex);
                return 0.0;
            }
        }

        private double CalculateThroughput(TimeSpan? lastProcessingTime)
        {
            if (!lastProcessingTime.HasValue || lastProcessingTime.Value.TotalSeconds <= 0)
            {
                return TargetThroughputElementsPerSecond; // Default assumption
            }
            
            return _currentChunkSize / lastProcessingTime.Value.TotalSeconds;
        }
    }

    public class PerformanceMetric
    {
        public DateTime Timestamp { get; set; }
        public int ElementsProcessed { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public long MemoryUsed { get; set; }
        public int ChunkSize { get; set; }
        public double Throughput { get; set; }
    }

    public class PerformanceStats
    {
        public double AverageThroughput { get; set; }
        public double MaxThroughput { get; set; }
        public double MinThroughput { get; set; }
        public double AverageChunkSize { get; set; }
        public int CurrentChunkSize { get; set; }
        public int TotalSamples { get; set; }
        public double MemoryPressure { get; set; }
        public double CpuUsage { get; set; }
    }

    public class ChunkInfo
    {
        public int Index { get; set; }
        public int StartIndex { get; set; }
        public int Size { get; set; }
        public int TotalElements { get; set; }
        public bool IsLast { get; set; }
        
        public double ProgressPercent => (double)(StartIndex + Size) / TotalElements * 100;
    }
}
