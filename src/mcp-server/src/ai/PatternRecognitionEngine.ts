/**
 * PatternRecognitionEngine - AI-powered log pattern recognition and proactive error detection
 * 
 * Implements Phase 3 AI integration features:
 * - Pattern recognition for proactive error detection in log streams
 * - ML-based anomaly detection with context-aware analysis
 * - Predictive failure detection based on historical patterns
 * - Automated recovery suggestions with error context
 */

import { EventEmitter } from 'events';
import { LogEntry } from '../streaming/LogStreamer.js';
import chalk from 'chalk';

export interface ErrorPattern {
    id: string;
    name: string;
    description: string;
    pattern: RegExp;
    severity: 'low' | 'medium' | 'high' | 'critical';
    category: 'performance' | 'security' | 'functional' | 'system';
    confidence: number;
    occurrences: number;
    lastSeen: Date;
    recoveryActions: string[];
    relatedPatterns: string[];
}

export interface AnomalyDetection {
    id: string;
    timestamp: Date;
    logEntry: LogEntry;
    anomalyType: 'frequency' | 'pattern' | 'sequence' | 'timing';
    confidence: number;
    severity: 'low' | 'medium' | 'high' | 'critical';
    description: string;
    context: {
        precedingEvents: LogEntry[];
        relatedPatterns: string[];
        systemState: any;
    };
    predictedImpact: string;
    recommendedActions: string[];
}

export interface PatternAnalysisResult {
    matchedPatterns: ErrorPattern[];
    anomalies: AnomalyDetection[];
    riskScore: number;
    recommendations: string[];
    contextualInsights: string[];
    predictiveAlerts: string[];
}

/**
 * Machine learning-based anomaly detector
 */
class AnomalyDetector {
    private frequencyBaseline: Map<string, { count: number; avgInterval: number }> = new Map();
    private sequencePatterns: Map<string, string[]> = new Map();
    private timingBaseline: Map<string, { avgDuration: number; stdDev: number }> = new Map();
    private recentEvents: LogEntry[] = [];
    private maxHistorySize: number = 1000;

    updateBaseline(logEntry: LogEntry): void {
        // Update frequency baseline
        const key = this.extractLogKey(logEntry);
        const existing = this.frequencyBaseline.get(key);
        if (existing) {
            existing.count++;
            const timeDiff = Date.now() - logEntry.timestamp.getTime();
            existing.avgInterval = (existing.avgInterval + timeDiff) / 2;
        } else {
            this.frequencyBaseline.set(key, { count: 1, avgInterval: 0 });
        }

        // Update sequence patterns
        this.updateSequencePatterns(logEntry);

        // Update timing baseline for performance logs
        this.updateTimingBaseline(logEntry);

        // Maintain recent events history
        this.recentEvents.push(logEntry);
        if (this.recentEvents.length > this.maxHistorySize) {
            this.recentEvents.shift();
        }
    }

    detectAnomalies(logEntry: LogEntry): AnomalyDetection[] {
        const anomalies: AnomalyDetection[] = [];

        // Frequency anomaly detection
        const frequencyAnomaly = this.detectFrequencyAnomaly(logEntry);
        if (frequencyAnomaly) anomalies.push(frequencyAnomaly);

        // Pattern anomaly detection
        const patternAnomaly = this.detectPatternAnomaly(logEntry);
        if (patternAnomaly) anomalies.push(patternAnomaly);

        // Sequence anomaly detection
        const sequenceAnomaly = this.detectSequenceAnomaly(logEntry);
        if (sequenceAnomaly) anomalies.push(sequenceAnomaly);

        // Timing anomaly detection
        const timingAnomaly = this.detectTimingAnomaly(logEntry);
        if (timingAnomaly) anomalies.push(timingAnomaly);

        return anomalies;
    }

