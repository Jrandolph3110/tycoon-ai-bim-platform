import * as fs from 'fs';
import * as path from 'path';
import { EventEmitter } from 'events';
import * as chokidar from 'chokidar';

/**
 * 🚀 TYCOON LIVE STREAM MONITOR - Real-time file streaming monitor
 * 
 * Features:
 * - Real-time file watching
 * - Live progress tracking
 * - Streaming data parsing
 * - Session management
 * - Historical data access
 */
export class LiveStreamMonitor extends EventEmitter {
    private vaultPath: string;
    private watcher: chokidar.FSWatcher | null = null;
    private activeSessions: Map<string, SessionInfo> = new Map();
    private isMonitoring: boolean = false;

    constructor(vaultPath?: string) {
        super();
        this.vaultPath = vaultPath || path.join(
            process.env.LOCALAPPDATA || process.env.HOME || '.',
            'Tycoon',
            'DataVault'
        );
        
        // Ensure vault directory exists
        this.ensureVaultStructure();
        
        console.log(`🏗️ Live Stream Monitor initialized: ${this.vaultPath}`);
    }

    /**
     * Start monitoring for streaming sessions
     */
    public startMonitoring(): void {
        if (this.isMonitoring) {
            console.log('⚠️ Monitor already running');
            return;
        }

        const sessionsPath = path.join(this.vaultPath, 'Sessions');
        
        this.watcher = chokidar.watch(sessionsPath, {
            ignored: /^\./, // ignore dotfiles
            persistent: true,
            ignoreInitial: false,
            depth: 2
        });

        this.watcher
            .on('add', (filePath: string) => this.handleFileAdded(filePath))
            .on('change', (filePath: string) => this.handleFileChanged(filePath))
            .on('unlink', (filePath: string) => this.handleFileRemoved(filePath))
            .on('error', (error: Error) => console.error('🚨 File watcher error:', error));

        this.isMonitoring = true;
        console.log('👁️ Live Stream Monitor started');
        
        // Scan for existing sessions
        this.scanExistingSessions();
    }

    /**
     * Stop monitoring
     */
    public stopMonitoring(): void {
        if (this.watcher) {
            this.watcher.close();
            this.watcher = null;
        }
        this.isMonitoring = false;
        console.log('🛑 Live Stream Monitor stopped');
    }

    /**
     * Get current streaming progress for a session
     */
    public async getStreamingProgress(sessionId: string): Promise<StreamingProgress | null> {
        const session = this.activeSessions.get(sessionId);
        if (!session) return null;

        try {
            const progressPath = path.join(session.sessionPath, 'progress.json');
            if (!fs.existsSync(progressPath)) return null;

            const progressData = JSON.parse(fs.readFileSync(progressPath, 'utf8'));
            return progressData as StreamingProgress;
        } catch (error) {
            console.error(`Failed to read progress for session ${sessionId}:`, error);
            return null;
        }
    }

    /**
     * Get streaming data chunks as they arrive
     */
    public async getStreamingChunks(sessionId: string, fromChunk: number = 0): Promise<StreamingChunk[]> {
        const session = this.activeSessions.get(sessionId);
        if (!session) return [];

        try {
            const streamPath = path.join(session.sessionPath, 'stream.jsonl');
            if (!fs.existsSync(streamPath)) return [];

            const content = fs.readFileSync(streamPath, 'utf8');
            const lines = content.trim().split('\n').filter(line => line.trim());
            
            const chunks: StreamingChunk[] = [];
            for (let i = fromChunk; i < lines.length; i++) {
                try {
                    const chunk = JSON.parse(lines[i]);
                    chunks.push(chunk);
                } catch (parseError) {
                    console.warn(`Failed to parse chunk ${i}:`, parseError);
                }
            }

            return chunks;
        } catch (error) {
            console.error(`Failed to read streaming chunks for session ${sessionId}:`, error);
            return [];
        }
    }

    /**
     * Get all active sessions
     */
    public getActiveSessions(): SessionInfo[] {
        return Array.from(this.activeSessions.values());
    }

    /**
     * Get session metadata
     */
    public async getSessionMetadata(sessionId: string): Promise<SessionMetadata | null> {
        const session = this.activeSessions.get(sessionId);
        if (!session) return null;

        try {
            const metadataPath = path.join(session.sessionPath, 'metadata.json');
            if (!fs.existsSync(metadataPath)) return null;

            const metadata = JSON.parse(fs.readFileSync(metadataPath, 'utf8'));
            return metadata as SessionMetadata;
        } catch (error) {
            console.error(`Failed to read metadata for session ${sessionId}:`, error);
            return null;
        }
    }

    /**
     * Handle file added
     */
    private handleFileAdded(filePath: string): void {
        const fileName = path.basename(filePath);
        const sessionId = this.extractSessionId(filePath);
        
        if (!sessionId) return;

        if (fileName === 'metadata.json') {
            this.registerSession(sessionId, filePath);
        }
    }

