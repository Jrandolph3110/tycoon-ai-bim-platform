using System;
using System.Diagnostics;
using Microsoft.VisualBasic.Devices;

namespace TycoonRevitAddin.Utils
{
    /// <summary>
    /// Dynamic memory-based chunk size optimizer for FAFB processing
    /// Monitors memory usage and adjusts chunk sizes in real-time for maximum performance
    /// </summary>
    public class DynamicMemoryOptimizer
    {
        private readonly Logger _logger;
        private int _currentChunkSize;
        private int _chunksProcessed;
        private long _initialMemory;
        private long _lastMemoryCheck;
        private readonly int _memoryCheckInterval;

        // Enhanced monitoring
        private long _totalSystemMemory;
        private long _peakMemoryUsage;
        private int _gcCollectionCount;
        private DateTime _lastGcTime;
        
        // Memory thresholds in bytes (will be adjusted based on system memory)
        private long _targetMemoryUsage;
        private long _warningThreshold;
        private long _maxMemoryThreshold;
        
        // Enhanced chunk size limits for maximum performance
        private readonly int _minChunkSize = 250;
        private readonly int _maxChunkSize = 5000; // Increased for high-end systems
        private readonly int _initialChunkSize = 1000; // Start with larger chunks

        public DynamicMemoryOptimizer(Logger logger, int memoryCheckInterval = 5)
        {
            _logger = logger;
            _memoryCheckInterval = memoryCheckInterval;
            _currentChunkSize = _initialChunkSize;
            _chunksProcessed = 0;
            _initialMemory = GetCurrentMemoryUsage();
            _lastMemoryCheck = _initialMemory;
            _peakMemoryUsage = _initialMemory;
            _gcCollectionCount = GC.CollectionCount(0);
            _lastGcTime = DateTime.UtcNow;

            // Get total system memory
            try
            {
                var computerInfo = new ComputerInfo();
                _totalSystemMemory = (long)computerInfo.TotalPhysicalMemory;
            }
            catch
            {
                _totalSystemMemory = 16L * 1024 * 1024 * 1024; // Default to 16GB if detection fails
            }

            // Calculate adaptive memory thresholds based on system memory
            CalculateAdaptiveThresholds();

            _logger.Log($"🧠 Dynamic Memory Optimizer initialized: Start={_initialChunkSize}, Target={_targetMemoryUsage / (1024*1024*1024)}GB, Warning={_warningThreshold / (1024*1024*1024)}GB, Max={_maxMemoryThreshold / (1024*1024*1024)}GB, System={_totalSystemMemory / (1024*1024*1024)}GB");
        }

        /// <summary>
        /// Get the current optimal chunk size
        /// </summary>
        public int GetOptimalChunkSize()
        {
            return _currentChunkSize;
        }

