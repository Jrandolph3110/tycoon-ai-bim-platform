/**
 * LogStreamer - Real-time log file streaming with hybrid FileSystemWatcher + polling
 * 
 * Implements Zen's recommended architecture with ChatGPT's production hardening:
 * - Hybrid file watching (FileSystemWatcher + polling backup)
 * - Back-pressure mechanism with queue depth monitoring
 * - PII redaction and security controls
 * - Robust error handling and recovery
 */

import { EventEmitter } from 'events';
import { watch, FSWatcher, readFile, stat } from 'fs';
import { promisify } from 'util';
import chalk from 'chalk';
import { NetworkRecoveryManager, RecoveryConfig } from './ErrorRecoveryManager.js';

const readFileAsync = promisify(readFile);
const statAsync = promisify(stat);

export interface LogEntry {
    timestamp: Date;
    level: 'info' | 'warning' | 'error' | 'success';
    source: 'tycoon' | 'scripts' | 'revit_journal';
    message: string;
    metadata?: Record<string, any>;
}

export interface LogStreamConfig {
    sources: ('tycoon' | 'scripts' | 'revit_journal')[];
    filterLevel: 'all' | 'error' | 'warning' | 'info' | 'success';
    bufferSize: number;
    followMode: boolean;
    includeHistory: boolean;
    enablePiiRedaction: boolean;
    maxQueueDepth: number;
}

export interface StreamMetrics {
    heapUsage: number;
    queueDepth: number;
    droppedMessages: number;
    averageLatency: number;
    connectionUptime: number;
    errorRate: number;
}

/**
 * Enhanced security manager for PII redaction and access control
 */
class LogSecurityManager {
    private piiPatterns: RegExp[] = [
        /\b\d{3}-\d{2}-\d{4}\b/g,           // SSN
        /\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b/g, // Email
        /\b\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b/g,          // Credit card
        /\b(?:password|pwd|token|key|secret)[\s:=]+\S+/gi,       // Credentials
        /\b(?:api[_-]?key|access[_-]?token|bearer\s+\S+)/gi,     // API keys/tokens
        /\b(?:username|user|login)[\s:=]+\S+/gi,                 // Usernames
        /\b(?:phone|tel|mobile)[\s:=]*[\+]?[\d\s\-\(\)]{10,}/g,  // Phone numbers
        /\b(?:ip|address)[\s:=]*\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}/gi, // IP addresses
        /\b[A-Z0-9]{20,}\b/g,                                    // Long alphanumeric strings (likely tokens)
        /\b(?:guid|uuid)[\s:=]*[a-f0-9\-]{32,36}/gi             // GUIDs/UUIDs
    ];

    private userPermissions: Map<string, Set<string>> = new Map();
    private auditLog: Array<{timestamp: Date, userId: string, action: string, source: string}> = [];

    constructor() {
        this.initializeDefaultPermissions();
    }

    private initializeDefaultPermissions(): void {
        // Default permissions - in production, load from configuration
        this.userPermissions.set('admin', new Set(['tycoon', 'scripts', 'revit_journal']));
        this.userPermissions.set('developer', new Set(['tycoon', 'scripts']));
        this.userPermissions.set('user', new Set(['scripts']));
    }

    redactSensitiveData(logEntry: string): string {
        let sanitized = logEntry;
        this.piiPatterns.forEach(pattern => {
            sanitized = sanitized.replace(pattern, '[REDACTED]');
        });
        return sanitized;
    }

    checkUserPermissions(userId: string, logSource: string): boolean {
        const userPerms = this.userPermissions.get(userId);
        const hasPermission = userPerms ? userPerms.has(logSource) : false;

        // Log access attempt for audit
        this.auditLog.push({
            timestamp: new Date(),
            userId,
            action: hasPermission ? 'ACCESS_GRANTED' : 'ACCESS_DENIED',
            source: logSource
        });

        // Keep audit log size manageable
        if (this.auditLog.length > 1000) {
            this.auditLog.shift();
        }

        return hasPermission;
    }

    addUserPermission(userId: string, logSource: string): void {
        if (!this.userPermissions.has(userId)) {
            this.userPermissions.set(userId, new Set());
        }
        this.userPermissions.get(userId)!.add(logSource);
    }

    removeUserPermission(userId: string, logSource: string): void {
        const userPerms = this.userPermissions.get(userId);
        if (userPerms) {
            userPerms.delete(logSource);
        }
    }

    getAuditLog(): Array<{timestamp: Date, userId: string, action: string, source: string}> {
        return [...this.auditLog];
    }

    getUserPermissions(userId: string): string[] {
        const perms = this.userPermissions.get(userId);
        return perms ? Array.from(perms) : [];
    }
}

