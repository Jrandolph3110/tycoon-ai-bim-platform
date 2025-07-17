/**
 * UserTrainingFramework - Comprehensive training system for F.L. Crane & Sons
 *
 * Implements role-based training for successful adoption of transformational AI debugging:
 * - Executive leadership training on business impact and ROI
 * - Project managers training on workflow optimization and monitoring
 * - CAD operators training on daily usage and AI assistance features
 * - IT administrators training on system maintenance and troubleshooting
 * - Hands-on training scenarios using actual F.L. Crane workflows
 */
import { EventEmitter } from 'events';
export interface TrainingConfiguration {
    organizationName: string;
    trainingPath: string;
    userRoles: UserRole[];
    trainingModules: TrainingModule[];
    handsOnScenarios: TrainingScenario[];
    assessmentEnabled: boolean;
    certificationRequired: boolean;
}
export interface UserRole {
    roleName: string;
    description: string;
    userCount: number;
    trainingPriority: 'high' | 'medium' | 'low';
    requiredModules: string[];
    optionalModules: string[];
    handsOnScenarios: string[];
}
export interface TrainingModule {
    moduleId: string;
    title: string;
    description: string;
    duration: number;
    format: 'presentation' | 'hands-on' | 'video' | 'documentation';
    prerequisites: string[];
    learningObjectives: string[];
    content: TrainingContent;
}
export interface TrainingContent {
    overview: string;
    keyPoints: string[];
    demonstrations: Demonstration[];
    exercises: Exercise[];
    resources: string[];
}
export interface Demonstration {
    title: string;
    description: string;
    steps: string[];
    expectedOutcome: string;
    troubleshooting: string[];
}
export interface Exercise {
    title: string;
    description: string;
    instructions: string[];
    successCriteria: string[];
    timeLimit: number;
}
export interface TrainingScenario {
    scenarioId: string;
    title: string;
    description: string;
    targetRoles: string[];
    difficulty: 'beginner' | 'intermediate' | 'advanced';
    duration: number;
    workflow: WorkflowStep[];
    expectedBenefits: string[];
}
export interface WorkflowStep {
    stepNumber: number;
    action: string;
    expectedResult: string;
    aiAssistance: string;
    troubleshooting: string[];
}
export interface TrainingProgress {
    userId: string;
    userName: string;
    role: string;
    completedModules: string[];
    currentModule?: string;
    completedScenarios: string[];
    assessmentScores: {
        [moduleId: string]: number;
    };
    certificationStatus: 'not_started' | 'in_progress' | 'completed' | 'expired';
    lastActivity: Date;
}
export interface TrainingReport {
    organizationName: string;
    reportDate: Date;
    overallProgress: {
        totalUsers: number;
        usersStarted: number;
        usersCompleted: number;
        averageCompletionTime: number;
        certificationRate: number;
    };
    roleProgress: {
        [role: string]: RoleProgress;
    };
    moduleEffectiveness: {
        [moduleId: string]: ModuleEffectiveness;
    };
    recommendations: string[];
}
export interface RoleProgress {
    totalUsers: number;
    completedUsers: number;
    averageScore: number;
    commonChallenges: string[];
}
export interface ModuleEffectiveness {
    completionRate: number;
    averageScore: number;
    averageTime: number;
    userFeedback: number;
    improvementSuggestions: string[];
}
/**
 * Main user training framework
 */
export declare class UserTrainingFramework extends EventEmitter {
    private contentGenerator;
    private trainingProgress;
    constructor();
    /**
     * Generate comprehensive training program for F.L. Crane & Sons
     */
    generateFLCTrainingProgram(trainingPath: string): Promise<TrainingConfiguration>;
    private generateHandsOnScenarios;
    private generateTrainingMaterials;
    private generateModuleMaterial;
    private generateQuickReferenceGuide;
    /**
     * Track user training progress
     */
    updateUserProgress(userId: string, moduleId: string, completed: boolean, score?: number): void;
    /**
     * Generate training progress report
     */
    generateProgressReport(): TrainingReport;
    /**
     * Get training status
     */
    getTrainingStatus(): any;
    private calculateCompletionRate;
    private calculateAverageProgress;
}
//# sourceMappingURL=UserTrainingFramework.d.ts.map