using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TycoonRevitAddin.AIActions.Data
{
    /// <summary>
    /// 🎯 AI Knowledge Query (Chat's Retrieval Pattern)
    /// </summary>
    public class AIKnowledgeQuery
    {
        public string QueryId { get; set; } = Guid.NewGuid().ToString();
        public AIQueryType QueryType { get; set; }
        public string Category { get; set; }
        public string SearchText { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public int MaxResults { get; set; } = 10;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 🧠 AI Knowledge Result (Chat's Response Pattern)
    /// </summary>
    public class AIKnowledgeResult
    {
        public string QueryId { get; set; }
        public AIKnowledgeQuery Query { get; set; }
        public List<AIKnowledgeItem> Results { get; set; } = new List<AIKnowledgeItem>();
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }

    /// <summary>
    /// 📋 AI Knowledge Item (Chat's Knowledge Unit)
    /// </summary>
    public class AIKnowledgeItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Category { get; set; }
        public string Key { get; set; }
        public object Value { get; set; }
        public AIDataLayer Source { get; set; }
        public double Confidence { get; set; } = 1.0;
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;
        public int UsageCount { get; set; } = 0;
    }

    /// <summary>
    /// 💾 AI Learning Data (Chat's Learning Loop)
    /// </summary>
    public class AILearningData
    {
        public string LearningId { get; set; } = Guid.NewGuid().ToString();
        public AILearningType LearningType { get; set; }
        public string Category { get; set; }
        public string Context { get; set; }
        public object Data { get; set; }
        public AILearningOutcome Outcome { get; set; }
        public double SuccessScore { get; set; } = 0.0;
        public Dictionary<string, object> Metrics { get; set; } = new Dictionary<string, object>();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string ProjectId { get; set; }
        public string UserId { get; set; }
    }

    /// <summary>
    /// 📊 AI Analytics (Chat's Metrics)
    /// </summary>
    public class AIAnalytics
    {
        public AILayerStats ProjectStats { get; set; }
        public AILayerStats CompanyStats { get; set; }
        public AILayerStats IndustryStats { get; set; }
        public DateTime GeneratedAt { get; set; }
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// 📈 AI Layer Statistics
    /// </summary>
    public class AILayerStats
    {
        public int TotalKnowledgeItems { get; set; }
        public int TotalLearnings { get; set; }
        public double AverageConfidence { get; set; }
        public Dictionary<string, int> CategoryCounts { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, double> SuccessRates { get; set; } = new Dictionary<string, double>();
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// 🎯 AI Query Types (Chat's Classification)
    /// </summary>
    public enum AIQueryType
    {
        WallTypeQuery,
        PanelPatternQuery,
        JointRequirementQuery,
        SpatialAnalysisQuery,
        ParameterMappingQuery,
        WorkflowPatternQuery,
        StructuralConstraintQuery,
        FLCStandardQuery,
        ProjectContextQuery,
        SemanticSearch
    }

    /// <summary>
    /// 💡 AI Learning Types (Chat's Learning Categories)
    /// </summary>
    public enum AILearningType
    {
        UserCorrection,
        WorkflowPattern,
        ParameterMapping,
        GeometryPattern,
        SuccessMetric,
        FailurePattern,
        OptimizationResult,
        UserPreference,
        ProjectSpecific,
        ContextualLearning
    }

    /// <summary>
    /// 🎯 AI Learning Outcomes (Chat's Success Tracking)
    /// </summary>
    public enum AILearningOutcome
    {
        Success,
        PartialSuccess,
        Failure,
        UserRejected,
        UserModified,
        SystemError,
        Timeout,
        Cancelled
    }

    /// <summary>
    /// 🏗️ AI Data Layers (Chat's Three-Tier Architecture)
    /// </summary>
    public enum AIDataLayer
    {
        Project,    // Fast-moving, short TTL
        Company,    // FLC standards, vetted patterns
        Industry    // Public specs, codes, standards
    }

    /// <summary>
    /// 🔧 AI Context Data (Chat's Context Awareness)
    /// </summary>
    public class AIContextData
    {
        public string ProjectId { get; set; }
        public string ProjectType { get; set; }
        public string BuildingType { get; set; }
        public Dictionary<string, object> ProjectParameters { get; set; } = new Dictionary<string, object>();
        public List<string> ActiveStandards { get; set; } = new List<string>();
        public Dictionary<string, object> UserPreferences { get; set; } = new Dictionary<string, object>();
        public DateTime ContextTimestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 🎨 AI Pattern Data (Chat's Pattern Recognition)
    /// </summary>
    public class AIPatternData
    {
        public string PatternId { get; set; } = Guid.NewGuid().ToString();
        public string PatternType { get; set; }
        public string Description { get; set; }
        public object PatternDefinition { get; set; }
        public double Confidence { get; set; }
        public int ObservationCount { get; set; }
        public List<string> ApplicableContexts { get; set; } = new List<string>();
        public Dictionary<string, object> ValidationMetrics { get; set; } = new Dictionary<string, object>();
        public DateTime FirstObserved { get; set; } = DateTime.UtcNow;
        public DateTime LastObserved { get; set; } = DateTime.UtcNow;
    }
}