    private extractLogKey(logEntry: LogEntry): string {
        // Extract key features from log entry for pattern matching
        const message = logEntry.message.toLowerCase();
        
        // Remove timestamps, IDs, and variable data
        const normalized = message
            .replace(/\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/g, '[TIMESTAMP]')
            .replace(/\b\d+\b/g, '[NUMBER]')
            .replace(/\b[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}\b/gi, '[UUID]')
            .replace(/\b\w+_\d+\b/g, '[ID]');

        return `${logEntry.level}:${logEntry.source}:${normalized}`;
    }

    private detectFrequencyAnomaly(logEntry: LogEntry): AnomalyDetection | null {
        const key = this.extractLogKey(logEntry);
        const baseline = this.frequencyBaseline.get(key);
        
        if (!baseline || baseline.count < 10) return null; // Need baseline data

        // Check if frequency is unusually high
        const recentCount = this.recentEvents.filter(e => 
            this.extractLogKey(e) === key && 
            Date.now() - e.timestamp.getTime() < 60000 // Last minute
        ).length;

        const expectedCount = 60000 / baseline.avgInterval; // Expected in last minute
        const anomalyThreshold = expectedCount * 3; // 3x normal frequency

        if (recentCount > anomalyThreshold) {
            return {
                id: `freq_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
                timestamp: new Date(),
                logEntry,
                anomalyType: 'frequency',
                confidence: Math.min(0.9, recentCount / anomalyThreshold),
                severity: recentCount > anomalyThreshold * 2 ? 'high' : 'medium',
                description: `Unusual frequency spike: ${recentCount} occurrences vs expected ${expectedCount.toFixed(1)}`,
                context: {
                    precedingEvents: this.recentEvents.slice(-5),
                    relatedPatterns: [],
                    systemState: { baseline: baseline, recentCount, expectedCount }
                },
                predictedImpact: 'System performance degradation or cascading failures',
                recommendedActions: [
                    'Investigate root cause of frequency spike',
                    'Check system resources and performance',
                    'Consider implementing rate limiting'
                ]
            };
        }

        return null;
    }

    private detectPatternAnomaly(logEntry: LogEntry): AnomalyDetection | null {
        // Detect unusual patterns in log messages
        const message = logEntry.message.toLowerCase();
        
        // Check for suspicious patterns
        const suspiciousPatterns = [
            { pattern: /failed.*\d+.*times/i, severity: 'high' as const, description: 'Multiple failure attempts detected' },
            { pattern: /timeout.*exceeded/i, severity: 'medium' as const, description: 'Timeout threshold exceeded' },
            { pattern: /memory.*leak/i, severity: 'critical' as const, description: 'Potential memory leak detected' },
            { pattern: /deadlock.*detected/i, severity: 'critical' as const, description: 'Deadlock condition detected' },
            { pattern: /security.*violation/i, severity: 'high' as const, description: 'Security violation detected' }
        ];

        for (const { pattern, severity, description } of suspiciousPatterns) {
            if (pattern.test(message)) {
                return {
                    id: `pattern_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
                    timestamp: new Date(),
                    logEntry,
                    anomalyType: 'pattern',
                    confidence: 0.8,
                    severity,
                    description,
                    context: {
                        precedingEvents: this.recentEvents.slice(-3),
                        relatedPatterns: [pattern.source],
                        systemState: {}
                    },
                    predictedImpact: this.getPredictedImpact(severity),
                    recommendedActions: this.getRecommendedActions(description)
                };
            }
        }

