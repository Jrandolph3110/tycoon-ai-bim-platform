using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.AIActions.Data
{
    /// <summary>
    /// 📁 Project Data Layer (Chat's Fast-Moving Layer)
    /// Short TTL, project-specific learnings and patterns
    /// </summary>
    public class ProjectDataLayer
    {
        private readonly ILogger _logger;
        private readonly string _dataPath;
        private readonly Dictionary<string, AIKnowledgeItem> _cache;

        public ProjectDataLayer(ILogger logger)
        {
            _logger = logger;
            _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                                   "Tycoon", "AIData", "Project");
            _cache = new Dictionary<string, AIKnowledgeItem>();
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                Directory.CreateDirectory(_dataPath);
                await LoadCacheAsync();
                _logger.Log("📁 Project Data Layer initialized");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize Project Data Layer", ex);
                return false;
            }
        }

        public async Task<List<AIKnowledgeItem>> QueryAsync(AIKnowledgeQuery query)
        {
            var results = new List<AIKnowledgeItem>();
            
            foreach (var item in _cache.Values)
            {
                if (MatchesQuery(item, query))
                {
                    item.LastUsed = DateTime.UtcNow;
                    item.UsageCount++;
                    results.Add(item);
                }
            }

            return await Task.FromResult(results);
        }

        public async Task<bool> StoreLearningAsync(AILearningData learning)
        {
            try
            {
                var knowledgeItem = new AIKnowledgeItem
                {
                    Category = learning.Category,
                    Key = $"{learning.LearningType}_{learning.Context}",
                    Value = learning.Data,
                    Source = AIDataLayer.Project,
                    Confidence = learning.SuccessScore,
                    Metadata = learning.Metrics
                };

                _cache[knowledgeItem.Id] = knowledgeItem;
                await SaveCacheAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to store project learning", ex);
                return false;
            }
        }

        public async Task<AILayerStats> GetStatsAsync()
        {
            return await Task.FromResult(new AILayerStats
            {
                TotalKnowledgeItems = _cache.Count,
                AverageConfidence = CalculateAverageConfidence(),
                LastUpdated = DateTime.UtcNow
            });
        }

        private async Task LoadCacheAsync()
        {
            var cacheFile = Path.Combine(_dataPath, "project_cache.json");
            if (File.Exists(cacheFile))
            {
                var json = await File.ReadAllTextAsync(cacheFile);
                var items = JsonConvert.DeserializeObject<List<AIKnowledgeItem>>(json);
                
                foreach (var item in items)
                {
                    _cache[item.Id] = item;
                }
            }
        }

        private async Task SaveCacheAsync()
        {
            var cacheFile = Path.Combine(_dataPath, "project_cache.json");
            var json = JsonConvert.SerializeObject(_cache.Values, Formatting.Indented);
            await File.WriteAllTextAsync(cacheFile, json);
        }

        private bool MatchesQuery(AIKnowledgeItem item, AIKnowledgeQuery query)
        {
            if (!string.IsNullOrEmpty(query.Category) && item.Category != query.Category)
                return false;

            if (!string.IsNullOrEmpty(query.SearchText))
            {
                var searchText = query.SearchText.ToLower();
                if (!item.Key.ToLower().Contains(searchText) && 
                    !item.Value?.ToString().ToLower().Contains(searchText) == true)
                    return false;
            }

            return true;
        }

        private double CalculateAverageConfidence()
        {
            if (_cache.Count == 0) return 0.0;
            
            double total = 0.0;
            foreach (var item in _cache.Values)
            {
                total += item.Confidence;
            }
            return total / _cache.Count;
        }
    }

    /// <summary>
    /// 🏢 Company Data Layer (Chat's FLC Standards Layer)
    /// Read-only company standards and vetted patterns
    /// </summary>
    public class CompanyDataLayer
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, AIKnowledgeItem> _standards;

        public CompanyDataLayer(ILogger logger)
        {
            _logger = logger;
            _standards = new Dictionary<string, AIKnowledgeItem>();
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                await LoadFLCStandardsAsync();
                _logger.Log("🏢 Company Data Layer initialized with FLC standards");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize Company Data Layer", ex);
                return false;
            }
        }

        public async Task<List<AIKnowledgeItem>> QueryAsync(AIKnowledgeQuery query)
        {
            var results = new List<AIKnowledgeItem>();
            
            foreach (var item in _standards.Values)
            {
                if (MatchesQuery(item, query))
                {
                    results.Add(item);
                }
            }

            return await Task.FromResult(results);
        }

        public async Task<AILayerStats> GetStatsAsync()
        {
            return await Task.FromResult(new AILayerStats
            {
                TotalKnowledgeItems = _standards.Count,
                AverageConfidence = 1.0, // Company standards are always high confidence
                LastUpdated = DateTime.UtcNow
            });
        }

        private async Task LoadFLCStandardsAsync()
        {
            // Load FLC company standards
            var flcStandards = new[]
            {
                new AIKnowledgeItem
                {
                    Category = "WallTypes",
                    Key = "FLC_6_Int_DW-FB",
                    Value = new { thickness = 6, type = "interior", finish = "drywall_both_sides" },
                    Source = AIDataLayer.Company,
                    Confidence = 1.0
                },
                new AIKnowledgeItem
                {
                    Category = "PanelNaming",
                    Key = "StandardFormat",
                    Value = "01-1012",
                    Source = AIDataLayer.Company,
                    Confidence = 1.0
                },
                new AIKnowledgeItem
                {
                    Category = "StudSpacing",
                    Key = "StandardSpacing",
                    Value = new[] { 12, 16, 19.2, 24 },
                    Source = AIDataLayer.Company,
                    Confidence = 1.0
                }
            };

            foreach (var standard in flcStandards)
            {
                _standards[standard.Id] = standard;
            }

            await Task.CompletedTask;
        }

        private bool MatchesQuery(AIKnowledgeItem item, AIKnowledgeQuery query)
        {
            if (!string.IsNullOrEmpty(query.Category) && item.Category != query.Category)
                return false;

            return true;
        }
    }

    /// <summary>
    /// 🌍 Industry Data Layer (Chat's Public Standards Layer)
    /// Read-only industry standards, codes, and public specifications
    /// </summary>
    public class IndustryDataLayer
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, AIKnowledgeItem> _industryStandards;

        public IndustryDataLayer(ILogger logger)
        {
            _logger = logger;
            _industryStandards = new Dictionary<string, AIKnowledgeItem>();
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                await LoadIndustryStandardsAsync();
                _logger.Log("🌍 Industry Data Layer initialized with public standards");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize Industry Data Layer", ex);
                return false;
            }
        }

        public async Task<List<AIKnowledgeItem>> QueryAsync(AIKnowledgeQuery query)
        {
            var results = new List<AIKnowledgeItem>();
            
            foreach (var item in _industryStandards.Values)
            {
                if (MatchesQuery(item, query))
                {
                    results.Add(item);
                }
            }

            return await Task.FromResult(results);
        }

        public async Task<AILayerStats> GetStatsAsync()
        {
            return await Task.FromResult(new AILayerStats
            {
                TotalKnowledgeItems = _industryStandards.Count,
                AverageConfidence = 0.9, // Industry standards are high but not perfect confidence
                LastUpdated = DateTime.UtcNow
            });
        }

        private async Task LoadIndustryStandardsAsync()
        {
            // Load industry standards and codes
            var industryStandards = new[]
            {
                new AIKnowledgeItem
                {
                    Category = "BuildingCodes",
                    Key = "IBC_2021",
                    Value = new { version = "2021", type = "international_building_code" },
                    Source = AIDataLayer.Industry,
                    Confidence = 0.95
                },
                new AIKnowledgeItem
                {
                    Category = "SteelFraming",
                    Key = "AISI_Standards",
                    Value = new { organization = "AISI", standards = new[] { "S100", "S200", "S240" } },
                    Source = AIDataLayer.Industry,
                    Confidence = 0.9
                },
                new AIKnowledgeItem
                {
                    Category = "Accessibility",
                    Key = "ADA_Requirements",
                    Value = new { door_width_min = 32, corridor_width_min = 44 },
                    Source = AIDataLayer.Industry,
                    Confidence = 0.95
                }
            };

            foreach (var standard in industryStandards)
            {
                _industryStandards[standard.Id] = standard;
            }

            await Task.CompletedTask;
        }

        private bool MatchesQuery(AIKnowledgeItem item, AIKnowledgeQuery query)
        {
            if (!string.IsNullOrEmpty(query.Category) && item.Category != query.Category)
                return false;

            return true;
        }
    }
}
