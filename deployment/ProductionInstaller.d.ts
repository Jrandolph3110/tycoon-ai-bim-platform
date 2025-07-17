/**
 * ProductionInstaller - Complete production deployment installer for F.L. Crane & Sons
 *
 * Implements Phase 5 production installer:
 * - Automated deployment orchestration with environment detection
 * - F.L. Crane & Sons specific configuration and integration
 * - Production monitoring setup with real-time dashboards
 * - Automated maintenance system initialization
 * - User onboarding and training material setup
 */
import { EventEmitter } from 'events';
import { DeploymentResult } from './DeploymentOrchestrator.js';
export interface ProductionInstallerConfig {
    organizationName: string;
    deploymentPath: string;
    enableMonitoring: boolean;
    enableMaintenance: boolean;
    enableUserTraining: boolean;
    customConfigurations: any[];
    installationMode: 'express' | 'custom' | 'enterprise';
}
export interface InstallationResult {
    success: boolean;
    installationId: string;
    deploymentResult: DeploymentResult;
    monitoringSetup?: any;
    maintenanceSetup?: any;
    userOnboarding?: any;
    postInstallationSteps: string[];
    supportInformation: any;
}
/**
 * Main production installer for F.L. Crane & Sons
 */
export declare class ProductionInstaller extends EventEmitter {
    private deploymentOrchestrator;
    private monitoringSetup;
    private maintenanceSystem;
    private installationId;
    constructor();
    /**
     * Execute complete production installation for F.L. Crane & Sons
     */
    executeProductionInstallation(config: ProductionInstallerConfig): Promise<InstallationResult>;
    private generateDeploymentConfig;
    private generateMonitoringConfig;
    private generateMaintenanceConfig;
    private setupUserOnboarding;
    private performFinalValidation;
    private generatePostInstallationSteps;
    private generateSupportInformation;
    private displayInstallationSummary;
    /**
     * Get installation status
     */
    getInstallationStatus(): any;
}
//# sourceMappingURL=ProductionInstaller.d.ts.map