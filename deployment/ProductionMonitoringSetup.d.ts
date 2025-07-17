/**
 * ProductionMonitoringSetup - Real-time monitoring and alerting for production deployment
 *
 * Implements Phase 5 production monitoring:
 * - Real-time performance dashboards with KPI tracking
 * - Automated alerting for system health and performance issues
 * - Usage analytics and debug time reduction metrics
 * - F.L. Crane & Sons specific monitoring configurations
 * - Self-healing system monitoring with automated responses
 */
import { EventEmitter } from 'events';
export interface MonitoringConfiguration {
    dashboardPort: number;
    metricsCollectionInterval: number;
    alertThresholds: AlertThresholds;
    enableRealTimeUpdates: boolean;
    enableAutomatedResponses: boolean;
    organizationSettings: OrganizationSettings;
}
export interface AlertThresholds {
    processingLatency: {
        warning: number;
        critical: number;
    };
    aiAccuracy: {
        warning: number;
        critical: number;
    };
    debugTimeReduction: {
        warning: number;
        critical: number;
    };
    systemHealth: {
        warning: string;
        critical: string;
    };
    memoryUsage: {
        warning: number;
        critical: number;
    };
    errorRate: {
        warning: number;
        critical: number;
    };
}
export interface OrganizationSettings {
    name: string;
    contactEmail: string;
    alertWebhook?: string;
    customMetrics: CustomMetric[];
    workflowSpecificAlerts: WorkflowAlert[];
}
export interface CustomMetric {
    name: string;
    description: string;
    unit: string;
    target: number;
    category: 'performance' | 'quality' | 'usage' | 'business';
}
export interface WorkflowAlert {
    workflowName: string;
    triggerCondition: string;
    severity: 'info' | 'warning' | 'critical';
    action: string[];
}
export interface MonitoringMetrics {
    timestamp: Date;
    system: SystemMetrics;
    ai: AIMetrics;
    performance: PerformanceMetrics;
    usage: UsageMetrics;
    business: BusinessMetrics;
}
export interface SystemMetrics {
    health: 'healthy' | 'warning' | 'critical';
    uptime: number;
    memoryUsage: number;
    cpuUsage: number;
    diskUsage: number;
    networkLatency: number;
    activeConnections: number;
}
export interface AIMetrics {
    patternRecognitionAccuracy: number;
    proactiveDetectionRate: number;
    falsePositiveRate: number;
    processingLatency: number;
    modelsActive: number;
    learningRate: number;
}
export interface PerformanceMetrics {
    averageLatency: number;
    peakLatency: number;
    throughput: number;
    queueDepth: number;
    errorRate: number;
    recoveryTime: number;
}
export interface UsageMetrics {
    activeUsers: number;
    scriptsMonitored: number;
    logsProcessed: number;
    debugSessionsActive: number;
    alertsGenerated: number;
    userSatisfactionScore: number;
}
export interface BusinessMetrics {
    debugTimeReduction: number;
    productivityGain: number;
    errorsPrevented: number;
    costSavings: number;
    userAdoption: number;
    roiMetrics: number;
}
export interface DashboardData {
    metrics: MonitoringMetrics;
    alerts: Alert[];
    trends: TrendData[];
    recommendations: Recommendation[];
}
export interface Alert {
    id: string;
    timestamp: Date;
    severity: 'info' | 'warning' | 'critical';
    category: string;
    message: string;
    details: string;
    acknowledged: boolean;
    resolvedAt?: Date;
    actions: string[];
}
export interface TrendData {
    metric: string;
    timeframe: string;
    data: {
        timestamp: Date;
        value: number;
    }[];
    trend: 'improving' | 'stable' | 'declining';
}
export interface Recommendation {
    id: string;
    category: 'performance' | 'security' | 'usage' | 'maintenance';
    priority: 'low' | 'medium' | 'high';
    title: string;
    description: string;
    action: string;
    estimatedImpact: string;
}
/**
 * Main production monitoring setup
 */
export declare class ProductionMonitoringSetup extends EventEmitter {
    private metricsCollector;
    private alertManager;
    private webSocketServer;
    private httpServer;
    private config;
    constructor(config: MonitoringConfiguration);
    /**
     * Initialize production monitoring system
     */
    initializeMonitoring(deploymentPath: string): Promise<void>;
    private generateMonitoringConfiguration;
    private createDashboardHTML;
    private startDashboardServer;
    private setupAutomatedAlerting;
    private generateRecommendations;
    /**
     * Stop monitoring system
     */
    stopMonitoring(): Promise<void>;
    /**
     * Get current monitoring status
     */
    getMonitoringStatus(): any;
    /**
     * Generate monitoring report
     */
    generateMonitoringReport(deploymentPath: string): Promise<void>;
}
//# sourceMappingURL=ProductionMonitoringSetup.d.ts.map