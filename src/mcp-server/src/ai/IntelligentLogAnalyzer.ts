/**
 * IntelligentLogAnalyzer - Context-aware debugging assistance with AI workflow integration
 * 
 * Implements Phase 3 intelligent analysis features:
 * - Context-aware debugging assistance with historical correlation
 * - AI workflow integration for automated script monitoring
 * - Smart alerting with ML-based anomaly detection
 * - Intelligent log correlation across multiple sources
 */

import { EventEmitter } from 'events';
import { LogEntry } from '../streaming/LogStreamer.js';
import { PatternRecognitionEngine, PatternAnalysisResult, AnomalyDetection } from './PatternRecognitionEngine.js';
import chalk from 'chalk';

export interface DebugContext {
    sessionId: string;
    scriptName?: string;
    userId: string;
    startTime: Date;
    relatedLogs: LogEntry[];
    errorSequence: LogEntry[];
    systemState: {
        memoryUsage: number;
        cpuUsage: number;
        activeConnections: number;
        queueDepth: number;
    };
    userActions: string[];
    environmentInfo: {
        revitVersion?: string;
        osVersion?: string;
        platformVersion?: string;
    };
}

export interface DebugSuggestion {
    id: string;
    type: 'immediate' | 'investigative' | 'preventive';
    priority: 'low' | 'medium' | 'high' | 'critical';
    title: string;
    description: string;
    steps: string[];
    estimatedTime: string;
    confidence: number;
    relatedPatterns: string[];
    codeSnippets?: string[];
    documentationLinks?: string[];
}

export interface WorkflowIntegration {
    workflowId: string;
    triggerConditions: {
        patterns: string[];
        anomalyTypes: string[];
        riskThreshold: number;
    };
    actions: {
        type: 'notification' | 'automation' | 'escalation';
        target: string;
        parameters: any;
    }[];
    isActive: boolean;
    executionHistory: {
        timestamp: Date;
        trigger: string;
        result: 'success' | 'failure' | 'partial';
        details: string;
    }[];
}

export interface SmartAlert {
    id: string;
    timestamp: Date;
    severity: 'info' | 'warning' | 'error' | 'critical';
    category: 'performance' | 'security' | 'functional' | 'system';
    title: string;
    message: string;
    context: DebugContext;
    suggestions: DebugSuggestion[];
    correlatedEvents: LogEntry[];
    predictedImpact: {
        scope: 'local' | 'system' | 'global';
        timeframe: 'immediate' | 'short-term' | 'long-term';
        description: string;
    };
    autoResolution?: {
        possible: boolean;
        confidence: number;
        actions: string[];
    };
}

/**
 * Context correlation engine for multi-source log analysis
 */
class ContextCorrelationEngine {
    private correlationWindow: number = 300000; // 5 minutes
    private logBuffer: Map<string, LogEntry[]> = new Map(); // source -> logs
    private correlationRules: Map<string, (logs: LogEntry[]) => LogEntry[]> = new Map();

    constructor() {
        this.initializeCorrelationRules();
    }

    addLogEntry(logEntry: LogEntry): void {
        const source = logEntry.source;
        if (!this.logBuffer.has(source)) {
            this.logBuffer.set(source, []);
        }

        const logs = this.logBuffer.get(source)!;
        logs.push(logEntry);

        // Clean old entries
        const cutoff = Date.now() - this.correlationWindow;
        this.logBuffer.set(source, logs.filter(log => log.timestamp.getTime() > cutoff));
    }

    findCorrelatedEvents(targetEntry: LogEntry): LogEntry[] {
        const correlatedEvents: LogEntry[] = [];
        const targetTime = targetEntry.timestamp.getTime();
        const timeWindow = 30000; // 30 seconds

        // Search across all sources
        for (const [source, logs] of this.logBuffer) {
            if (source === targetEntry.source) continue; // Skip same source

            const relevantLogs = logs.filter(log => 
                Math.abs(log.timestamp.getTime() - targetTime) <= timeWindow
            );

            // Apply correlation rules
            const ruleName = `${targetEntry.source}_to_${source}`;
            const rule = this.correlationRules.get(ruleName);
            if (rule) {
                correlatedEvents.push(...rule(relevantLogs));
            } else {
                // Default correlation: similar severity or error keywords
                correlatedEvents.push(...this.defaultCorrelation(targetEntry, relevantLogs));
            }
        }

        return correlatedEvents;
    }

