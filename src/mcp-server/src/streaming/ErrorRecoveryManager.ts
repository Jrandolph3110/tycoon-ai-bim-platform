/**
 * ErrorRecoveryManager - Comprehensive error recovery with exponential backoff
 * 
 * Implements ChatGPT's recommendations for production resilience:
 * - Exponential backoff for file access errors
 * - Network resilience with connection recovery
 * - Automatic stream restart for failed connections
 * - lastByteOffset token recovery to prevent duplicates
 */

import { EventEmitter } from 'events';
import chalk from 'chalk';

export interface RecoveryConfig {
    maxRetries: number;
    initialDelayMs: number;
    maxDelayMs: number;
    backoffMultiplier: number;
    jitterEnabled: boolean;
}

export interface RecoveryAttempt {
    attemptNumber: number;
    timestamp: Date;
    error: Error;
    delayMs: number;
    success: boolean;
}

export interface StreamRecoveryState {
    streamId: string;
    lastByteOffset: number;
    lastSuccessfulRead: Date;
    failureCount: number;
    recoveryAttempts: RecoveryAttempt[];
    isRecovering: boolean;
}

/**
 * Exponential backoff calculator with jitter
 */
class BackoffCalculator {
    private config: RecoveryConfig;

    constructor(config: RecoveryConfig) {
        this.config = config;
    }

    calculateDelay(attemptNumber: number): number {
        const exponentialDelay = this.config.initialDelayMs * 
            Math.pow(this.config.backoffMultiplier, attemptNumber - 1);
        
        const cappedDelay = Math.min(exponentialDelay, this.config.maxDelayMs);
        
        if (this.config.jitterEnabled) {
            // Add ±25% jitter to prevent thundering herd
            const jitter = cappedDelay * 0.25 * (Math.random() - 0.5);
            return Math.max(0, cappedDelay + jitter);
        }
        
        return cappedDelay;
    }

    shouldRetry(attemptNumber: number): boolean {
        return attemptNumber <= this.config.maxRetries;
    }
}

/**
 * File access recovery with ERROR_SHARING_VIOLATION handling
 */
export class FileAccessRecovery {
    private backoffCalculator: BackoffCalculator;
    private debugMode: boolean;

    constructor(config: RecoveryConfig, debugMode: boolean = false) {
        this.backoffCalculator = new BackoffCalculator(config);
        this.debugMode = debugMode;
    }

    async readFileWithRecovery(
        filePath: string, 
        readFunction: () => Promise<string>,
        streamState?: StreamRecoveryState
    ): Promise<string> {
        let attemptNumber = 1;
        
        while (this.backoffCalculator.shouldRetry(attemptNumber)) {
            try {
                const result = await readFunction();
                
                // Update recovery state on success
                if (streamState) {
                    streamState.isRecovering = false;
                    streamState.lastSuccessfulRead = new Date();
                    streamState.failureCount = 0;
                }
                
                this.log(`✅ File read successful on attempt ${attemptNumber}: ${filePath}`);
                return result;
                
            } catch (error: any) {
                const isFileAccessError = this.isRecoverableFileError(error);
                
                if (!isFileAccessError || !this.backoffCalculator.shouldRetry(attemptNumber + 1)) {
                    // Non-recoverable error or max retries exceeded
                    this.logError(`❌ File read failed permanently after ${attemptNumber} attempts: ${filePath}`, error);
                    throw error;
                }
                
                // Calculate delay and record attempt
                const delayMs = this.backoffCalculator.calculateDelay(attemptNumber);
                const attempt: RecoveryAttempt = {
                    attemptNumber,
                    timestamp: new Date(),
                    error,
                    delayMs,
                    success: false
                };
                
                if (streamState) {
                    streamState.isRecovering = true;
                    streamState.failureCount++;
                    streamState.recoveryAttempts.push(attempt);
                }
                
                this.log(`⚠️ File read failed (attempt ${attemptNumber}), retrying in ${delayMs}ms: ${error.message}`);
                
                // Wait before retry
                await this.delay(delayMs);
                attemptNumber++;
            }
        }
        
        throw new Error(`File read failed after ${attemptNumber - 1} attempts: ${filePath}`);
    }

