/**
 * LogStreamingManager - Coordinates multiple log watchers and manages streaming sessions
 * 
 * Implements the enhanced architecture from dual AI consultation:
 * - Session management with connection lifecycle
 * - Multiple log source coordination
 * - Operational monitoring and KPI tracking
 * - WebSocket message routing for log streams
 */

import { EventEmitter } from 'events';
import { ReliableLogWatcher, LogEntry, LogStreamConfig, StreamMetrics } from './LogStreamer.js';
import { WebSocket } from 'ws';
import { join } from 'path';
import { tmpdir } from 'os';
import { homedir } from 'os';
import chalk from 'chalk';
import { OperationalMonitor as OpMonitor } from '../monitoring/OperationalMonitor.js';
import { SecurityLayer, SecurityConfig } from '../security/SecurityLayer.js';

export interface LogStreamSession {
    id: string;
    config: LogStreamConfig;
    watchers: Map<string, ReliableLogWatcher>;
    connection: WebSocket;
    startTime: Date;
    messageCount: number;
    lastActivity: Date;
}

export interface LogStreamMessage {
    type: 'log_stream_data' | 'log_stream_control' | 'log_stream_status';
    stream_id: string;
    queue_depth?: number;
    requires_ack?: boolean;
    payload: LogEntry | LogStreamConfig | StreamMetrics | any;
}

/**
 * Operational monitor for KPI tracking and alerting
 */
class OperationalMonitor {
    private sessions: Map<string, LogStreamSession>;
    private startTime: Date;

    constructor(sessions: Map<string, LogStreamSession>) {
        this.sessions = sessions;
        this.startTime = new Date();
    }

    generateKPIDashboard(): any {
        const totalSessions = this.sessions.size;
        const activeSessions = Array.from(this.sessions.values()).filter(s => 
            Date.now() - s.lastActivity.getTime() < 30000 // Active in last 30 seconds
        ).length;

        const totalMessages = Array.from(this.sessions.values())
            .reduce((sum, session) => sum + session.messageCount, 0);

        const uptime = Date.now() - this.startTime.getTime();

        return {
            healthStatus: this.calculateOverallHealth(),
            performanceMetrics: {
                totalSessions,
                activeSessions,
                totalMessages,
                uptime,
                averageMessagesPerSession: totalSessions > 0 ? totalMessages / totalSessions : 0
            },
            alerts: this.checkThresholds(),
            recommendations: this.generateOptimizations()
        };
    }

    private calculateOverallHealth(): 'healthy' | 'warning' | 'critical' {
        const memoryUsage = process.memoryUsage().heapUsed / 1024 / 1024; // MB
        
        if (memoryUsage > 100) return 'critical'; // >100MB
        if (memoryUsage > 50) return 'warning';   // >50MB
        return 'healthy';
    }

    private checkThresholds(): Array<{ level: string; message: string }> {
        const alerts: Array<{ level: string; message: string }> = [];
        const memoryUsage = process.memoryUsage().heapUsed / 1024 / 1024;

        if (memoryUsage > 80) {
            alerts.push({ level: 'warning', message: 'High memory usage detected' });
        }

        const staleSessions = Array.from(this.sessions.values()).filter(s =>
            Date.now() - s.lastActivity.getTime() > 300000 // 5 minutes
        );

        if (staleSessions.length > 0) {
            alerts.push({ 
                level: 'warning', 
                message: `${staleSessions.length} stale sessions detected` 
            });
        }

        return alerts;
    }

    private generateOptimizations(): string[] {
        const recommendations: string[] = [];
        const memoryUsage = process.memoryUsage().heapUsed / 1024 / 1024;

        if (memoryUsage > 50) {
            recommendations.push('Consider reducing buffer sizes or session count');
        }

        if (this.sessions.size > 10) {
            recommendations.push('High session count - monitor for performance impact');
        }

        return recommendations;
    }
}

/**
 * Main log streaming manager
 */
export class LogStreamingManager extends EventEmitter {
    private sessions: Map<string, LogStreamSession> = new Map();
    private operationalMonitor: OpMonitor;
    private securityLayer: SecurityLayer;
    private debugMode: boolean;