    private initializeCorrelationRules(): void {
        // Script to Tycoon correlation
        this.correlationRules.set('scripts_to_tycoon', (logs: LogEntry[]) => {
            return logs.filter(log => 
                log.level === 'error' || 
                log.message.toLowerCase().includes('script') ||
                log.message.toLowerCase().includes('execution')
            );
        });

        // Tycoon to Revit Journal correlation
        this.correlationRules.set('tycoon_to_revit_journal', (logs: LogEntry[]) => {
            return logs.filter(log =>
                log.message.toLowerCase().includes('api') ||
                log.message.toLowerCase().includes('command') ||
                log.message.toLowerCase().includes('transaction')
            );
        });

        // Revit Journal to Scripts correlation
        this.correlationRules.set('revit_journal_to_scripts', (logs: LogEntry[]) => {
            return logs.filter(log =>
                log.message.toLowerCase().includes('error') ||
                log.message.toLowerCase().includes('exception') ||
                log.message.toLowerCase().includes('failed')
            );
        });
    }

    private defaultCorrelation(targetEntry: LogEntry, logs: LogEntry[]): LogEntry[] {
        return logs.filter(log => {
            // Same severity level
            if (log.level === targetEntry.level && targetEntry.level !== 'info') {
                return true;
            }

            // Error keyword correlation
            const errorKeywords = ['error', 'exception', 'failed', 'timeout', 'denied'];
            const targetHasError = errorKeywords.some(keyword => 
                targetEntry.message.toLowerCase().includes(keyword)
            );
            const logHasError = errorKeywords.some(keyword => 
                log.message.toLowerCase().includes(keyword)
            );

            return targetHasError && logHasError;
        });
    }
}

/**
 * Debug suggestion generator
 */
class DebugSuggestionGenerator {
    generateSuggestions(
        logEntry: LogEntry, 
        analysisResult: PatternAnalysisResult,
        context: DebugContext
    ): DebugSuggestion[] {
        const suggestions: DebugSuggestion[] = [];

        // Pattern-based suggestions
        for (const pattern of analysisResult.matchedPatterns) {
            suggestions.push(...this.generatePatternSuggestions(pattern, context));
        }

        // Anomaly-based suggestions
        for (const anomaly of analysisResult.anomalies) {
            suggestions.push(...this.generateAnomalySuggestions(anomaly, context));
        }

        // Context-specific suggestions
        suggestions.push(...this.generateContextSuggestions(logEntry, context));

        // Risk-based suggestions
        if (analysisResult.riskScore > 0.7) {
            suggestions.push(...this.generateHighRiskSuggestions(analysisResult, context));
        }

        return suggestions.sort((a, b) => {
            const priorityOrder = { 'critical': 4, 'high': 3, 'medium': 2, 'low': 1 };
            return priorityOrder[b.priority] - priorityOrder[a.priority];
        });
    }

    private generatePatternSuggestions(pattern: any, context: DebugContext): DebugSuggestion[] {
        const suggestions: DebugSuggestion[] = [];

        if (pattern.name === 'File Access Error') {
            suggestions.push({
                id: `file_access_${Date.now()}`,
                type: 'immediate',
                priority: 'high',
                title: 'Resolve File Access Issue',
                description: 'File access error detected - check permissions and file locks',
                steps: [
                    'Verify file exists and is accessible',
                    'Check file permissions for current user',
                    'Look for file locking by other processes',
                    'Try alternative file path if available',
                    'Implement retry mechanism with exponential backoff'
                ],
                estimatedTime: '5-10 minutes',
                confidence: 0.85,
                relatedPatterns: [pattern.id],
                codeSnippets: [
                    'using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))',
                    'if (File.Exists(filePath) && !IsFileLocked(filePath)) { /* proceed */ }'
                ]
            });
        }

        if (pattern.name === 'Memory Exhaustion') {
            suggestions.push({
                id: `memory_${Date.now()}`,
                type: 'immediate',
                priority: 'critical',
                title: 'Address Memory Exhaustion',
                description: 'Critical memory issue detected - immediate action required',
                steps: [
                    'Check current memory usage and available memory',
                    'Identify memory-intensive operations in recent logs',
                    'Force garbage collection if applicable',
                    'Consider restarting the application',
                    'Implement memory monitoring and alerts'
                ],
                estimatedTime: '2-5 minutes',
                confidence: 0.95,
                relatedPatterns: [pattern.id],
                codeSnippets: [
                    'GC.Collect(); GC.WaitForPendingFinalizers();',
                    'var memoryUsage = GC.GetTotalMemory(false);'
                ]
            });
        }

        return suggestions;
    }