    private isRecoverableFileError(error: any): boolean {
        const recoverableErrors = [
            'EBUSY',           // File is busy
            'ENOENT',          // File not found (might be created soon)
            'EACCES',          // Access denied (temporary)
            'EMFILE',          // Too many open files
            'ENFILE',          // File table overflow
            'EAGAIN',          // Resource temporarily unavailable
            'ERROR_SHARING_VIOLATION'  // Windows file sharing violation
        ];
        
        return recoverableErrors.some(code => 
            error.code === code || 
            error.message?.includes(code) ||
            error.errno === code
        );
    }

    private delay(ms: number): Promise<void> {
        return new Promise(resolve => setTimeout(resolve, ms));
    }

    private log(message: string): void {
        if (this.debugMode) {
            console.log(chalk.yellow(`[FileAccessRecovery] ${message}`));
        }
    }

    private logError(message: string, error: any): void {
        console.error(chalk.red(`[FileAccessRecovery] ${message}:`), error);
    }
}

/**
 * Network connection recovery manager
 */
export class NetworkRecoveryManager extends EventEmitter {
    private recoveryStates: Map<string, StreamRecoveryState> = new Map();
    private fileAccessRecovery: FileAccessRecovery;
    private config: RecoveryConfig;
    private debugMode: boolean;

    constructor(config: RecoveryConfig, debugMode: boolean = false) {
        super();
        this.config = config;
        this.debugMode = debugMode;
        this.fileAccessRecovery = new FileAccessRecovery(config, debugMode);
    }

    /**
     * Initialize recovery state for a stream
     */
    initializeStreamRecovery(streamId: string, initialOffset: number = 0): void {
        const recoveryState: StreamRecoveryState = {
            streamId,
            lastByteOffset: initialOffset,
            lastSuccessfulRead: new Date(),
            failureCount: 0,
            recoveryAttempts: [],
            isRecovering: false
        };
        
        this.recoveryStates.set(streamId, recoveryState);
        this.log(`🔧 Initialized recovery state for stream: ${streamId}`);
    }

    /**
     * Recover stream with lastByteOffset token to prevent duplicates
     */
    async recoverStream(
        streamId: string,
        filePath: string,
        readFunction: (offset: number) => Promise<{ content: string; newOffset: number }>
    ): Promise<{ content: string; newOffset: number }> {
        const recoveryState = this.recoveryStates.get(streamId);
        if (!recoveryState) {
            throw new Error(`No recovery state found for stream: ${streamId}`);
        }

        this.log(`🔄 Starting stream recovery for: ${streamId} from offset ${recoveryState.lastByteOffset}`);

        try {
            const result = await this.fileAccessRecovery.readFileWithRecovery(
                filePath,
                async () => {
                    const readResult = await readFunction(recoveryState.lastByteOffset);
                    return JSON.stringify(readResult); // Serialize for recovery function
                },
                recoveryState
            );

            const parsedResult = JSON.parse(result);
            
            // Update offset on successful read
            recoveryState.lastByteOffset = parsedResult.newOffset;
            recoveryState.lastSuccessfulRead = new Date();
            
            this.log(`✅ Stream recovery successful for: ${streamId}, new offset: ${parsedResult.newOffset}`);
            this.emit('streamRecovered', { streamId, newOffset: parsedResult.newOffset });
            
            return parsedResult;
            
        } catch (error) {
            this.logError(`❌ Stream recovery failed for: ${streamId}`, error);
            this.emit('streamRecoveryFailed', { streamId, error });
            throw error;
        }
    }