    constructor(debugMode: boolean = false) {
        super();
        this.debugMode = debugMode;
        this.operationalMonitor = new OpMonitor();

        // Initialize security layer with production config
        const securityConfig: SecurityConfig = {
            enableTLS: false, // Will be enabled in production with proper certificates
            enableUserAuth: true,
            enableAuditLogging: true,
            enablePiiRedaction: true,
            complianceMode: 'gdpr',
            sessionTimeoutMinutes: 60,
            maxFailedAttempts: 5,
            rateLimitRequestsPerMinute: 100
        };
        this.securityLayer = new SecurityLayer(securityConfig);

        // Start operational monitoring
        this.operationalMonitor.startMonitoring(5000); // 5-second intervals

        // Cleanup stale sessions every 5 minutes
        setInterval(() => this.cleanupStaleSessions(), 5 * 60 * 1000);
    }

    /**
     * Start a new log streaming session
     */
    async startLogStream(connection: WebSocket, config: LogStreamConfig): Promise<string> {
        const sessionId = this.generateSessionId();
        
        this.log(`🚀 Starting log stream session: ${sessionId}`);

        try {
            const session: LogStreamSession = {
                id: sessionId,
                config,
                watchers: new Map(),
                connection,
                startTime: new Date(),
                messageCount: 0,
                lastActivity: new Date()
            };

            // Create watchers for each requested source
            for (const source of config.sources) {
                const filePath = this.getLogFilePath(source);
                const watcher = new ReliableLogWatcher(filePath, config, this.debugMode);

                // Set up event handlers
                watcher.on('logEntry', (entry: LogEntry) => {
                    this.handleLogEntry(sessionId, entry);
                });

                watcher.on('error', (error: Error) => {
                    this.handleWatcherError(sessionId, source, error);
                });

                // Start the watcher
                await watcher.start();
                session.watchers.set(source, watcher);
            }

            this.sessions.set(sessionId, session);

            // Send initial status message
            this.sendStreamMessage(connection, {
                type: 'log_stream_control',
                stream_id: sessionId,
                payload: { status: 'started', sources: config.sources }
            });

            this.log(`✅ Log stream session started: ${sessionId} with ${config.sources.length} sources`);
            return sessionId;

        } catch (error) {
            this.logError(`Failed to start log stream session: ${sessionId}`, error);
            throw error;
        }
    }

    /**
     * Stop a log streaming session
     */
    async stopLogStream(sessionId: string): Promise<void> {
        const session = this.sessions.get(sessionId);
        if (!session) {
            throw new Error(`Log stream session not found: ${sessionId}`);
        }

        this.log(`🛑 Stopping log stream session: ${sessionId}`);

        try {
            // Stop all watchers
            for (const [source, watcher] of session.watchers) {
                watcher.stop();
                this.log(`  ✅ Stopped watcher for: ${source}`);
            }

            // Send final status message
            this.sendStreamMessage(session.connection, {
                type: 'log_stream_control',
                stream_id: sessionId,
                payload: { status: 'stopped' }
            });

            // Remove session
            this.sessions.delete(sessionId);
            this.log(`✅ Log stream session stopped: ${sessionId}`);

        } catch (error) {
            this.logError(`Error stopping log stream session: ${sessionId}`, error);
            throw error;
        }
    }

    /**
     * Get status for a specific stream or all streams
     */
    getStreamStatus(sessionId?: string): any {
        if (sessionId) {
            const session = this.sessions.get(sessionId);
            if (!session) {
                throw new Error(`Log stream session not found: ${sessionId}`);
            }

            return {
                id: session.id,
                config: session.config,
                startTime: session.startTime,
                messageCount: session.messageCount,
                lastActivity: session.lastActivity,
                sources: Array.from(session.watchers.keys()),
                metrics: this.getSessionMetrics(session)
            };
        }

        // Return all sessions status
        return {
            totalSessions: this.sessions.size,
            sessions: Array.from(this.sessions.values()).map(session => ({
                id: session.id,
                sources: Array.from(session.watchers.keys()),
                messageCount: session.messageCount,
                lastActivity: session.lastActivity
            })),
            kpiDashboard: this.operationalMonitor.getKPIDashboard()
        };
    }