    private generateAnomalySuggestions(anomaly: AnomalyDetection, context: DebugContext): DebugSuggestion[] {
        const suggestions: DebugSuggestion[] = [];

        if (anomaly.anomalyType === 'frequency') {
            suggestions.push({
                id: `freq_anomaly_${Date.now()}`,
                type: 'investigative',
                priority: 'medium',
                title: 'Investigate Frequency Anomaly',
                description: `Unusual frequency spike detected: ${anomaly.description}`,
                steps: [
                    'Analyze recent system changes or deployments',
                    'Check for increased user activity or load',
                    'Review system resources and performance metrics',
                    'Implement rate limiting if appropriate',
                    'Monitor for cascading effects'
                ],
                estimatedTime: '10-15 minutes',
                confidence: anomaly.confidence,
                relatedPatterns: []
            });
        }

        if (anomaly.anomalyType === 'timing') {
            suggestions.push({
                id: `timing_anomaly_${Date.now()}`,
                type: 'investigative',
                priority: 'high',
                title: 'Performance Investigation Required',
                description: `Performance anomaly detected: ${anomaly.description}`,
                steps: [
                    'Profile the slow operation to identify bottlenecks',
                    'Check system resources (CPU, memory, disk I/O)',
                    'Review database query performance if applicable',
                    'Analyze network latency and connectivity',
                    'Consider optimization or scaling solutions'
                ],
                estimatedTime: '15-30 minutes',
                confidence: anomaly.confidence,
                relatedPatterns: []
            });
        }

        return suggestions;
    }

    private generateContextSuggestions(logEntry: LogEntry, context: DebugContext): DebugSuggestion[] {
        const suggestions: DebugSuggestion[] = [];

        // Script-specific suggestions
        if (logEntry.source === 'scripts' && context.scriptName) {
            suggestions.push({
                id: `script_context_${Date.now()}`,
                type: 'investigative',
                priority: 'medium',
                title: `Debug Script: ${context.scriptName}`,
                description: 'Script execution issue detected - review script logic and environment',
                steps: [
                    `Review ${context.scriptName} script logic and parameters`,
                    'Check input data validity and format',
                    'Verify Revit API compatibility and version',
                    'Test script in isolated environment',
                    'Add additional logging for debugging'
                ],
                estimatedTime: '10-20 minutes',
                confidence: 0.7,
                relatedPatterns: [],
                documentationLinks: [
                    'https://www.revitapidocs.com/',
                    'https://thebuildingcoder.typepad.com/'
                ]
            });
        }

        // System resource suggestions
        if (context.systemState.memoryUsage > 80) {
            suggestions.push({
                id: `memory_context_${Date.now()}`,
                type: 'preventive',
                priority: 'medium',
                title: 'Memory Usage Optimization',
                description: 'High memory usage detected - optimize resource usage',
                steps: [
                    'Review memory-intensive operations',
                    'Implement object disposal patterns',
                    'Consider memory pooling for frequent allocations',
                    'Monitor for memory leaks',
                    'Optimize data structures and algorithms'
                ],
                estimatedTime: '20-30 minutes',
                confidence: 0.8,
                relatedPatterns: []
            });
        }

        return suggestions;
    }

    private generateHighRiskSuggestions(result: PatternAnalysisResult, context: DebugContext): DebugSuggestion[] {
        return [{
            id: `high_risk_${Date.now()}`,
            type: 'immediate',
            priority: 'critical',
            title: 'High Risk Situation Detected',
            description: `Critical situation detected with risk score: ${(result.riskScore * 100).toFixed(1)}%`,
            steps: [
                'Immediately review all recent error logs',
                'Check system stability and resource usage',
                'Consider stopping non-essential operations',
                'Notify system administrators',
                'Prepare for potential system restart if needed',
                'Document the incident for post-mortem analysis'
            ],
            estimatedTime: '5-10 minutes',
            confidence: 0.9,
            relatedPatterns: result.matchedPatterns.map(p => p.id)
        }];
    }
}