    /**
     * Handle connection drop with automatic recovery
     */
    async handleConnectionDrop(
        streamId: string,
        reconnectFunction: () => Promise<void>
    ): Promise<void> {
        const recoveryState = this.recoveryStates.get(streamId);
        if (!recoveryState) {
            throw new Error(`No recovery state found for stream: ${streamId}`);
        }

        this.log(`🔌 Handling connection drop for stream: ${streamId}`);
        recoveryState.isRecovering = true;

        let attemptNumber = 1;
        const backoffCalculator = new BackoffCalculator(this.config);

        while (backoffCalculator.shouldRetry(attemptNumber)) {
            try {
                await reconnectFunction();
                
                recoveryState.isRecovering = false;
                recoveryState.failureCount = 0;
                
                this.log(`✅ Connection recovered for stream: ${streamId} on attempt ${attemptNumber}`);
                this.emit('connectionRecovered', { streamId, attemptNumber });
                return;
                
            } catch (error: any) {
                const delayMs = backoffCalculator.calculateDelay(attemptNumber);
                
                const attempt: RecoveryAttempt = {
                    attemptNumber,
                    timestamp: new Date(),
                    error,
                    delayMs,
                    success: false
                };
                
                recoveryState.failureCount++;
                recoveryState.recoveryAttempts.push(attempt);
                
                this.log(`⚠️ Connection recovery failed (attempt ${attemptNumber}), retrying in ${delayMs}ms: ${error.message}`);
                
                if (backoffCalculator.shouldRetry(attemptNumber + 1)) {
                    await this.delay(delayMs);
                    attemptNumber++;
                } else {
                    this.logError(`❌ Connection recovery failed permanently after ${attemptNumber} attempts for stream: ${streamId}`, error);
                    this.emit('connectionRecoveryFailed', { streamId, error, attempts: attemptNumber });
                    throw error;
                }
            }
        }
    }

    /**
     * Get recovery metrics for monitoring
     */
    getRecoveryMetrics(streamId?: string): any {
        if (streamId) {
            const state = this.recoveryStates.get(streamId);
            return state ? {
                streamId: state.streamId,
                lastByteOffset: state.lastByteOffset,
                lastSuccessfulRead: state.lastSuccessfulRead,
                failureCount: state.failureCount,
                isRecovering: state.isRecovering,
                totalRecoveryAttempts: state.recoveryAttempts.length,
                successRate: this.calculateSuccessRate(state.recoveryAttempts)
            } : null;
        }

        // Return metrics for all streams
        const allMetrics: any = {};
        for (const [id, state] of this.recoveryStates) {
            allMetrics[id] = {
                lastByteOffset: state.lastByteOffset,
                failureCount: state.failureCount,
                isRecovering: state.isRecovering,
                totalRecoveryAttempts: state.recoveryAttempts.length,
                successRate: this.calculateSuccessRate(state.recoveryAttempts)
            };
        }

        return {
            totalStreams: this.recoveryStates.size,
            recoveringStreams: Array.from(this.recoveryStates.values()).filter(s => s.isRecovering).length,
            streams: allMetrics
        };
    }

    /**
     * Cleanup recovery state for a stream
     */
    cleanupStreamRecovery(streamId: string): void {
        if (this.recoveryStates.delete(streamId)) {
            this.log(`🧹 Cleaned up recovery state for stream: ${streamId}`);
        }
    }

    private calculateSuccessRate(attempts: RecoveryAttempt[]): number {
        if (attempts.length === 0) return 1.0;
        const successful = attempts.filter(a => a.success).length;
        return successful / attempts.length;
    }

    private delay(ms: number): Promise<void> {
        return new Promise(resolve => setTimeout(resolve, ms));
    }

    private log(message: string): void {
        if (this.debugMode) {
            console.log(chalk.blue(`[NetworkRecoveryManager] ${message}`));
        }
    }

    private logError(message: string, error: any): void {
        console.error(chalk.red(`[NetworkRecoveryManager] ${message}:`), error);
    }
}