    /**
     * Handle connection close - cleanup associated sessions
     */
    handleConnectionClose(connection: WebSocket): void {
        const sessionsToClose: string[] = [];

        for (const [sessionId, session] of this.sessions) {
            if (session.connection === connection) {
                sessionsToClose.push(sessionId);
            }
        }

        for (const sessionId of sessionsToClose) {
            this.log(`🔌 Connection closed, stopping session: ${sessionId}`);
            this.stopLogStream(sessionId).catch(error => {
                this.logError(`Error stopping session on connection close: ${sessionId}`, error);
            });
        }
    }

    private handleLogEntry(sessionId: string, entry: LogEntry): void {
        const session = this.sessions.get(sessionId);
        if (!session) return;

        session.messageCount++;
        session.lastActivity = new Date();

        // Send log entry to client
        this.sendStreamMessage(session.connection, {
            type: 'log_stream_data',
            stream_id: sessionId,
            queue_depth: entry.metadata?.queueDepth,
            payload: entry
        });
    }

    private handleWatcherError(sessionId: string, source: string, error: Error): void {
        this.logError(`Watcher error for session ${sessionId}, source ${source}`, error);
        
        const session = this.sessions.get(sessionId);
        if (session) {
            this.sendStreamMessage(session.connection, {
                type: 'log_stream_control',
                stream_id: sessionId,
                payload: { 
                    status: 'error', 
                    source, 
                    error: error.message 
                }
            });
        }
    }

    private sendStreamMessage(connection: WebSocket, message: LogStreamMessage): void {
        if (connection.readyState === WebSocket.OPEN) {
            try {
                connection.send(JSON.stringify(message));
            } catch (error) {
                this.logError('Failed to send stream message', error);
            }
        }
    }

    private getLogFilePath(source: string): string {
        const today = new Date().toISOString().slice(0, 10).replace(/-/g, '');
        
        switch (source) {
            case 'tycoon':
                return join(tmpdir(), `Tycoon_${today}.log`);
            case 'scripts':
                return join(tmpdir(), 'TycoonScriptOutput', `ScriptOutput_${today}.log`);
            case 'revit_journal':
                // Find latest Revit journal - simplified for now
                return join(homedir(), 'AppData', 'Local', 'Autodesk', 'Revit', 'Autodesk Revit 2024', 'Journals', `journal.${today}.txt`);
            default:
                throw new Error(`Unknown log source: ${source}`);
        }
    }

    private getSessionMetrics(session: LogStreamSession): StreamMetrics {
        const watchers = Array.from(session.watchers.values());
        const metrics = watchers.map(w => w.getMetrics());
        
        return {
            heapUsage: Math.max(...metrics.map(m => m.heapUsage)),
            queueDepth: metrics.reduce((sum, m) => sum + m.queueDepth, 0),
            droppedMessages: metrics.reduce((sum, m) => sum + m.droppedMessages, 0),
            averageLatency: metrics.reduce((sum, m) => sum + m.averageLatency, 0) / metrics.length,
            connectionUptime: Date.now() - session.startTime.getTime(),
            errorRate: 0 // TODO: Implement error rate tracking
        };
    }

    private cleanupStaleSessions(): void {
        const staleThreshold = 10 * 60 * 1000; // 10 minutes
        const now = Date.now();

        for (const [sessionId, session] of this.sessions) {
            if (now - session.lastActivity.getTime() > staleThreshold) {
                this.log(`🧹 Cleaning up stale session: ${sessionId}`);
                this.stopLogStream(sessionId).catch(error => {
                    this.logError(`Error cleaning up stale session: ${sessionId}`, error);
                });
            }
        }
    }

    private generateSessionId(): string {
        return `stream_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
    }

    private log(message: string): void {
        if (this.debugMode) {
            console.log(chalk.green(`[LogStreamingManager] ${message}`));
        }
    }

    private logError(message: string, error: any): void {
        console.error(chalk.red(`[LogStreamingManager] ${message}:`), error);
    }
}