/**
 * Main intelligent log analyzer
 */
export class IntelligentLogAnalyzer extends EventEmitter {
    private patternEngine: PatternRecognitionEngine;
    private correlationEngine: ContextCorrelationEngine;
    private suggestionGenerator: DebugSuggestionGenerator;
    private activeContexts: Map<string, DebugContext> = new Map();
    private workflowIntegrations: Map<string, WorkflowIntegration> = new Map();
    private alertHistory: SmartAlert[] = [];
    private debugMode: boolean;

    constructor(debugMode: boolean = false) {
        super();
        this.debugMode = debugMode;
        this.patternEngine = new PatternRecognitionEngine(debugMode);
        this.correlationEngine = new ContextCorrelationEngine();
        this.suggestionGenerator = new DebugSuggestionGenerator();

        this.setupPatternEngineEvents();
    }

    /**
     * Analyze log entry with full context awareness
     */
    async analyzeWithContext(
        logEntry: LogEntry, 
        context?: Partial<DebugContext>
    ): Promise<{
        analysis: PatternAnalysisResult;
        suggestions: DebugSuggestion[];
        correlatedEvents: LogEntry[];
        smartAlert?: SmartAlert;
    }> {
        const startTime = performance.now();

        try {
            // Add to correlation engine
            this.correlationEngine.addLogEntry(logEntry);

            // Get pattern analysis
            const analysis = await this.patternEngine.analyzeLogEntry(logEntry);

            // Find correlated events
            const correlatedEvents = this.correlationEngine.findCorrelatedEvents(logEntry);

            // Create or update debug context
            const debugContext = this.getOrCreateContext(logEntry, context);

            // Generate debug suggestions
            const suggestions = this.suggestionGenerator.generateSuggestions(
                logEntry, analysis, debugContext
            );

            // Create smart alert if needed
            let smartAlert: SmartAlert | undefined;
            if (analysis.riskScore > 0.5 || analysis.anomalies.length > 0) {
                smartAlert = this.createSmartAlert(
                    logEntry, analysis, debugContext, suggestions, correlatedEvents
                );
                this.alertHistory.push(smartAlert);
            }

            // Execute workflow integrations
            await this.executeWorkflowIntegrations(analysis, logEntry);

            const analysisTime = performance.now() - startTime;
            this.log(`Context analysis completed in ${analysisTime.toFixed(2)}ms`);

            return {
                analysis,
                suggestions,
                correlatedEvents,
                smartAlert
            };

        } catch (error) {
            this.logError('Context analysis failed', error);
            throw error;
        }
    }

    /**
     * Register workflow integration
     */
    registerWorkflow(workflow: WorkflowIntegration): void {
        this.workflowIntegrations.set(workflow.workflowId, workflow);
        this.log(`Registered workflow: ${workflow.workflowId}`);
    }

    /**
     * Create debug session context
     */
    createDebugSession(sessionId: string, context: Partial<DebugContext>): void {
        const debugContext: DebugContext = {
            sessionId,
            userId: context.userId || 'unknown',
            startTime: new Date(),
            relatedLogs: [],
            errorSequence: [],
            systemState: context.systemState || {
                memoryUsage: 0,
                cpuUsage: 0,
                activeConnections: 0,
                queueDepth: 0
            },
            userActions: [],
            environmentInfo: context.environmentInfo || {},
            ...context
        };

        this.activeContexts.set(sessionId, debugContext);
        this.log(`Created debug session: ${sessionId}`);
    }

    /**
     * Get analysis statistics
     */
    getAnalysisStats(): any {
        const patternStats = this.patternEngine.getAnalysisStats();
        
        return {
            ...patternStats,
            activeContexts: this.activeContexts.size,
            workflowIntegrations: this.workflowIntegrations.size,
            alertHistory: this.alertHistory.length,
            recentAlerts: this.alertHistory.slice(-10),
            correlationEngineStats: {
                sourcesTracked: this.correlationEngine['logBuffer'].size,
                totalLogEntries: Array.from(this.correlationEngine['logBuffer'].values())
                    .reduce((sum, logs) => sum + logs.length, 0)
            }
        };
    }