/**
 * Back-pressure manager for flow control
 */
class BackPressureManager {
    private queueDepth: number = 0;
    private maxQueueSize: number;
    private droppedMessages: number = 0;

    constructor(maxQueueSize: number = 1000) {
        this.maxQueueSize = maxQueueSize;
    }

    shouldThrottle(): boolean {
        return this.queueDepth > (this.maxQueueSize * 0.8);
    }

    addToQueue(): boolean {
        if (this.queueDepth >= this.maxQueueSize) {
            this.droppedMessages++;
            return false; // Queue full, drop message
        }
        this.queueDepth++;
        return true;
    }

    removeFromQueue(): void {
        this.queueDepth = Math.max(0, this.queueDepth - 1);
    }

    getMetrics(): { queueDepth: number; droppedMessages: number } {
        return {
            queueDepth: this.queueDepth,
            droppedMessages: this.droppedMessages
        };
    }

    reset(): void {
        this.queueDepth = 0;
        this.droppedMessages = 0;
    }
}

/**
 * Reliable log watcher with hybrid FileSystemWatcher + polling
 */
export class ReliableLogWatcher extends EventEmitter {
    private filePath: string;
    private fsWatcher: FSWatcher | null = null;
    private pollingTimer: NodeJS.Timeout | null = null;
    private lastFileSize: number = 0;
    private lastModified: Date = new Date(0);
    private lastPosition: number = 0;
    private isRunning: boolean = false;
    private securityManager: LogSecurityManager;
    private backPressureManager: BackPressureManager;
    private networkRecoveryManager: NetworkRecoveryManager;
    private config: LogStreamConfig;
    private debugMode: boolean;
    private streamId: string;