        return null;
    }

    private detectSequenceAnomaly(logEntry: LogEntry): AnomalyDetection | null {
        // Detect unusual sequences of events
        if (this.recentEvents.length < 3) return null;

        const recentSequence = this.recentEvents.slice(-3).map(e => this.extractLogKey(e));
        const sequenceKey = recentSequence.join(' -> ');

        // Check for known problematic sequences
        const problematicSequences = [
            'error -> error -> error', // Error cascade
            'warning -> error -> critical', // Escalating severity
            'timeout -> retry -> timeout' // Retry loop
        ];

        for (const problematic of problematicSequences) {
            if (sequenceKey.includes(problematic)) {
                return {
                    id: `seq_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
                    timestamp: new Date(),
                    logEntry,
                    anomalyType: 'sequence',
                    confidence: 0.75,
                    severity: 'high',
                    description: `Problematic sequence detected: ${problematic}`,
                    context: {
                        precedingEvents: this.recentEvents.slice(-3),
                        relatedPatterns: [problematic],
                        systemState: { sequence: recentSequence }
                    },
                    predictedImpact: 'System instability or cascading failures',
                    recommendedActions: [
                        'Investigate sequence root cause',
                        'Implement circuit breaker pattern',
                        'Review error handling logic'
                    ]
                };
            }
        }

        return null;
    }

    private detectTimingAnomaly(logEntry: LogEntry): AnomalyDetection | null {
        // Extract timing information from performance logs
        const timingMatch = logEntry.message.match(/(\d+)ms|(\d+)\s*seconds?/i);
        if (!timingMatch) return null;

        const duration = timingMatch[1] ? parseInt(timingMatch[1]) : parseInt(timingMatch[2]) * 1000;
        const key = this.extractLogKey(logEntry);
        const baseline = this.timingBaseline.get(key);

        if (!baseline) return null;

        const threshold = baseline.avgDuration + (2 * baseline.stdDev);
        if (duration > threshold) {
            return {
                id: `timing_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
                timestamp: new Date(),
                logEntry,
                anomalyType: 'timing',
                confidence: Math.min(0.9, duration / threshold),
                severity: duration > threshold * 2 ? 'high' : 'medium',
                description: `Performance anomaly: ${duration}ms vs expected ${baseline.avgDuration.toFixed(1)}ms`,
                context: {
                    precedingEvents: this.recentEvents.slice(-2),
                    relatedPatterns: [],
                    systemState: { baseline, actualDuration: duration, threshold }
                },
                predictedImpact: 'Performance degradation affecting user experience',
                recommendedActions: [
                    'Profile performance bottlenecks',
                    'Check system resources',
                    'Optimize slow operations'
                ]
            };
        }

        return null;
    }

    private updateSequencePatterns(logEntry: LogEntry): void {
        if (this.recentEvents.length < 2) return;

        const key = this.extractLogKey(logEntry);
        const prevKey = this.extractLogKey(this.recentEvents[this.recentEvents.length - 1]);
        
        if (!this.sequencePatterns.has(prevKey)) {
            this.sequencePatterns.set(prevKey, []);
        }
        this.sequencePatterns.get(prevKey)!.push(key);
    }

    private updateTimingBaseline(logEntry: LogEntry): void {
        const timingMatch = logEntry.message.match(/(\d+)ms|(\d+)\s*seconds?/i);
        if (!timingMatch) return;

        const duration = timingMatch[1] ? parseInt(timingMatch[1]) : parseInt(timingMatch[2]) * 1000;
        const key = this.extractLogKey(logEntry);
        
        const existing = this.timingBaseline.get(key);
        if (existing) {
            const newAvg = (existing.avgDuration + duration) / 2;
            const newStdDev = Math.sqrt(((existing.stdDev ** 2) + ((duration - newAvg) ** 2)) / 2);
            this.timingBaseline.set(key, { avgDuration: newAvg, stdDev: newStdDev });
        } else {
            this.timingBaseline.set(key, { avgDuration: duration, stdDev: duration * 0.1 });
        }
    }

    private getPredictedImpact(severity: string): string {
        switch (severity) {
            case 'critical': return 'System failure or data loss imminent';
            case 'high': return 'Service degradation or functionality loss';
            case 'medium': return 'Performance impact or user experience degradation';
            case 'low': return 'Minor impact with potential for escalation';
            default: return 'Impact assessment pending';
        }
    }

    private getRecommendedActions(description: string): string[] {
        if (description.includes('memory leak')) {
            return ['Profile memory usage', 'Check for object retention', 'Restart affected services'];
        }
        if (description.includes('deadlock')) {
            return ['Analyze lock ordering', 'Implement timeout mechanisms', 'Review concurrent code'];
        }
        if (description.includes('security')) {
            return ['Investigate security breach', 'Review access logs', 'Implement additional security measures'];
        }
        return ['Investigate root cause', 'Monitor system health', 'Implement preventive measures'];
    }
}