    private setupPatternEngineEvents(): void {
        this.patternEngine.on('highRiskDetected', (data) => {
            this.emit('highRiskDetected', data);
        });

        this.patternEngine.on('anomalyDetected', (data) => {
            this.emit('anomalyDetected', data);
        });

        this.patternEngine.on('patternLearned', (pattern) => {
            this.emit('patternLearned', pattern);
        });
    }

    private getOrCreateContext(logEntry: LogEntry, context?: Partial<DebugContext>): DebugContext {
        const sessionId = context?.sessionId || 'default';
        
        let debugContext = this.activeContexts.get(sessionId);
        if (!debugContext) {
            debugContext = {
                sessionId,
                userId: context?.userId || 'system',
                startTime: new Date(),
                relatedLogs: [],
                errorSequence: [],
                systemState: {
                    memoryUsage: 0,
                    cpuUsage: 0,
                    activeConnections: 0,
                    queueDepth: 0
                },
                userActions: [],
                environmentInfo: {},
                ...context
            };
            this.activeContexts.set(sessionId, debugContext);
        }

        // Update context with new log entry
        debugContext.relatedLogs.push(logEntry);
        if (logEntry.level === 'error') {
            debugContext.errorSequence.push(logEntry);
        }

        // Keep context size manageable
        if (debugContext.relatedLogs.length > 100) {
            debugContext.relatedLogs = debugContext.relatedLogs.slice(-50);
        }
        if (debugContext.errorSequence.length > 20) {
            debugContext.errorSequence = debugContext.errorSequence.slice(-10);
        }

        return debugContext;
    }