    constructor(filePath: string, config: LogStreamConfig, debugMode: boolean = false) {
        super();
        this.filePath = filePath;
        this.config = config;
        this.debugMode = debugMode;
        this.streamId = `stream_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
        this.securityManager = new LogSecurityManager();
        this.backPressureManager = new BackPressureManager(config.maxQueueDepth);

        // Initialize network recovery with production-ready config
        const recoveryConfig: RecoveryConfig = {
            maxRetries: 5,
            initialDelayMs: 1000,
            maxDelayMs: 30000,
            backoffMultiplier: 2,
            jitterEnabled: true
        };
        this.networkRecoveryManager = new NetworkRecoveryManager(recoveryConfig, debugMode);
    }

    async start(): Promise<void> {
        if (this.isRunning) return;

        this.isRunning = true;
        this.log(`🚀 Starting log watcher for: ${this.filePath}`);

        try {
            // Initialize file position
            await this.initializeFilePosition();

            // Initialize network recovery for this stream
            this.networkRecoveryManager.initializeStreamRecovery(this.streamId, this.lastPosition);

            // Start FileSystemWatcher (primary)
            this.startFileSystemWatcher();

            // Start polling backup (secondary)
            this.startPollingBackup();

            // Initial read in case file already has content
            await this.readChangesWithRecovery();

        } catch (error) {
            this.logError('Failed to start log watcher', error);
            throw error;
        }
    }

    stop(): void {
        if (!this.isRunning) return;

        this.isRunning = false;
        this.log(`🛑 Stopping log watcher for: ${this.filePath}`);

        // Clean up FileSystemWatcher
        if (this.fsWatcher) {
            this.fsWatcher.close();
            this.fsWatcher = null;
        }

        // Clean up polling timer
        if (this.pollingTimer) {
            clearInterval(this.pollingTimer);
            this.pollingTimer = null;
        }

        // Clean up network recovery
        this.networkRecoveryManager.cleanupStreamRecovery(this.streamId);

        // Reset back-pressure manager
        this.backPressureManager.reset();

        this.emit('stopped');
    }

    private async initializeFilePosition(): Promise<void> {
        try {
            const stats = await statAsync(this.filePath);
            this.lastFileSize = stats.size;
            this.lastModified = stats.mtime;
            
            if (!this.config.includeHistory) {
                // Start from end of file if not including history
                this.lastPosition = stats.size;
            }
        } catch (error) {
            // File doesn't exist yet, start from beginning
            this.lastFileSize = 0;
            this.lastPosition = 0;
            this.lastModified = new Date(0);
        }
    }

    private startFileSystemWatcher(): void {
        try {
            this.fsWatcher = watch(this.filePath, { persistent: true }, (eventType) => {
                if (eventType === 'change') {
                    // Debounce rapid changes
                    setTimeout(() => this.handleFileSystemEvent(), 100);
                }
            });

            this.fsWatcher.on('error', (error) => {
                this.logError('FileSystemWatcher error', error);
                // Rely on polling backup
            });

        } catch (error) {
            this.logError('Failed to start FileSystemWatcher, relying on polling', error);
        }
    }

    private startPollingBackup(): void {
        // Poll every 1 second as backup
        this.pollingTimer = setInterval(() => {
            this.handlePollingCheck();
        }, 1000);
    }

    private async handleFileSystemEvent(): Promise<void> {
        if (!this.isRunning) return;
        await this.readChanges();
    }

    private async handlePollingCheck(): Promise<void> {
        if (!this.isRunning) return;
        
        try {
            const stats = await statAsync(this.filePath);
            
            // Check if file has been modified since last check
            if (stats.mtime > this.lastModified || stats.size !== this.lastFileSize) {
                await this.readChanges();
            }
        } catch (error) {
            // File might not exist yet, ignore
        }
    }

    private async readChanges(): Promise<void> {
        try {
            const stats = await statAsync(this.filePath);
            
            // Handle file truncation
            if (stats.size < this.lastPosition) {
                this.lastPosition = 0;
            }

            // Read new content if file has grown
            if (stats.size > this.lastPosition) {
                const buffer = Buffer.alloc(stats.size - this.lastPosition);
                const fs = await import('fs');
                const fd = await fs.promises.open(this.filePath, 'r');
                
                try {
                    await fd.read(buffer, 0, buffer.length, this.lastPosition);
                    const newContent = buffer.toString('utf8');
                    
                    if (newContent.trim()) {
                        await this.processNewContent(newContent);
                    }
                    
                    this.lastPosition = stats.size;
                    this.lastFileSize = stats.size;
                    this.lastModified = stats.mtime;
                    
                } finally {
                    await fd.close();
                }
            }
            
        } catch (error) {
            // Handle file locking gracefully
            if ((error as any).code === 'EBUSY' || (error as any).code === 'ENOENT') {
                // File is locked or doesn't exist, try again later
                return;
            }
            this.logError('Error reading log file changes', error);
        }
    }

    private async processNewContent(content: string): Promise<void> {
        const lines = content.split(/\r?\n/).filter(line => line.trim());
        
        for (const line of lines) {
            // Check back-pressure
            if (this.backPressureManager.shouldThrottle()) {
                this.log('⚠️ Back-pressure detected, throttling log stream');
                break;
            }

            // Add to queue
            if (!this.backPressureManager.addToQueue()) {
                this.log('❌ Queue full, dropping log message');
                continue;
            }

            // Process log entry
            const logEntry = this.parseLogEntry(line);
            
            // Apply security filtering
            if (this.config.enablePiiRedaction) {
                logEntry.message = this.securityManager.redactSensitiveData(logEntry.message);
            }

            // Apply level filtering
            if (this.shouldIncludeEntry(logEntry)) {
                this.emit('logEntry', logEntry);
            }

            // Remove from queue after processing
            this.backPressureManager.removeFromQueue();
        }
    }

    private parseLogEntry(line: string): LogEntry {
        // Basic log parsing - can be enhanced for specific formats
        const level = this.determineLogLevel(line);
        const source = this.determineLogSource();
        
        return {
            timestamp: new Date(),
            level,
            source,
            message: line,
            metadata: {
                filePath: this.filePath,
                queueDepth: this.backPressureManager.getMetrics().queueDepth
            }
        };
    }

    private determineLogLevel(line: string): LogEntry['level'] {
        const lowerLine = line.toLowerCase();
        if (lowerLine.includes('error') || lowerLine.includes('exception')) return 'error';
        if (lowerLine.includes('warning') || lowerLine.includes('warn')) return 'warning';
        if (lowerLine.includes('success') || lowerLine.includes('complete')) return 'success';
        return 'info';
    }

    private determineLogSource(): LogEntry['source'] {
        if (this.filePath.includes('Tycoon_')) return 'tycoon';
        if (this.filePath.includes('ScriptOutput_')) return 'scripts';
        if (this.filePath.includes('journal')) return 'revit_journal';
        return 'tycoon';
    }

    private shouldIncludeEntry(entry: LogEntry): boolean {
        if (this.config.filterLevel === 'all') return true;
        return entry.level === this.config.filterLevel;
    }

    getMetrics(): StreamMetrics {
        const backPressureMetrics = this.backPressureManager.getMetrics();
        
        return {
            heapUsage: process.memoryUsage().heapUsed / 1024 / 1024, // MB
            queueDepth: backPressureMetrics.queueDepth,
            droppedMessages: backPressureMetrics.droppedMessages,
            averageLatency: 0, // TODO: Implement latency tracking
            connectionUptime: this.isRunning ? Date.now() : 0,
            errorRate: 0 // TODO: Implement error rate tracking
        };
    }

    private log(message: string): void {
        if (this.debugMode) {
            console.log(chalk.blue(`[LogStreamer] ${message}`));
        }
    }

    private logError(message: string, error: any): void {
        console.error(chalk.red(`[LogStreamer] ${message}:`), error);
    }

    /**
     * Enhanced read changes with network recovery support
     */
    private async readChangesWithRecovery(): Promise<void> {
        try {
            const result = await this.networkRecoveryManager.recoverStream(
                this.streamId,
                this.filePath,
                async (offset: number) => {
                    const stats = await statAsync(this.filePath);

                    if (stats.size <= offset) {
                        return { content: '', newOffset: offset };
                    }

                    const buffer = Buffer.alloc(stats.size - offset);
                    const fs = await import('fs');
                    const fd = await fs.promises.open(this.filePath, 'r');

                    try {
                        await fd.read(buffer, 0, buffer.length, offset);
                        const content = buffer.toString('utf8');
                        return { content, newOffset: stats.size };
                    } finally {
                        await fd.close();
                    }
                }
            );

            if (result.content.trim()) {
                await this.processNewContent(result.content);
            }

            this.lastPosition = result.newOffset;

        } catch (error) {
            this.logError('Enhanced read with recovery failed', error);
            // Fall back to regular read method
            await this.readChanges();
        }
    }

    /**
     * Get enhanced metrics including recovery information
     */
    getEnhancedMetrics(): StreamMetrics & { recoveryMetrics: any } {
        const baseMetrics = this.getMetrics();
        const recoveryMetrics = this.networkRecoveryManager.getRecoveryMetrics(this.streamId);

        return {
            ...baseMetrics,
            recoveryMetrics
        };
    }
}
