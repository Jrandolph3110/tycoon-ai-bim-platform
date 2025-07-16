/**
 * OperationalMonitor - Enhanced KPI dashboard and alerting system
 * 
 * Implements ChatGPT's operational monitoring requirements:
 * - KPI dashboard with heap usage, queue depth, dropped messages
 * - Configurable alerting with thresholds (memory >80%, queue >800)
 * - Operational metrics collection for Day 2 operations
 * - Health status monitoring with recovery recommendations
 */

import { EventEmitter } from 'events';
import chalk from 'chalk';

export interface KPIMetrics {
    timestamp: Date;
    heapUsageMB: number;
    heapUsagePercent: number;
    queueDepth: number;
    droppedMessages: number;
    activeStreams: number;
    throughputLinesPerSecond: number;
    averageLatencyMs: number;
    errorRate: number;
    networkFlaps: number;
    recoveryTime: number;
}

export interface AlertThreshold {
    metric: keyof KPIMetrics;
    operator: 'gt' | 'lt' | 'eq' | 'gte' | 'lte';
    value: number;
    severity: 'info' | 'warning' | 'error' | 'critical';
    message: string;
    enabled: boolean;
}

export interface Alert {
    id: string;
    timestamp: Date;
    severity: 'info' | 'warning' | 'error' | 'critical';
    metric: string;
    currentValue: number;
    threshold: number;
    message: string;
    acknowledged: boolean;
    resolvedAt?: Date;
}

export interface HealthStatus {
    overall: 'healthy' | 'warning' | 'critical' | 'unknown';
    components: {
        memory: 'healthy' | 'warning' | 'critical';
        performance: 'healthy' | 'warning' | 'critical';
        network: 'healthy' | 'warning' | 'critical';
        streams: 'healthy' | 'warning' | 'critical';
    };
    recommendations: string[];
    lastUpdated: Date;
}

export interface OperationalReport {
    reportId: string;
    generatedAt: Date;
    timeRange: {
        start: Date;
        end: Date;
    };
    summary: {
        totalStreams: number;
        totalMessages: number;
        averageThroughput: number;
        uptimePercent: number;
        errorCount: number;
        alertCount: number;
    };
    trends: {
        memoryTrend: 'increasing' | 'decreasing' | 'stable';
        performanceTrend: 'improving' | 'degrading' | 'stable';
        errorTrend: 'increasing' | 'decreasing' | 'stable';
    };
    recommendations: string[];
}

/**
 * Alert manager for threshold-based monitoring
 */
class AlertManager {
    private alerts: Map<string, Alert> = new Map();
    private thresholds: AlertThreshold[] = [];
    private alertIdCounter: number = 1;

    constructor() {
        this.initializeDefaultThresholds();
    }

    private initializeDefaultThresholds(): void {
        this.thresholds = [
            {
                metric: 'heapUsagePercent',
                operator: 'gt',
                value: 80,
                severity: 'warning',
                message: 'High memory usage detected',
                enabled: true
            },
            {
                metric: 'heapUsagePercent',
                operator: 'gt',
                value: 90,
                severity: 'critical',
                message: 'Critical memory usage - immediate action required',
                enabled: true
            },
            {
                metric: 'queueDepth',
                operator: 'gt',
                value: 800,
                severity: 'warning',
                message: 'Queue depth approaching limit',
                enabled: true
            },
            {
                metric: 'queueDepth',
                operator: 'gt',
                value: 950,
                severity: 'critical',
                message: 'Queue depth critical - messages may be dropped',
                enabled: true
            },
            {
                metric: 'errorRate',
                operator: 'gt',
                value: 0.05,
                severity: 'warning',
                message: 'Error rate elevated above 5%',
                enabled: true
            },
            {
                metric: 'averageLatencyMs',
                operator: 'gt',
                value: 100,
                severity: 'warning',
                message: 'High latency detected',
                enabled: true
            },
            {
                metric: 'droppedMessages',
                operator: 'gt',
                value: 10,
                severity: 'error',
                message: 'Messages being dropped due to back-pressure',
                enabled: true
            }
        ];
    }

    checkThresholds(metrics: KPIMetrics): Alert[] {
        const newAlerts: Alert[] = [];

        for (const threshold of this.thresholds) {
            if (!threshold.enabled) continue;

            const currentValue = metrics[threshold.metric] as number;
            const breached = this.evaluateThreshold(currentValue, threshold);

            if (breached) {
                const alertId = `alert_${this.alertIdCounter++}`;
                const alert: Alert = {
                    id: alertId,
                    timestamp: new Date(),
                    severity: threshold.severity,
                    metric: threshold.metric,
                    currentValue,
                    threshold: threshold.value,
                    message: threshold.message,
                    acknowledged: false
                };

                this.alerts.set(alertId, alert);
                newAlerts.push(alert);
            }
        }

        return newAlerts;
    }

