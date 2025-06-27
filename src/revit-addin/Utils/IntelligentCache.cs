/**
 * Intelligent Caching System for BIM Elements
 * Provides smart caching for repeated elements, families, and types
 * Massive performance gains for common BIM components
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using TycoonRevitAddin.Communication;

namespace TycoonRevitAddin.Utils
{
    public class IntelligentCache
    {
        private readonly ConcurrentDictionary<string, CachedElement> _elementCache;
        private readonly ConcurrentDictionary<string, CachedFamily> _familyCache;
        private readonly ConcurrentDictionary<string, CachedType> _typeCache;
        private readonly ConcurrentDictionary<string, DateTime> _accessTimes;
        private readonly ILogger _logger;
        
        // Cache statistics
        private long _cacheHits = 0;
        private long _cacheMisses = 0;
        private long _totalRequests = 0;
        
        // Cache limits
        private readonly int _maxElementCacheSize = 10000;
        private readonly int _maxFamilyCacheSize = 1000;
        private readonly int _maxTypeCacheSize = 2000;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(24);

        public IntelligentCache(ILogger logger)
        {
            _logger = logger;
            _elementCache = new ConcurrentDictionary<string, CachedElement>();
            _familyCache = new ConcurrentDictionary<string, CachedFamily>();
            _typeCache = new ConcurrentDictionary<string, CachedType>();
            _accessTimes = new ConcurrentDictionary<string, DateTime>();
            
            _logger.Log("🧠 Intelligent Cache System initialized");
        }

        /// <summary>
        /// Get cached element data or process if not cached
        /// </summary>
        public RevitElementData GetOrProcessElement(Element element, Func<Element, RevitElementData> processor)
        {
            _totalRequests++;
            
            var cacheKey = GenerateElementCacheKey(element);
            
            // Check cache first
            if (_elementCache.TryGetValue(cacheKey, out var cachedElement))
            {
                if (DateTime.UtcNow - cachedElement.CachedAt < _cacheExpiry)
                {
                    _cacheHits++;
                    _accessTimes[cacheKey] = DateTime.UtcNow;
                    
                    if (_totalRequests % 100 == 0)
                    {
                        LogCacheStats();
                    }
                    
                    return cachedElement.Data;
                }
                else
                {
                    // Expired - remove from cache
                    _elementCache.TryRemove(cacheKey, out _);
                    _accessTimes.TryRemove(cacheKey, out _);
                }
            }
            
            // Cache miss - process element
            _cacheMisses++;
            var elementData = processor(element);
            
            // Cache the result
            var cached = new CachedElement
            {
                Data = elementData,
                CachedAt = DateTime.UtcNow,
                ElementId = element.Id.IntegerValue,
                Hash = cacheKey
            };
            
            // Add to cache with size management
            if (_elementCache.Count >= _maxElementCacheSize)
            {
                CleanupElementCache();
            }
            
            _elementCache[cacheKey] = cached;
            _accessTimes[cacheKey] = DateTime.UtcNow;
            
            if (_totalRequests % 100 == 0)
            {
                LogCacheStats();
            }
            
            return elementData;
        }

        /// <summary>
        /// Get cached family data
        /// </summary>
        public CachedFamily GetOrProcessFamily(Family family, Func<Family, CachedFamily> processor)
        {
            var cacheKey = GenerateFamilyCacheKey(family);
            
            if (_familyCache.TryGetValue(cacheKey, out var cachedFamily))
            {
                if (DateTime.UtcNow - cachedFamily.CachedAt < _cacheExpiry)
                {
                    _accessTimes[cacheKey] = DateTime.UtcNow;
                    return cachedFamily;
                }
                else
                {
                    _familyCache.TryRemove(cacheKey, out _);
                    _accessTimes.TryRemove(cacheKey, out _);
                }
            }
            
            // Process and cache
            var familyData = processor(family);
            familyData.CachedAt = DateTime.UtcNow;
            
            if (_familyCache.Count >= _maxFamilyCacheSize)
            {
                CleanupFamilyCache();
            }
            
            _familyCache[cacheKey] = familyData;
            _accessTimes[cacheKey] = DateTime.UtcNow;
            
            return familyData;
        }

        /// <summary>
        /// Get cached type data
        /// </summary>
        public CachedType GetOrProcessType(ElementType elementType, Func<ElementType, CachedType> processor)
        {
            var cacheKey = GenerateTypeCacheKey(elementType);
            
            if (_typeCache.TryGetValue(cacheKey, out var cachedType))
            {
                if (DateTime.UtcNow - cachedType.CachedAt < _cacheExpiry)
                {
                    _accessTimes[cacheKey] = DateTime.UtcNow;
                    return cachedType;
                }
                else
                {
                    _typeCache.TryRemove(cacheKey, out _);
                    _accessTimes.TryRemove(cacheKey, out _);
                }
            }
            
            // Process and cache
            var typeData = processor(elementType);
            typeData.CachedAt = DateTime.UtcNow;
            
            if (_typeCache.Count >= _maxTypeCacheSize)
            {
                CleanupTypeCache();
            }
            
            _typeCache[cacheKey] = typeData;
            _accessTimes[cacheKey] = DateTime.UtcNow;
            
            return typeData;
        }

        /// <summary>
        /// Generate cache key for element based on its properties
        /// </summary>
        private string GenerateElementCacheKey(Element element)
        {
            var keyBuilder = new StringBuilder();
            keyBuilder.Append(element.Id.IntegerValue);
            keyBuilder.Append("|");
            keyBuilder.Append(element.Category?.Name ?? "NoCategory");
            keyBuilder.Append("|");
            
            // Include key parameters that affect serialization
            if (element is FamilyInstance fi)
            {
                keyBuilder.Append(fi.Symbol?.Name ?? "NoSymbol");
                keyBuilder.Append("|");
                keyBuilder.Append(fi.Symbol?.Family?.Name ?? "NoFamily");
            }
            else if (element.GetTypeId() != ElementId.InvalidElementId)
            {
                var type = element.Document.GetElement(element.GetTypeId());
                keyBuilder.Append(type?.Name ?? "NoType");
            }
            
            // Hash the key for consistent length
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyBuilder.ToString()));
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Generate cache key for family
        /// </summary>
        private string GenerateFamilyCacheKey(Family family)
        {
            var keyBuilder = new StringBuilder();
            keyBuilder.Append(family.Name);
            keyBuilder.Append("|");
            keyBuilder.Append(family.FamilyCategory?.Name ?? "NoCategory");
            
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyBuilder.ToString()));
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Generate cache key for element type
        /// </summary>
        private string GenerateTypeCacheKey(ElementType elementType)
        {
            var keyBuilder = new StringBuilder();
            keyBuilder.Append(elementType.Name);
            keyBuilder.Append("|");
            keyBuilder.Append(elementType.Category?.Name ?? "NoCategory");
            keyBuilder.Append("|");
            keyBuilder.Append(elementType.FamilyName ?? "NoFamily");
            
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyBuilder.ToString()));
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Cleanup element cache using LRU strategy
        /// </summary>
        private void CleanupElementCache()
        {
            var itemsToRemove = _maxElementCacheSize / 4; // Remove 25%
            var oldestItems = _accessTimes
                .Where(kvp => _elementCache.ContainsKey(kvp.Key))
                .OrderBy(kvp => kvp.Value)
                .Take(itemsToRemove)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in oldestItems)
            {
                _elementCache.TryRemove(key, out _);
                _accessTimes.TryRemove(key, out _);
            }
            
            _logger.Log($"🧹 Cleaned up {oldestItems.Count} old element cache entries");
        }

        /// <summary>
        /// Cleanup family cache using LRU strategy
        /// </summary>
        private void CleanupFamilyCache()
        {
            var itemsToRemove = _maxFamilyCacheSize / 4;
            var oldestItems = _accessTimes
                .Where(kvp => _familyCache.ContainsKey(kvp.Key))
                .OrderBy(kvp => kvp.Value)
                .Take(itemsToRemove)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in oldestItems)
            {
                _familyCache.TryRemove(key, out _);
                _accessTimes.TryRemove(key, out _);
            }
            
            _logger.Log($"🧹 Cleaned up {oldestItems.Count} old family cache entries");
        }

        /// <summary>
        /// Cleanup type cache using LRU strategy
        /// </summary>
        private void CleanupTypeCache()
        {
            var itemsToRemove = _maxTypeCacheSize / 4;
            var oldestItems = _accessTimes
                .Where(kvp => _typeCache.ContainsKey(kvp.Key))
                .OrderBy(kvp => kvp.Value)
                .Take(itemsToRemove)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in oldestItems)
            {
                _typeCache.TryRemove(key, out _);
                _accessTimes.TryRemove(key, out _);
            }
            
            _logger.Log($"🧹 Cleaned up {oldestItems.Count} old type cache entries");
        }

        /// <summary>
        /// Log cache statistics
        /// </summary>
        private void LogCacheStats()
        {
            var hitRate = _totalRequests > 0 ? (_cacheHits * 100.0 / _totalRequests) : 0;
            _logger.Log($"🧠 Cache Stats: {hitRate:F1}% hit rate ({_cacheHits}/{_totalRequests}) | Elements: {_elementCache.Count}, Families: {_familyCache.Count}, Types: {_typeCache.Count}");
        }

        /// <summary>
        /// Get comprehensive cache statistics
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            var hitRate = _totalRequests > 0 ? (_cacheHits * 100.0 / _totalRequests) : 0;
            
            return new CacheStatistics
            {
                TotalRequests = _totalRequests,
                CacheHits = _cacheHits,
                CacheMisses = _cacheMisses,
                HitRate = hitRate,
                ElementCacheSize = _elementCache.Count,
                FamilyCacheSize = _familyCache.Count,
                TypeCacheSize = _typeCache.Count,
                MemoryEstimateMB = EstimateMemoryUsage()
            };
        }

        /// <summary>
        /// Estimate memory usage of cache
        /// </summary>
        private double EstimateMemoryUsage()
        {
            // Rough estimate: 1KB per cached element
            var totalItems = _elementCache.Count + _familyCache.Count + _typeCache.Count;
            return totalItems * 1.0 / 1024; // MB
        }

        /// <summary>
        /// Clear all caches
        /// </summary>
        public void ClearAll()
        {
            _elementCache.Clear();
            _familyCache.Clear();
            _typeCache.Clear();
            _accessTimes.Clear();
            
            _cacheHits = 0;
            _cacheMisses = 0;
            _totalRequests = 0;
            
            _logger.Log("🧹 All caches cleared");
        }
    }

    public class CachedElement
    {
        public RevitElementData Data { get; set; }
        public DateTime CachedAt { get; set; }
        public int ElementId { get; set; }
        public string Hash { get; set; }
    }

    public class CachedFamily
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public Dictionary<string, object> Properties { get; set; }
        public DateTime CachedAt { get; set; }
    }

    public class CachedType
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string FamilyName { get; set; }
        public Dictionary<string, object> Properties { get; set; }
        public DateTime CachedAt { get; set; }
    }

    public class CacheStatistics
    {
        public long TotalRequests { get; set; }
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }
        public double HitRate { get; set; }
        public int ElementCacheSize { get; set; }
        public int FamilyCacheSize { get; set; }
        public int TypeCacheSize { get; set; }
        public double MemoryEstimateMB { get; set; }
    }
}