    /**
     * Handle file changed
     */
    private handleFileChanged(filePath: string): void {
        const fileName = path.basename(filePath);
        const sessionId = this.extractSessionId(filePath);
        
        if (!sessionId) return;

        if (fileName === 'progress.json') {
            this.handleProgressUpdate(sessionId, filePath);
        } else if (fileName === 'stream.jsonl') {
            this.handleStreamUpdate(sessionId, filePath);
        }
    }

    /**
     * Handle file removed
     */
    private handleFileRemoved(filePath: string): void {
        const sessionId = this.extractSessionId(filePath);
        if (sessionId && this.activeSessions.has(sessionId)) {
            console.log(`🗑️ Session removed: ${sessionId}`);
            this.activeSessions.delete(sessionId);
            this.emit('sessionRemoved', sessionId);
        }
    }

    /**
     * Register new streaming session
     */
    private async registerSession(sessionId: string, metadataPath: string): Promise<void> {
        try {
            const sessionPath = path.dirname(metadataPath);
            const metadata = JSON.parse(fs.readFileSync(metadataPath, 'utf8'));
            
            const sessionInfo: SessionInfo = {
                sessionId,
                sessionPath,
                startTime: new Date(metadata.startTime),
                documentTitle: metadata.documentTitle,
                totalElements: metadata.totalElements,
                processingTier: metadata.processingTier,
                isActive: true
            };

            this.activeSessions.set(sessionId, sessionInfo);
            console.log(`📡 New streaming session: ${sessionId} (${metadata.totalElements} elements)`);
            
            this.emit('sessionStarted', sessionInfo);
        } catch (error) {
            console.error(`Failed to register session ${sessionId}:`, error);
        }
    }

    /**
     * Handle progress update
     */
    private async handleProgressUpdate(sessionId: string, progressPath: string): Promise<void> {
        try {
            const progress = JSON.parse(fs.readFileSync(progressPath, 'utf8'));
            console.log(`📊 Progress update: ${sessionId} - ${progress.status} (${progress.progressPercent?.toFixed(1)}%)`);
            
            this.emit('progressUpdate', sessionId, progress);
            
            if (progress.status === 'streaming_complete') {
                const session = this.activeSessions.get(sessionId);
                if (session) {
                    session.isActive = false;
                    this.emit('sessionCompleted', sessionId);
                }
            }
        } catch (error) {
            console.error(`Failed to handle progress update for ${sessionId}:`, error);
        }
    }

    /**
     * Handle stream data update
     */
    private async handleStreamUpdate(sessionId: string, streamPath: string): Promise<void> {
        try {
            const stats = fs.statSync(streamPath);
            console.log(`📤 Stream update: ${sessionId} - ${(stats.size / 1024 / 1024).toFixed(2)} MB`);
            
            this.emit('streamUpdate', sessionId, stats.size);
        } catch (error) {
            console.error(`Failed to handle stream update for ${sessionId}:`, error);
        }
    }

    /**
     * Extract session ID from file path
     */
    private extractSessionId(filePath: string): string | null {
        const parts = filePath.split(path.sep);
        const sessionsIndex = parts.findIndex(part => part === 'Sessions');
        
        if (sessionsIndex >= 0 && sessionsIndex < parts.length - 1) {
            return parts[sessionsIndex + 1];
        }
        
        return null;
    }

    /**
     * Scan for existing sessions
     */
    private scanExistingSessions(): void {
        const sessionsPath = path.join(this.vaultPath, 'Sessions');
        
        if (!fs.existsSync(sessionsPath)) return;

        const sessionDirs = fs.readdirSync(sessionsPath, { withFileTypes: true })
            .filter(dirent => dirent.isDirectory())
            .map(dirent => dirent.name);

        for (const sessionId of sessionDirs) {
            const metadataPath = path.join(sessionsPath, sessionId, 'metadata.json');
            if (fs.existsSync(metadataPath)) {
                this.registerSession(sessionId, metadataPath);
            }
        }

        console.log(`🔍 Found ${sessionDirs.length} existing sessions`);
    }

    /**
     * Ensure vault directory structure exists
     */
    private ensureVaultStructure(): void {
        const dirs = [
            this.vaultPath,
            path.join(this.vaultPath, 'Sessions'),
            path.join(this.vaultPath, 'Archive'),
            path.join(this.vaultPath, 'Database')
        ];

        for (const dir of dirs) {
            if (!fs.existsSync(dir)) {
                fs.mkdirSync(dir, { recursive: true });
            }
        }
    }
}

// Type definitions
export interface SessionInfo {
    sessionId: string;
    sessionPath: string;
    startTime: Date;
    documentTitle: string;
    totalElements: number;
    processingTier: string;
    isActive: boolean;
}

export interface StreamingProgress {
    sessionId: string;
    timestamp: string;
    status: string;
    message: string;
    totalElements: number;
    processedElements: number;
    chunkCount: number;
    progressPercent: number;
    isStreaming: boolean;
}

export interface StreamingChunk {
    chunkId: number;
    timestamp: string;
    type: string;
    elementCount: number;
    elements: any[];
}

export interface SessionMetadata {
    sessionId: string;
    startTime: string;
    documentTitle: string;
    viewName?: string;
    totalElements: number;
    revitVersion: string;
    tycoonVersion: string;
    processingTier: string;
}