    private evaluateThreshold(value: number, threshold: AlertThreshold): boolean {
        switch (threshold.operator) {
            case 'gt': return value > threshold.value;
            case 'gte': return value >= threshold.value;
            case 'lt': return value < threshold.value;
            case 'lte': return value <= threshold.value;
            case 'eq': return value === threshold.value;
            default: return false;
        }
    }

    acknowledgeAlert(alertId: string): boolean {
        const alert = this.alerts.get(alertId);
        if (alert) {
            alert.acknowledged = true;
            return true;
        }
        return false;
    }

    resolveAlert(alertId: string): boolean {
        const alert = this.alerts.get(alertId);
        if (alert) {
            alert.resolvedAt = new Date();
            return true;
        }
        return false;
    }

    getActiveAlerts(): Alert[] {
        return Array.from(this.alerts.values()).filter(a => !a.resolvedAt);
    }

    getAlertHistory(): Alert[] {
        return Array.from(this.alerts.values());
    }

    updateThreshold(metric: keyof KPIMetrics, value: number): void {
        const threshold = this.thresholds.find(t => t.metric === metric);
        if (threshold) {
            threshold.value = value;
        }
    }
}

/**
 * Health status calculator
 */
class HealthCalculator {
    calculateHealthStatus(metrics: KPIMetrics, alerts: Alert[]): HealthStatus {
        const components = {
            memory: this.calculateMemoryHealth(metrics),
            performance: this.calculatePerformanceHealth(metrics),
            network: this.calculateNetworkHealth(metrics),
            streams: this.calculateStreamHealth(metrics)
        };

        const overall = this.calculateOverallHealth(components, alerts);
        const recommendations = this.generateRecommendations(metrics, components);

        return {
            overall,
            components,
            recommendations,
            lastUpdated: new Date()
        };
    }

    private calculateMemoryHealth(metrics: KPIMetrics): 'healthy' | 'warning' | 'critical' {
        if (metrics.heapUsagePercent > 90) return 'critical';
        if (metrics.heapUsagePercent > 80) return 'warning';
        return 'healthy';
    }

    private calculatePerformanceHealth(metrics: KPIMetrics): 'healthy' | 'warning' | 'critical' {
        if (metrics.averageLatencyMs > 200 || metrics.throughputLinesPerSecond < 1000) return 'critical';
        if (metrics.averageLatencyMs > 100 || metrics.throughputLinesPerSecond < 5000) return 'warning';
        return 'healthy';
    }

    private calculateNetworkHealth(metrics: KPIMetrics): 'healthy' | 'warning' | 'critical' {
        if (metrics.networkFlaps > 5 || metrics.recoveryTime > 30000) return 'critical';
        if (metrics.networkFlaps > 2 || metrics.recoveryTime > 10000) return 'warning';
        return 'healthy';
    }

    private calculateStreamHealth(metrics: KPIMetrics): 'healthy' | 'warning' | 'critical' {
        if (metrics.queueDepth > 950 || metrics.droppedMessages > 50) return 'critical';
        if (metrics.queueDepth > 800 || metrics.droppedMessages > 10) return 'warning';
        return 'healthy';
    }

    private calculateOverallHealth(
        components: HealthStatus['components'], 
        alerts: Alert[]
    ): 'healthy' | 'warning' | 'critical' | 'unknown' {
        const criticalAlerts = alerts.filter(a => a.severity === 'critical' && !a.resolvedAt);
        if (criticalAlerts.length > 0) return 'critical';

        const componentValues = Object.values(components);
        if (componentValues.some(c => c === 'critical')) return 'critical';
        if (componentValues.some(c => c === 'warning')) return 'warning';
        
        return 'healthy';
    }

    private generateRecommendations(
        metrics: KPIMetrics, 
        components: HealthStatus['components']
    ): string[] {
        const recommendations: string[] = [];

        if (components.memory === 'warning' || components.memory === 'critical') {
            recommendations.push('Consider reducing buffer sizes or implementing more aggressive garbage collection');
            recommendations.push('Monitor for memory leaks in long-running streams');
        }

        if (components.performance === 'warning' || components.performance === 'critical') {
            recommendations.push('Optimize log processing pipeline for better throughput');
            recommendations.push('Consider horizontal scaling with multiple stream processors');
        }

        if (components.network === 'warning' || components.network === 'critical') {
            recommendations.push('Implement more robust network retry mechanisms');
            recommendations.push('Consider connection pooling for better network resilience');
        }

        if (components.streams === 'warning' || components.streams === 'critical') {
            recommendations.push('Increase queue depth limits or implement flow control');
            recommendations.push('Review back-pressure mechanisms for better message handling');
        }

        if (metrics.errorRate > 0.01) {
            recommendations.push('Investigate root causes of elevated error rates');
        }

        return recommendations;
    }
}