/**
 * Main pattern recognition engine
 */
export class PatternRecognitionEngine extends EventEmitter {
    private errorPatterns: Map<string, ErrorPattern> = new Map();
    private anomalyDetector: AnomalyDetector;
    private analysisHistory: PatternAnalysisResult[] = [];
    private isLearning: boolean = true;
    private debugMode: boolean;

    constructor(debugMode: boolean = false) {
        super();
        this.debugMode = debugMode;
        this.anomalyDetector = new AnomalyDetector();
        this.initializeKnownPatterns();
    }

    /**
     * Analyze log entry for patterns and anomalies
     */
    async analyzeLogEntry(logEntry: LogEntry): Promise<PatternAnalysisResult> {
        const startTime = performance.now();

        try {
            // Update ML baselines if in learning mode
            if (this.isLearning) {
                this.anomalyDetector.updateBaseline(logEntry);
            }

            // Match against known error patterns
            const matchedPatterns = this.matchErrorPatterns(logEntry);

            // Detect anomalies using ML
            const anomalies = this.anomalyDetector.detectAnomalies(logEntry);

            // Calculate risk score
            const riskScore = this.calculateRiskScore(matchedPatterns, anomalies);

            // Generate recommendations
            const recommendations = this.generateRecommendations(matchedPatterns, anomalies);

            // Generate contextual insights
            const contextualInsights = this.generateContextualInsights(logEntry, matchedPatterns);

            // Generate predictive alerts
            const predictiveAlerts = this.generatePredictiveAlerts(anomalies);

            const result: PatternAnalysisResult = {
                matchedPatterns,
                anomalies,
                riskScore,
                recommendations,
                contextualInsights,
                predictiveAlerts
            };

            // Store analysis history
            this.analysisHistory.push(result);
            if (this.analysisHistory.length > 100) {
                this.analysisHistory.shift();
            }

            const analysisTime = performance.now() - startTime;
            this.log(`Analysis completed in ${analysisTime.toFixed(2)}ms - Risk: ${riskScore.toFixed(2)}`);

            // Emit events for high-risk situations
            if (riskScore > 0.7) {
                this.emit('highRiskDetected', { logEntry, result });
            }

            if (anomalies.length > 0) {
                this.emit('anomalyDetected', { logEntry, anomalies });
            }

            return result;

        } catch (error) {
            this.logError('Pattern analysis failed', error);
            return {
                matchedPatterns: [],
                anomalies: [],
                riskScore: 0,
                recommendations: ['Pattern analysis failed - manual review required'],
                contextualInsights: [],
                predictiveAlerts: []
            };
        }
    }