    private createSmartAlert(
        logEntry: LogEntry,
        analysis: PatternAnalysisResult,
        context: DebugContext,
        suggestions: DebugSuggestion[],
        correlatedEvents: LogEntry[]
    ): SmartAlert {
        const severity = analysis.riskScore > 0.8 ? 'critical' :
                        analysis.riskScore > 0.6 ? 'error' :
                        analysis.riskScore > 0.4 ? 'warning' : 'info';

        const category = analysis.matchedPatterns.length > 0 ? 
            analysis.matchedPatterns[0].category : 'system';

        return {
            id: `alert_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
            timestamp: new Date(),
            severity,
            category,
            title: this.generateAlertTitle(analysis, logEntry),
            message: this.generateAlertMessage(analysis, logEntry),
            context,
            suggestions,
            correlatedEvents,
            predictedImpact: {
                scope: analysis.riskScore > 0.7 ? 'system' : 'local',
                timeframe: severity === 'critical' ? 'immediate' : 'short-term',
                description: analysis.predictiveAlerts.join('; ') || 'Impact assessment pending'
            },
            autoResolution: this.assessAutoResolution(analysis, suggestions)
        };
    }

    private generateAlertTitle(analysis: PatternAnalysisResult, logEntry: LogEntry): string {
        if (analysis.matchedPatterns.length > 0) {
            return `${analysis.matchedPatterns[0].name} Detected`;
        }
        if (analysis.anomalies.length > 0) {
            return `${analysis.anomalies[0].anomalyType} Anomaly Detected`;
        }
        return `Log Analysis Alert - ${logEntry.source}`;
    }

    private generateAlertMessage(analysis: PatternAnalysisResult, logEntry: LogEntry): string {
        const parts: string[] = [];
        
        if (analysis.matchedPatterns.length > 0) {
            parts.push(`Pattern: ${analysis.matchedPatterns[0].description}`);
        }
        
        if (analysis.anomalies.length > 0) {
            parts.push(`Anomaly: ${analysis.anomalies[0].description}`);
        }
        
        parts.push(`Risk Score: ${(analysis.riskScore * 100).toFixed(1)}%`);
        
        if (analysis.contextualInsights.length > 0) {
            parts.push(`Context: ${analysis.contextualInsights[0]}`);
        }

        return parts.join(' | ');
    }

    private assessAutoResolution(analysis: PatternAnalysisResult, suggestions: DebugSuggestion[]): {
        possible: boolean;
        confidence: number;
        actions: string[];
    } {
        const immediateSuggestions = suggestions.filter(s => s.type === 'immediate');
        const highConfidenceSuggestions = suggestions.filter(s => s.confidence > 0.8);
        
        const possible = immediateSuggestions.length > 0 && highConfidenceSuggestions.length > 0;
        const confidence = possible ? 
            Math.min(...highConfidenceSuggestions.map(s => s.confidence)) : 0;
        
        const actions = immediateSuggestions.slice(0, 3).map(s => s.title);

        return { possible, confidence, actions };
    }

    private async executeWorkflowIntegrations(analysis: PatternAnalysisResult, logEntry: LogEntry): Promise<void> {
        for (const workflow of this.workflowIntegrations.values()) {
            if (!workflow.isActive) continue;

            const shouldTrigger = this.shouldTriggerWorkflow(workflow, analysis, logEntry);
            if (shouldTrigger) {
                try {
                    await this.executeWorkflow(workflow, analysis, logEntry);
                } catch (error) {
                    this.logError(`Workflow execution failed: ${workflow.workflowId}`, error);
                }
            }
        }
    }

    private shouldTriggerWorkflow(
        workflow: WorkflowIntegration, 
        analysis: PatternAnalysisResult, 
        logEntry: LogEntry
    ): boolean {
        const conditions = workflow.triggerConditions;
        
        // Check risk threshold
        if (analysis.riskScore < conditions.riskThreshold) {
            return false;
        }

        // Check pattern matches
        if (conditions.patterns.length > 0) {
            const hasMatchingPattern = analysis.matchedPatterns.some(pattern =>
                conditions.patterns.includes(pattern.name)
            );
            if (!hasMatchingPattern) return false;
        }

        // Check anomaly types
        if (conditions.anomalyTypes.length > 0) {
            const hasMatchingAnomaly = analysis.anomalies.some(anomaly =>
                conditions.anomalyTypes.includes(anomaly.anomalyType)
            );
            if (!hasMatchingAnomaly) return false;
        }

        return true;
    }

    private async executeWorkflow(
        workflow: WorkflowIntegration, 
        analysis: PatternAnalysisResult, 
        logEntry: LogEntry
    ): Promise<void> {
        this.log(`Executing workflow: ${workflow.workflowId}`);

        for (const action of workflow.actions) {
            try {
                switch (action.type) {
                    case 'notification':
                        await this.sendNotification(action.target, analysis, logEntry);
                        break;
                    case 'automation':
                        await this.executeAutomation(action.target, action.parameters);
                        break;
                    case 'escalation':
                        await this.escalateIssue(action.target, analysis, logEntry);
                        break;
                }

                workflow.executionHistory.push({
                    timestamp: new Date(),
                    trigger: `${analysis.matchedPatterns.map(p => p.name).join(', ')}`,
                    result: 'success',
                    details: `Action ${action.type} executed successfully`
                });

            } catch (error) {
                workflow.executionHistory.push({
                    timestamp: new Date(),
                    trigger: `${analysis.matchedPatterns.map(p => p.name).join(', ')}`,
                    result: 'failure',
                    details: `Action ${action.type} failed: ${error}`
                });
            }
        }
    }

    private async sendNotification(target: string, analysis: PatternAnalysisResult, logEntry: LogEntry): Promise<void> {
        // Implementation would send notification to specified target
        this.log(`Notification sent to ${target} for ${logEntry.source} log`);
    }

    private async executeAutomation(target: string, parameters: any): Promise<void> {
        // Implementation would execute automated action
        this.log(`Automation executed: ${target} with parameters: ${JSON.stringify(parameters)}`);
    }

    private async escalateIssue(target: string, analysis: PatternAnalysisResult, logEntry: LogEntry): Promise<void> {
        // Implementation would escalate issue to specified target
        this.log(`Issue escalated to ${target} for risk score: ${analysis.riskScore}`);
    }

    private log(message: string): void {
        if (this.debugMode) {
            console.log(chalk.magenta(`[IntelligentLogAnalyzer] ${message}`));
        }
    }

    private logError(message: string, error: any): void {
        console.error(chalk.red(`[IntelligentLogAnalyzer] ${message}:`), error);
    }

    /**
     * Clean up old contexts and alerts
     */
    cleanup(): void {
        const cutoff = Date.now() - (24 * 60 * 60 * 1000); // 24 hours

        // Clean old contexts
        for (const [sessionId, context] of this.activeContexts) {
            if (context.startTime.getTime() < cutoff) {
                this.activeContexts.delete(sessionId);
            }
        }

        // Clean old alerts
        this.alertHistory = this.alertHistory.filter(alert => 
            alert.timestamp.getTime() > cutoff
        );

        this.log('Cleanup completed');
    }
}