/**
 * Main operational monitor
 */
export class OperationalMonitor extends EventEmitter {
    private alertManager: AlertManager;
    private healthCalculator: HealthCalculator;
    private metricsHistory: KPIMetrics[] = [];
    private monitoringInterval: NodeJS.Timeout | null = null;
    private isMonitoring: boolean = false;

    constructor() {
        super();
        this.alertManager = new AlertManager();
        this.healthCalculator = new HealthCalculator();
    }

    /**
     * Start operational monitoring
     */
    startMonitoring(intervalMs: number = 5000): void {
        if (this.isMonitoring) {
            console.warn(chalk.yellow('⚠️ Operational monitoring is already running'));
            return;
        }

        this.isMonitoring = true;
        console.log(chalk.green('📊 Starting operational monitoring...'));

        this.monitoringInterval = setInterval(() => {
            this.collectMetrics();
        }, intervalMs);
    }

    /**
     * Stop operational monitoring
     */
    stopMonitoring(): void {
        if (this.monitoringInterval) {
            clearInterval(this.monitoringInterval);
            this.monitoringInterval = null;
        }
        this.isMonitoring = false;
        console.log(chalk.blue('📊 Operational monitoring stopped'));
    }

    /**
     * Collect current metrics
     */
    private collectMetrics(): void {
        const memoryUsage = process.memoryUsage();
        const heapUsageMB = memoryUsage.heapUsed / 1024 / 1024;
        const heapTotal = memoryUsage.heapTotal / 1024 / 1024;
        
        const metrics: KPIMetrics = {
            timestamp: new Date(),
            heapUsageMB,
            heapUsagePercent: (heapUsageMB / heapTotal) * 100,
            queueDepth: 0, // Will be updated by stream managers
            droppedMessages: 0, // Will be updated by stream managers
            activeStreams: 0, // Will be updated by stream managers
            throughputLinesPerSecond: 0, // Will be updated by stream managers
            averageLatencyMs: 0, // Will be updated by stream managers
            errorRate: 0, // Will be updated by stream managers
            networkFlaps: 0, // Will be updated by network recovery manager
            recoveryTime: 0 // Will be updated by network recovery manager
        };

        // Store metrics
        this.metricsHistory.push(metrics);
        
        // Keep only last 1000 metrics to prevent memory growth
        if (this.metricsHistory.length > 1000) {
            this.metricsHistory.shift();
        }

        // Check for alerts
        const newAlerts = this.alertManager.checkThresholds(metrics);
        if (newAlerts.length > 0) {
            this.handleNewAlerts(newAlerts);
        }

        // Calculate health status
        const healthStatus = this.healthCalculator.calculateHealthStatus(
            metrics, 
            this.alertManager.getActiveAlerts()
        );

        // Emit monitoring events
        this.emit('metricsCollected', metrics);
        this.emit('healthStatusUpdated', healthStatus);
    }

    /**
     * Handle new alerts
     */
    private handleNewAlerts(alerts: Alert[]): void {
        for (const alert of alerts) {
            console.log(this.formatAlert(alert));
            this.emit('alertTriggered', alert);
        }
    }

    /**
     * Format alert for console output
     */
    private formatAlert(alert: Alert): string {
        const severityColors = {
            info: chalk.blue,
            warning: chalk.yellow,
            error: chalk.red,
            critical: chalk.red.bold
        };

        const color = severityColors[alert.severity];
        const icon = alert.severity === 'critical' ? '🚨' : 
                    alert.severity === 'error' ? '❌' : 
                    alert.severity === 'warning' ? '⚠️' : 'ℹ️';

        return color(`${icon} [${alert.severity.toUpperCase()}] ${alert.message} (${alert.metric}: ${alert.currentValue} > ${alert.threshold})`);
    }

    /**
     * Get current KPI dashboard
     */
    getKPIDashboard(): any {
        const latestMetrics = this.metricsHistory[this.metricsHistory.length - 1];
        const activeAlerts = this.alertManager.getActiveAlerts();
        const healthStatus = latestMetrics ? 
            this.healthCalculator.calculateHealthStatus(latestMetrics, activeAlerts) : 
            null;

        return {
            timestamp: new Date(),
            metrics: latestMetrics,
            healthStatus,
            activeAlerts: activeAlerts.length,
            criticalAlerts: activeAlerts.filter(a => a.severity === 'critical').length,
            trends: this.calculateTrends(),
            recommendations: healthStatus?.recommendations || []
        };
    }