    /**
     * Learn new pattern from user feedback
     */
    learnPattern(logEntry: LogEntry, patternName: string, severity: ErrorPattern['severity']): void {
        const pattern: ErrorPattern = {
            id: `learned_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
            name: patternName,
            description: `User-defined pattern: ${patternName}`,
            pattern: new RegExp(this.escapeRegExp(logEntry.message), 'i'),
            severity,
            category: 'functional',
            confidence: 0.8,
            occurrences: 1,
            lastSeen: new Date(),
            recoveryActions: [],
            relatedPatterns: []
        };

        this.errorPatterns.set(pattern.id, pattern);
        this.log(`Learned new pattern: ${patternName}`);
        this.emit('patternLearned', pattern);
    }

    /**
     * Get analysis statistics
     */
    getAnalysisStats(): any {
        const totalAnalyses = this.analysisHistory.length;
        const highRiskCount = this.analysisHistory.filter(a => a.riskScore > 0.7).length;
        const anomalyCount = this.analysisHistory.reduce((sum, a) => sum + a.anomalies.length, 0);
        const patternMatchCount = this.analysisHistory.reduce((sum, a) => sum + a.matchedPatterns.length, 0);

        return {
            totalAnalyses,
            highRiskCount,
            anomalyCount,
            patternMatchCount,
            riskRate: totalAnalyses > 0 ? (highRiskCount / totalAnalyses) : 0,
            anomalyRate: totalAnalyses > 0 ? (anomalyCount / totalAnalyses) : 0,
            knownPatterns: this.errorPatterns.size,
            learningEnabled: this.isLearning
        };
    }

    private initializeKnownPatterns(): void {
        const knownPatterns: Omit<ErrorPattern, 'id' | 'occurrences' | 'lastSeen'>[] = [
            {
                name: 'File Access Error',
                description: 'File cannot be accessed due to permissions or locking',
                pattern: /(?:access.*denied|file.*locked|permission.*denied)/i,
                severity: 'medium',
                category: 'system',
                confidence: 0.9,
                recoveryActions: ['Check file permissions', 'Retry after delay', 'Use alternative file path'],
                relatedPatterns: ['file_not_found', 'io_error']
            },
            {
                name: 'Memory Exhaustion',
                description: 'System running out of available memory',
                pattern: /(?:out.*of.*memory|memory.*exhausted|heap.*overflow)/i,
                severity: 'critical',
                category: 'performance',
                confidence: 0.95,
                recoveryActions: ['Increase memory allocation', 'Optimize memory usage', 'Restart application'],
                relatedPatterns: ['gc_pressure', 'memory_leak']
            },
            {
                name: 'Network Timeout',
                description: 'Network operation timed out',
                pattern: /(?:network.*timeout|connection.*timeout|request.*timeout)/i,
                severity: 'medium',
                category: 'system',
                confidence: 0.85,
                recoveryActions: ['Retry with exponential backoff', 'Check network connectivity', 'Increase timeout values'],
                relatedPatterns: ['connection_failed', 'network_error']
            },
            {
                name: 'Database Connection Error',
                description: 'Cannot connect to database',
                pattern: /(?:database.*connection.*failed|db.*connection.*error|sql.*connection.*timeout)/i,
                severity: 'high',
                category: 'system',
                confidence: 0.9,
                recoveryActions: ['Check database server status', 'Verify connection string', 'Implement connection pooling'],
                relatedPatterns: ['sql_error', 'connection_pool_exhausted']
            },
            {
                name: 'Script Execution Error',
                description: 'Script failed to execute properly',
                pattern: /(?:script.*failed|execution.*error|runtime.*exception)/i,
                severity: 'medium',
                category: 'functional',
                confidence: 0.8,
                recoveryActions: ['Review script logic', 'Check input parameters', 'Validate environment setup'],
                relatedPatterns: ['syntax_error', 'runtime_error']
            }
        ];

        knownPatterns.forEach((pattern, index) => {
            const fullPattern: ErrorPattern = {
                ...pattern,
                id: `builtin_${index}`,
                occurrences: 0,
                lastSeen: new Date()
            };
            this.errorPatterns.set(fullPattern.id, fullPattern);
        });

        this.log(`Initialized ${knownPatterns.length} known error patterns`);
    }

    private matchErrorPatterns(logEntry: LogEntry): ErrorPattern[] {
        const matches: ErrorPattern[] = [];

        for (const pattern of this.errorPatterns.values()) {
            if (pattern.pattern.test(logEntry.message)) {
                pattern.occurrences++;
                pattern.lastSeen = new Date();
                matches.push(pattern);
            }
        }

        return matches;
    }

    private calculateRiskScore(patterns: ErrorPattern[], anomalies: AnomalyDetection[]): number {
        let score = 0;

        // Pattern-based risk
        for (const pattern of patterns) {
            const severityWeight = {
                'low': 0.1,
                'medium': 0.3,
                'high': 0.6,
                'critical': 1.0
            };
            score += severityWeight[pattern.severity] * pattern.confidence;
        }

        // Anomaly-based risk
        for (const anomaly of anomalies) {
            const severityWeight = {
                'low': 0.2,
                'medium': 0.4,
                'high': 0.7,
                'critical': 1.0
            };
            score += severityWeight[anomaly.severity] * anomaly.confidence;
        }

        return Math.min(1.0, score);
    }

    private generateRecommendations(patterns: ErrorPattern[], anomalies: AnomalyDetection[]): string[] {
        const recommendations = new Set<string>();

        // Add pattern-based recommendations
        patterns.forEach(pattern => {
            pattern.recoveryActions.forEach(action => recommendations.add(action));
        });

        // Add anomaly-based recommendations
        anomalies.forEach(anomaly => {
            anomaly.recommendedActions.forEach(action => recommendations.add(action));
        });

        // Add general recommendations based on analysis
        if (patterns.length > 2) {
            recommendations.add('Multiple error patterns detected - investigate system stability');
        }

        if (anomalies.length > 1) {
            recommendations.add('Multiple anomalies detected - perform comprehensive system check');
        }

        return Array.from(recommendations);
    }

    private generateContextualInsights(logEntry: LogEntry, patterns: ErrorPattern[]): string[] {
        const insights: string[] = [];

        // Source-specific insights
        switch (logEntry.source) {
            case 'scripts':
                insights.push('Script execution context - check input parameters and environment');
                break;
            case 'tycoon':
                insights.push('Core system context - monitor system resources and stability');
                break;
            case 'revit_journal':
                insights.push('Revit integration context - verify add-in compatibility and API usage');
                break;
        }

        // Pattern-specific insights
        if (patterns.some(p => p.category === 'performance')) {
            insights.push('Performance-related issue detected - monitor system resources');
        }

        if (patterns.some(p => p.category === 'security')) {
            insights.push('Security-related issue detected - review access controls and audit logs');
        }

        // Time-based insights
        const hour = logEntry.timestamp.getHours();
        if (hour >= 9 && hour <= 17) {
            insights.push('Occurred during business hours - high user impact potential');
        } else {
            insights.push('Occurred outside business hours - maintenance window opportunity');
        }

        return insights;
    }

    private generatePredictiveAlerts(anomalies: AnomalyDetection[]): string[] {
        const alerts: string[] = [];

        for (const anomaly of anomalies) {
            if (anomaly.confidence > 0.8) {
                alerts.push(`High confidence ${anomaly.anomalyType} anomaly: ${anomaly.predictedImpact}`);
            }

            if (anomaly.severity === 'critical') {
                alerts.push(`Critical anomaly detected: Immediate attention required`);
            }
        }

        return alerts;
    }

    private escapeRegExp(string: string): string {
        return string.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    }

    private log(message: string): void {
        if (this.debugMode) {
            console.log(chalk.cyan(`[PatternRecognitionEngine] ${message}`));
        }
    }

    private logError(message: string, error: any): void {
        console.error(chalk.red(`[PatternRecognitionEngine] ${message}:`), error);
    }

    /**
     * Enable or disable learning mode
     */
    setLearningMode(enabled: boolean): void {
        this.isLearning = enabled;
        this.log(`Learning mode ${enabled ? 'enabled' : 'disabled'}`);
    }

    /**
     * Export learned patterns for backup
     */
    exportPatterns(): any {
        return Array.from(this.errorPatterns.values()).map(pattern => ({
            ...pattern,
            pattern: pattern.pattern.source
        }));
    }

    /**
     * Import patterns from backup
     */
    importPatterns(patterns: any[]): void {
        patterns.forEach(patternData => {
            const pattern: ErrorPattern = {
                ...patternData,
                pattern: new RegExp(patternData.pattern, 'i')
            };
            this.errorPatterns.set(pattern.id, pattern);
        });
        
        this.log(`Imported ${patterns.length} patterns`);
    }
}