        /// <summary>
        /// Update chunk size based on current memory usage
        /// Call this every few chunks to optimize performance
        /// </summary>
        public void UpdateChunkSize()
        {
            _chunksProcessed++;
            
            // Check memory every N chunks
            if (_chunksProcessed % _memoryCheckInterval == 0)
            {
                long currentMemory = GetCurrentMemoryUsage();
                long memoryDelta = currentMemory - _initialMemory;
                long memoryGrowth = currentMemory - _lastMemoryCheck;

                // Track peak memory usage
                if (currentMemory > _peakMemoryUsage)
                {
                    _peakMemoryUsage = currentMemory;
                }

                // Monitor GC pressure
                int currentGcCount = GC.CollectionCount(0);
                int gcDelta = currentGcCount - _gcCollectionCount;
                var timeSinceLastGc = DateTime.UtcNow - _lastGcTime;

                // Calculate memory utilization percentage
                double memoryUtilization = (double)currentMemory / _totalSystemMemory * 100;

                _logger.Log($"🧠 Memory Check #{_chunksProcessed / _memoryCheckInterval}: Current={currentMemory / (1024*1024)}MB ({memoryUtilization:F1}%), Delta={memoryDelta / (1024*1024)}MB, Growth={memoryGrowth / (1024*1024)}MB, Peak={_peakMemoryUsage / (1024*1024)}MB, GC={gcDelta}");
                
                int oldChunkSize = _currentChunkSize;
                
                // Adjust chunk size based on memory usage
                if (currentMemory > _maxMemoryThreshold)
                {
                    // Emergency: Reduce chunk size significantly
                    _currentChunkSize = Math.Max(_minChunkSize, (int)(_currentChunkSize * 0.5));
                    _logger.Log($"🚨 EMERGENCY: Memory > {_maxMemoryThreshold / (1024*1024*1024)}GB! Reducing chunk size: {oldChunkSize} → {_currentChunkSize}");
                }
                else if (currentMemory > _warningThreshold)
                {
                    // Warning: Reduce chunk size moderately
                    _currentChunkSize = Math.Max(_minChunkSize, (int)(_currentChunkSize * 0.75));
                    _logger.Log($"⚠️ WARNING: Memory > {_warningThreshold / (1024*1024*1024)}GB! Reducing chunk size: {oldChunkSize} → {_currentChunkSize}");
                }
                else if (currentMemory < _targetMemoryUsage && memoryGrowth < 200 * 1024 * 1024) // Less than 200MB growth
                {
                    // Opportunity: Increase chunk size for better performance
                    double scaleFactor = 1.5; // More aggressive scaling

                    // Ultra-aggressive scaling for high-end systems
                    if (_totalSystemMemory > 60L * 1024 * 1024 * 1024) // 60GB+ systems
                    {
                        scaleFactor = 2.0; // Double chunk size for ultra high-end
                    }

                    _currentChunkSize = Math.Min(_maxChunkSize, (int)(_currentChunkSize * scaleFactor));
                    _logger.Log($"🚀 ULTRA-OPTIMIZE: Memory < {_targetMemoryUsage / (1024*1024*1024)}GB & stable! Scaling chunk size: {oldChunkSize} → {_currentChunkSize} (x{scaleFactor})");
                }
                else
                {
                    _logger.Log($"✅ STABLE: Memory usage optimal, maintaining chunk size: {_currentChunkSize}");
                }
                
                _lastMemoryCheck = currentMemory;
                _gcCollectionCount = currentGcCount;

                // Force garbage collection if memory is getting high
                if (currentMemory > _warningThreshold)
                {
                    _logger.Log("🗑️ Forcing garbage collection due to high memory usage");
                    _lastGcTime = DateTime.UtcNow;
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
            }
        }

        /// <summary>
        /// Get current memory usage in bytes
        /// </summary>
        private long GetCurrentMemoryUsage()
        {
            try
            {
                using (var process = Process.GetCurrentProcess())
                {
                    return process.WorkingSet64;
                }
            }
            catch
            {
                // Fallback to GC memory if process memory fails
                return GC.GetTotalMemory(false);
            }
        }

        /// <summary>
        /// Get memory statistics for logging
        /// </summary>
        public string GetMemoryStats()
        {
            long currentMemory = GetCurrentMemoryUsage();
            long memoryDelta = currentMemory - _initialMemory;
            
            return $"Memory: {currentMemory / (1024*1024)}MB (Δ{memoryDelta / (1024*1024)}MB), Chunk: {_currentChunkSize}, Processed: {_chunksProcessed}";
        }

        /// <summary>
        /// Reset optimizer for new processing session
        /// </summary>
        public void Reset()
        {
            _currentChunkSize = _initialChunkSize;
            _chunksProcessed = 0;
            _initialMemory = GetCurrentMemoryUsage();
            _lastMemoryCheck = _initialMemory;
            
            _logger.Log($"🔄 Memory Optimizer reset: Chunk={_currentChunkSize}, Memory={_initialMemory / (1024*1024)}MB");
        }

        /// <summary>
        /// Calculate adaptive memory thresholds based on system memory
        /// </summary>
        private void CalculateAdaptiveThresholds()
        {
            long systemMemoryGB = _totalSystemMemory / (1024 * 1024 * 1024);

            if (systemMemoryGB >= 64)
            {
                // Ultra high-end system: Use maximum performance thresholds
                _targetMemoryUsage = 8L * 1024 * 1024 * 1024;  // 8GB target
                _warningThreshold = 16L * 1024 * 1024 * 1024;  // 16GB warning
                _maxMemoryThreshold = 24L * 1024 * 1024 * 1024; // 24GB max
                _logger.Log($"🚀 ULTRA HIGH-END SYSTEM: Using maximum performance thresholds for {systemMemoryGB}GB system");
            }
            else if (systemMemoryGB >= 32)
            {
                // High-end system: Use aggressive thresholds
                _targetMemoryUsage = 6L * 1024 * 1024 * 1024;  // 6GB target
                _warningThreshold = 12L * 1024 * 1024 * 1024;  // 12GB warning
                _maxMemoryThreshold = 18L * 1024 * 1024 * 1024; // 18GB max
                _logger.Log($"🚀 HIGH-END SYSTEM: Using aggressive thresholds for {systemMemoryGB}GB system");
            }
            else if (systemMemoryGB >= 16)
            {
                // Standard system: Use balanced thresholds
                _targetMemoryUsage = 3L * 1024 * 1024 * 1024;  // 3GB target
                _warningThreshold = 6L * 1024 * 1024 * 1024;   // 6GB warning
                _maxMemoryThreshold = 8L * 1024 * 1024 * 1024;  // 8GB max
                _logger.Log($"⚖️ STANDARD SYSTEM: Using balanced thresholds for {systemMemoryGB}GB system");
            }
            else
            {
                // Low-memory system: Use conservative thresholds
                _targetMemoryUsage = 2L * 1024 * 1024 * 1024;  // 2GB target
                _warningThreshold = 4L * 1024 * 1024 * 1024;   // 4GB warning
                _maxMemoryThreshold = 6L * 1024 * 1024 * 1024;  // 6GB max
                _logger.Log($"🛡️ LOW-MEMORY SYSTEM: Using conservative thresholds for {systemMemoryGB}GB system");
            }
        }
    }
}
