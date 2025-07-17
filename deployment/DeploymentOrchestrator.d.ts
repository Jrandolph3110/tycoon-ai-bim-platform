/**
 * DeploymentOrchestrator - Comprehensive production deployment framework
 *
 * Implements Phase 5 production deployment preparation:
 * - Automated environment detection and configuration
 * - F.L. Crane & Sons specific setup and integration
 * - Production monitoring and maintenance automation
 * - User onboarding and training material setup
 * - Complete deployment validation and health checks
 */
import { EventEmitter } from 'events';
export interface DeploymentConfig {
    targetEnvironment: 'development' | 'staging' | 'production';
    organizationName: string;
    deploymentPath: string;
    enableMonitoring: boolean;
    enableAutoMaintenance: boolean;
    customConfigurations: CustomConfiguration[];
    userTraining: boolean;
    backupExisting: boolean;
}
export interface CustomConfiguration {
    name: string;
    category: 'revit' | 'framecad' | 'workflow' | 'security';
    settings: any;
    required: boolean;
}
export interface DeploymentResult {
    success: boolean;
    deploymentId: string;
    installedComponents: InstalledComponent[];
    configurationApplied: string[];
    validationResults: ValidationResult[];
    monitoringSetup: MonitoringSetup;
    userOnboarding: UserOnboardingSetup;
    rollbackProcedure: string[];
    supportInformation: SupportInfo;
}
export interface InstalledComponent {
    name: string;
    version: string;
    path: string;
    status: 'installed' | 'configured' | 'validated' | 'failed';
    dependencies: string[];
}
export interface ValidationResult {
    component: string;
    test: string;
    passed: boolean;
    details: string;
    recommendation?: string;
}
export interface MonitoringSetup {
    dashboardUrl: string;
    alertingConfigured: boolean;
    healthChecksEnabled: boolean;
    metricsCollectionActive: boolean;
    supportContactInfo: string;
}
export interface UserOnboardingSetup {
    quickStartGuideGenerated: boolean;
    trainingMaterialsInstalled: boolean;
    initialConfigurationComplete: boolean;
    supportDocumentationAvailable: boolean;
}
export interface SupportInfo {
    documentationPath: string;
    troubleshootingGuide: string;
    contactInformation: string;
    escalationProcedures: string[];
}
export interface EnvironmentInfo {
    operatingSystem: string;
    architecture: string;
    nodeVersion: string;
    revitVersions: string[];
    pyRevitInstalled: boolean;
    pyRevitVersion?: string;
    availableDiskSpace: number;
    availableMemory: number;
    networkConnectivity: boolean;
}
/**
 * Main deployment orchestrator
 */
export declare class DeploymentOrchestrator extends EventEmitter {
    private environmentDetector;
    private flcConfigManager;
    private deploymentId;
    constructor();
    /**
     * Execute complete production deployment
     */
    executeDeployment(config: DeploymentConfig): Promise<DeploymentResult>;
    private createBackup;
    private installCoreSystem;
    private copyDirectory;
    private setupProductionMonitoring;
    private setupAutomatedMaintenance;
    private setupUserOnboarding;
    private validateDeployment;
    private setupSupportInformation;
    private generateRollbackProcedure;
    /**
     * Get deployment status
     */
    getDeploymentStatus(): any;
}
//# sourceMappingURL=DeploymentOrchestrator.d.ts.map