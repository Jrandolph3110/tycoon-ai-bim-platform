using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.AIActions.Data
{
    /// <summary>
    /// 🧠 AI Data Service - Chat's Three-Layer Architecture Implementation
    /// Manages Project/Company/Industry data layers with hybrid storage
    /// </summary>
    public class AIDataService
    {
        private readonly ILogger _logger;
        private readonly ProjectDataLayer _projectLayer;
        private readonly CompanyDataLayer _companyLayer;
        private readonly IndustryDataLayer _industryLayer;

        public AIDataService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _projectLayer = new ProjectDataLayer(logger);
            _companyLayer = new CompanyDataLayer(logger);
            _industryLayer = new IndustryDataLayer(logger);
        }

        /// <summary>
        /// 🎯 Initialize AI Data Service (Chat's Hybrid Architecture)
        /// Sets up three-layer data architecture with proper governance
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                _logger.Log("🧠 Initializing AI Data Service with three-layer architecture");

                // Initialize layers in order (Industry → Company → Project)
                await _industryLayer.InitializeAsync();
                await _companyLayer.InitializeAsync();
                await _projectLayer.InitializeAsync();

                _logger.Log("🧠 AI Data Service initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize AI Data Service", ex);
                return false;
            }
        }

        /// <summary>
        /// 🔍 Query AI Knowledge (Chat's Retrieval Pattern)
        /// Searches across all three layers with proper precedence
        /// </summary>
        public async Task<AIKnowledgeResult> QueryKnowledgeAsync(AIKnowledgeQuery query)
        {
            try
            {
                _logger.Log($"🔍 Querying AI knowledge: {query.QueryType}");

                var result = new AIKnowledgeResult
                {
                    QueryId = Guid.NewGuid().ToString(),
                    Query = query,
                    Timestamp = DateTime.UtcNow
                };

                // Chat's precedence: Project → Company → Industry
                var projectResults = await _projectLayer.QueryAsync(query);
                var companyResults = await _companyLayer.QueryAsync(query);
                var industryResults = await _industryLayer.QueryAsync(query);

                // Merge results with proper precedence
                result.Results = MergeKnowledgeResults(projectResults, companyResults, industryResults);
                result.Success = true;

                _logger.Log($"🔍 Knowledge query completed: {result.Results.Count} results");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("AI knowledge query failed", ex);
                return new AIKnowledgeResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 💾 Store AI Learning (Chat's Learning Loop)
        /// Stores new knowledge with proper layer governance
        /// </summary>
        public async Task<bool> StoreLearningAsync(AILearningData learning)
        {
            try
            {
                _logger.Log($"💾 Storing AI learning: {learning.LearningType}");

                // Chat's governance: Only project layer accepts new learnings directly
                var stored = await _projectLayer.StoreLearningAsync(learning);

                if (stored)
                {
                    _logger.Log("💾 AI learning stored successfully");
                    
                    // Check if learning should be promoted to company layer
                    await EvaluateForPromotion(learning);
                }

                return stored;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to store AI learning", ex);
                return false;
            }
        }

        /// <summary>
        /// 📊 Get AI Analytics (Chat's Metrics)
        /// Returns analytics across all three layers
        /// </summary>
        public async Task<AIAnalytics> GetAnalyticsAsync()
        {
            try
            {
                var analytics = new AIAnalytics
                {
                    ProjectStats = await _projectLayer.GetStatsAsync(),
                    CompanyStats = await _companyLayer.GetStatsAsync(),
                    IndustryStats = await _industryLayer.GetStatsAsync(),
                    GeneratedAt = DateTime.UtcNow
                };

                return analytics;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to get AI analytics", ex);
                return new AIAnalytics { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Merge knowledge results with Chat's precedence rules
        /// </summary>
        private List<AIKnowledgeItem> MergeKnowledgeResults(
            List<AIKnowledgeItem> projectResults,
            List<AIKnowledgeItem> companyResults,
            List<AIKnowledgeItem> industryResults)
        {
            var merged = new List<AIKnowledgeItem>();

            // Project results have highest precedence
            merged.AddRange(projectResults);

            // Add company results that don't conflict
            foreach (var companyItem in companyResults)
            {
                if (!HasConflict(merged, companyItem))
                {
                    merged.Add(companyItem);
                }
            }

            // Add industry results that don't conflict
            foreach (var industryItem in industryResults)
            {
                if (!HasConflict(merged, industryItem))
                {
                    merged.Add(industryItem);
                }
            }

            return merged;
        }

        /// <summary>
        /// Check if knowledge item conflicts with existing results
        /// </summary>
        private bool HasConflict(List<AIKnowledgeItem> existing, AIKnowledgeItem newItem)
        {
            foreach (var item in existing)
            {
                if (item.Category == newItem.Category && item.Key == newItem.Key)
                {
                    return true; // Conflict found
                }
            }
            return false;
        }

        /// <summary>
        /// Evaluate if learning should be promoted to company layer
        /// </summary>
        private async Task EvaluateForPromotion(AILearningData learning)
        {
            // Chat's promotion criteria: usage frequency, success rate, etc.
            // This would implement the promotion logic
            await Task.CompletedTask; // Placeholder
        }
    }
}