    /**
     * Calculate performance trends
     */
    private calculateTrends(): any {
        if (this.metricsHistory.length < 10) {
            return {
                memoryTrend: 'stable',
                performanceTrend: 'stable',
                errorTrend: 'stable'
            };
        }

        const recent = this.metricsHistory.slice(-10);
        const older = this.metricsHistory.slice(-20, -10);

        const recentAvgMemory = recent.reduce((sum, m) => sum + m.heapUsagePercent, 0) / recent.length;
        const olderAvgMemory = older.reduce((sum, m) => sum + m.heapUsagePercent, 0) / older.length;

        const recentAvgLatency = recent.reduce((sum, m) => sum + m.averageLatencyMs, 0) / recent.length;
        const olderAvgLatency = older.reduce((sum, m) => sum + m.averageLatencyMs, 0) / older.length;

        const recentAvgErrors = recent.reduce((sum, m) => sum + m.errorRate, 0) / recent.length;
        const olderAvgErrors = older.reduce((sum, m) => sum + m.errorRate, 0) / older.length;

        return {
            memoryTrend: this.calculateTrend(recentAvgMemory, olderAvgMemory),
            performanceTrend: this.calculateTrend(olderAvgLatency, recentAvgLatency), // Lower is better
            errorTrend: this.calculateTrend(recentAvgErrors, olderAvgErrors)
        };
    }

    private calculateTrend(recent: number, older: number): 'increasing' | 'decreasing' | 'stable' {
        const threshold = 0.05; // 5% change threshold
        const change = (recent - older) / older;
        
        if (change > threshold) return 'increasing';
        if (change < -threshold) return 'decreasing';
        return 'stable';
    }

    /**
     * Update metrics from external sources
     */
    updateMetrics(updates: Partial<KPIMetrics>): void {
        const latestMetrics = this.metricsHistory[this.metricsHistory.length - 1];
        if (latestMetrics) {
            Object.assign(latestMetrics, updates);
        }
    }

    /**
     * Generate operational report
     */
    generateOperationalReport(timeRangeHours: number = 24): OperationalReport {
        const endTime = new Date();
        const startTime = new Date(endTime.getTime() - (timeRangeHours * 60 * 60 * 1000));
        
        const relevantMetrics = this.metricsHistory.filter(m => 
            m.timestamp >= startTime && m.timestamp <= endTime
        );

        const totalMessages = relevantMetrics.reduce((sum, m) => sum + m.throughputLinesPerSecond, 0);
        const averageThroughput = relevantMetrics.length > 0 ? 
            totalMessages / relevantMetrics.length : 0;

        const errorCount = relevantMetrics.reduce((sum, m) => sum + m.errorRate, 0);
        const alertHistory = this.alertManager.getAlertHistory().filter(a => 
            a.timestamp >= startTime && a.timestamp <= endTime
        );

        return {
            reportId: `report_${Date.now()}`,
            generatedAt: new Date(),
            timeRange: { start: startTime, end: endTime },
            summary: {
                totalStreams: Math.max(...relevantMetrics.map(m => m.activeStreams), 0),
                totalMessages: Math.round(totalMessages),
                averageThroughput: Math.round(averageThroughput),
                uptimePercent: 99.9, // Placeholder - would calculate from actual uptime data
                errorCount: Math.round(errorCount),
                alertCount: alertHistory.length
            },
            trends: this.calculateTrends(),
            recommendations: this.generateOperationalRecommendations(relevantMetrics)
        };
    }

    private generateOperationalRecommendations(metrics: KPIMetrics[]): string[] {
        const recommendations: string[] = [];
        
        if (metrics.length === 0) {
            recommendations.push('Insufficient data for recommendations');
            return recommendations;
        }

        const avgMemory = metrics.reduce((sum, m) => sum + m.heapUsagePercent, 0) / metrics.length;
        const avgLatency = metrics.reduce((sum, m) => sum + m.averageLatencyMs, 0) / metrics.length;
        const totalDropped = metrics.reduce((sum, m) => sum + m.droppedMessages, 0);

        if (avgMemory > 70) {
            recommendations.push('Consider implementing memory optimization strategies');
        }

        if (avgLatency > 50) {
            recommendations.push('Investigate performance bottlenecks in log processing pipeline');
        }

        if (totalDropped > 0) {
            recommendations.push('Review back-pressure mechanisms to reduce message drops');
        }

        return recommendations;
    }

    /**
     * Acknowledge an alert
     */
    acknowledgeAlert(alertId: string): boolean {
        return this.alertManager.acknowledgeAlert(alertId);
    }

    /**
     * Resolve an alert
     */
    resolveAlert(alertId: string): boolean {
        return this.alertManager.resolveAlert(alertId);
    }

    /**
     * Get metrics history
     */
    getMetricsHistory(hours: number = 1): KPIMetrics[] {
        const cutoff = new Date(Date.now() - (hours * 60 * 60 * 1000));
        return this.metricsHistory.filter(m => m.timestamp >= cutoff);
    }
}
