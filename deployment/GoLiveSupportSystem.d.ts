/**
 * GoLiveSupportSystem - Comprehensive support framework for production launch
 *
 * Implements go-live support for successful F.L. Crane & Sons deployment:
 * - Real-time monitoring during initial production use
 * - Success metrics tracking (90% debug time reduction, AI accuracy, user adoption)
 * - Escalation procedures and support protocols
 * - Performance validation and optimization recommendations
 * - User feedback collection and issue resolution
 */
import { EventEmitter } from 'events';
export interface GoLiveSupportConfig {
    organizationName: string;
    supportPath: string;
    monitoringPeriod: number;
    successMetrics: SuccessMetric[];
    escalationLevels: EscalationLevel[];
    supportTeam: SupportTeamMember[];
    reportingSchedule: ReportingSchedule;
}
export interface SuccessMetric {
    metricName: string;
    description: string;
    targetValue: number;
    currentValue: number;
    unit: string;
    priority: 'critical' | 'high' | 'medium' | 'low';
    measurementMethod: string;
    validationCriteria: string[];
}
export interface EscalationLevel {
    level: number;
    name: string;
    triggerConditions: string[];
    responseTime: number;
    assignedTeam: string[];
    actions: string[];
    escalationCriteria: string[];
}
export interface SupportTeamMember {
    name: string;
    role: string;
    contactInfo: string;
    expertise: string[];
    availability: string;
    escalationLevel: number;
}
export interface ReportingSchedule {
    dailyReports: boolean;
    weeklyReports: boolean;
    monthlyReports: boolean;
    realTimeAlerts: boolean;
    stakeholderUpdates: string[];
}
export interface GoLiveMetrics {
    timestamp: Date;
    debugTimeReduction: {
        target: number;
        actual: number;
        improvement: number;
        sampleSize: number;
    };
    aiAccuracy: {
        target: number;
        actual: number;
        falsePositives: number;
        falseNegatives: number;
    };
    userAdoption: {
        totalUsers: number;
        activeUsers: number;
        adoptionRate: number;
        trainingCompletion: number;
    };
    systemPerformance: {
        uptime: number;
        averageLatency: number;
        throughput: number;
        errorRate: number;
    };
    businessImpact: {
        timeSaved: number;
        costSavings: number;
        errorsPrevented: number;
        productivityGain: number;
    };
}
export interface SupportIncident {
    incidentId: string;
    timestamp: Date;
    severity: 'low' | 'medium' | 'high' | 'critical';
    category: 'technical' | 'training' | 'workflow' | 'performance';
    description: string;
    reportedBy: string;
    assignedTo: string;
    status: 'open' | 'in_progress' | 'resolved' | 'closed';
    resolution: string;
    resolutionTime: number;
    preventiveMeasures: string[];
}
export interface GoLiveReport {
    reportId: string;
    generatedAt: Date;
    reportPeriod: {
        start: Date;
        end: Date;
    };
    organizationName: string;
    executiveSummary: {
        overallStatus: 'excellent' | 'good' | 'fair' | 'poor';
        keyAchievements: string[];
        criticalIssues: string[];
        recommendations: string[];
    };
    metricsPerformance: {
        debugTimeReduction: MetricPerformance;
        aiAccuracy: MetricPerformance;
        userAdoption: MetricPerformance;
        systemPerformance: MetricPerformance;
    };
    incidentSummary: {
        totalIncidents: number;
        resolvedIncidents: number;
        averageResolutionTime: number;
        criticalIncidents: number;
    };
    userFeedback: {
        satisfactionScore: number;
        commonIssues: string[];
        improvementSuggestions: string[];
    };
    nextSteps: string[];
}
export interface MetricPerformance {
    target: number;
    actual: number;
    variance: number;
    trend: 'improving' | 'stable' | 'declining';
    status: 'on_track' | 'at_risk' | 'off_track';
}
/**
 * Main go-live support system
 */
export declare class GoLiveSupportSystem extends EventEmitter {
    private metricsTracker;
    private incidentManager;
    private config;
    private supportStartTime;
    constructor(config: GoLiveSupportConfig);
    /**
     * Initialize go-live support system
     */
    initializeGoLiveSupport(): Promise<void>;
    private generateSupportDocumentation;
    private setupRealTimeMonitoring;
    private initializeSuccessMetrics;
    private collectAndAnalyzeMetrics;
    private checkForAlerts;
    private generateDailyReport;
    private determineOverallStatus;
    private generateKeyAchievements;
    private identifyCriticalIssues;
    private generateRecommendations;
    /**
     * Get current support status
     */
    getSupportStatus(): any;
    /**
     * Create support incident
     */
    createSupportIncident(severity: 'low' | 'medium' | 'high' | 'critical', category: 'technical' | 'training' | 'workflow' | 'performance', description: string, reportedBy: string): SupportIncident;
    /**
     * Resolve support incident
     */
    resolveSupportIncident(incidentId: string, resolution: string, preventiveMeasures?: string[]): boolean;
}
//# sourceMappingURL=GoLiveSupportSystem.d.ts.map