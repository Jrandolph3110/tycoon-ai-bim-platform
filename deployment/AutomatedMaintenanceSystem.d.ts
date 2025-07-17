/**
 * AutomatedMaintenanceSystem - Self-managing system maintenance and optimization
 *
 * Implements Phase 5 automated maintenance:
 * - Automated log rotation and cleanup procedures
 * - Performance optimization and system tuning
 * - Health monitoring with self-healing capabilities
 * - Backup and recovery automation
 * - System updates and patch management
 */
import { EventEmitter } from 'events';
export interface MaintenanceConfiguration {
    deploymentPath: string;
    schedules: MaintenanceSchedule[];
    enableSelfHealing: boolean;
    enableAutomaticUpdates: boolean;
    backupRetentionDays: number;
    logRetentionDays: number;
    performanceOptimization: boolean;
}
export interface MaintenanceSchedule {
    name: string;
    frequency: 'hourly' | 'daily' | 'weekly' | 'monthly';
    time?: string;
    dayOfWeek?: number;
    dayOfMonth?: number;
    tasks: MaintenanceTask[];
    enabled: boolean;
}
export interface MaintenanceTask {
    name: string;
    type: 'cleanup' | 'optimization' | 'backup' | 'health_check' | 'update' | 'custom';
    parameters: any;
    timeout: number;
    retryCount: number;
    criticalTask: boolean;
}
export interface MaintenanceResult {
    taskName: string;
    startTime: Date;
    endTime: Date;
    success: boolean;
    details: string;
    warnings: string[];
    errors: string[];
    metricsImpact: MetricsImpact;
}
export interface MetricsImpact {
    diskSpaceFreed: number;
    performanceImprovement: number;
    memoryOptimized: number;
    errorsResolved: number;
}
export interface SystemHealth {
    overall: 'healthy' | 'warning' | 'critical';
    components: ComponentHealth[];
    recommendations: string[];
    lastCheckTime: Date;
}
export interface ComponentHealth {
    name: string;
    status: 'healthy' | 'warning' | 'critical';
    details: string;
    metrics: any;
}
/**
 * Main automated maintenance system
 */
export declare class AutomatedMaintenanceSystem extends EventEmitter {
    private logRotationManager;
    private performanceOptimizer;
    private healthMonitor;
    private config;
    private scheduledTasks;
    constructor(config: MaintenanceConfiguration);
    /**
     * Initialize automated maintenance system
     */
    initializeMaintenanceSystem(): Promise<void>;
    private scheduleMaintenanceTasks;
    private calculateInterval;
    private executeMaintenanceSchedule;
    private executeMaintenanceTask;
    private logMaintenanceResults;
    private setupSelfHealing;
    private performSelfHealing;
    /**
     * Stop automated maintenance system
     */
    stopMaintenanceSystem(): Promise<void>;
    /**
     * Get maintenance system status
     */
    getMaintenanceStatus(): any;
}
/**
 * Main automated maintenance system
 */
export declare class AutomatedMaintenanceSystem extends EventEmitter {
    private logRotationManager;
    private performanceOptimizer;
    private healthMonitor;
    private config;
    private scheduledTasks;
    constructor(config: MaintenanceConfiguration);
    /**
     * Initialize automated maintenance system
     */
    initializeMaintenanceSystem(): Promise<void>;
    private scheduleMaintenanceTasks;
    private calculateInterval;
    private executeMaintenanceSchedule;
    private executeMaintenanceTask;
    private logMaintenanceResults;
    private setupSelfHealing;
    private performSelfHealing;
    /**
     * Stop automated maintenance system
     */
    stopMaintenanceSystem(): Promise<void>;
    /**
     * Get maintenance system status
     */
    getMaintenanceStatus(): any;
}
//# sourceMappingURL=AutomatedMaintenanceSystem.d.ts.map