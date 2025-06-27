import { LiveStreamMonitor, SessionInfo, StreamingProgress } from '../storage/LiveStreamMonitor';
import { BimDatabase } from '../storage/BimDatabase';
import * as path from 'path';

/**
 * 🚀 TYCOON STREAMING TOOLS - MCP tools for accessing streaming data
 */
export class StreamingTools {
    private monitor: LiveStreamMonitor;
    private database: BimDatabase;
    private vaultPath: string;

    constructor(vaultPath?: string) {
        this.vaultPath = vaultPath || path.join(
            process.env.LOCALAPPDATA || process.env.HOME || '.',
            'Tycoon',
            'DataVault'
        );
        
        this.monitor = new LiveStreamMonitor(this.vaultPath);
        this.database = new BimDatabase(this.vaultPath);
    }

    /**
     * Initialize streaming tools
     */
    public async initialize(): Promise<void> {
        await this.database.initialize();
        this.monitor.startMonitoring();
        
        console.log('🚀 Streaming Tools initialized');
    }

    /**
     * Get live streaming progress
     */
    public async getLiveStreamingProgress(): Promise<any> {
        const activeSessions = this.monitor.getActiveSessions();
        const progressData = [];

        for (const session of activeSessions) {
            const progress = await this.monitor.getStreamingProgress(session.sessionId);
            if (progress) {
                progressData.push({
                    sessionId: session.sessionId,
                    documentTitle: session.documentTitle,
                    totalElements: session.totalElements,
                    processingTier: session.processingTier,
                    isActive: session.isActive,
                    progress: progress
                });
            }
        }

        return {
            status: 'success',
            activeSessions: activeSessions.length,
            streamingData: progressData,
            vaultPath: this.vaultPath,
            timestamp: new Date().toISOString()
        };
    }

    /**
     * Get streaming session data
     */
    public async getStreamingSession(sessionId: string): Promise<any> {
        const metadata = await this.monitor.getSessionMetadata(sessionId);
        const progress = await this.monitor.getStreamingProgress(sessionId);
        const chunks = await this.monitor.getStreamingChunks(sessionId);

        if (!metadata) {
            return {
                status: 'error',
                error: `Session ${sessionId} not found`
            };
        }

        return {
            status: 'success',
            session: {
                metadata,
                progress,
                chunkCount: chunks.length,
                totalElements: metadata.totalElements,
                isComplete: progress?.status === 'streaming_complete'
            }
        };
    }

    /**
     * Get streaming chunks for a session
     */
    public async getStreamingChunks(sessionId: string, fromChunk: number = 0, maxChunks: number = 10): Promise<any> {
        const chunks = await this.monitor.getStreamingChunks(sessionId, fromChunk);
        
        if (chunks.length === 0) {
            return {
                status: 'error',
                error: `No chunks found for session ${sessionId} from chunk ${fromChunk}`
            };
        }

        // Limit chunks to prevent huge responses
        const limitedChunks = chunks.slice(0, maxChunks);
        
        return {
            status: 'success',
            sessionId,
            fromChunk,
            returnedChunks: limitedChunks.length,
            totalAvailableChunks: chunks.length,
            chunks: limitedChunks.map(chunk => ({
                chunkId: chunk.chunkId,
                timestamp: chunk.timestamp,
                elementCount: chunk.elementCount,
                type: chunk.type,
                // Include first few elements as preview
                elementPreview: chunk.elements.slice(0, 3)
            }))
        };
    }

    /**
     * Get all sessions (active and historical)
     */
    public async getAllSessions(): Promise<any> {
        const activeSessions = this.monitor.getActiveSessions();
        
        return {
            status: 'success',
            totalSessions: activeSessions.length,
            sessions: activeSessions.map(session => ({
                sessionId: session.sessionId,
                documentTitle: session.documentTitle,
                startTime: session.startTime,
                totalElements: session.totalElements,
                processingTier: session.processingTier,
                isActive: session.isActive
            })),
            vaultPath: this.vaultPath
        };
    }

    /**
     * Get session statistics
     */
    public async getSessionStats(sessionId: string): Promise<any> {
        try {
            const stats = await this.database.getSessionStats(sessionId);
            const metadata = await this.monitor.getSessionMetadata(sessionId);
            
            return {
                status: 'success',
                sessionId,
                statistics: stats,
                metadata: metadata
            };
        } catch (error) {
            return {
                status: 'error',
                error: `Failed to get stats for session ${sessionId}: ${error instanceof Error ? error.message : String(error)}`
            };
        }
    }

    /**
     * Search elements across sessions
     */
    public async searchElements(criteria: any): Promise<any> {
        try {
            const elements = await this.database.queryElements(criteria);
            
            return {
                status: 'success',
                criteria,
                resultCount: elements.length,
                elements: elements.slice(0, 100) // Limit results
            };
        } catch (error) {
            return {
                status: 'error',
                error: `Search failed: ${error instanceof Error ? error.message : String(error)}`
            };
        }
    }

    /**
     * Get vault status and health
     */
    public async getVaultStatus(): Promise<any> {
        const activeSessions = this.monitor.getActiveSessions();
        const activeCount = activeSessions.filter(s => s.isActive).length;
        const completedCount = activeSessions.filter(s => !s.isActive).length;
        
        return {
            status: 'success',
            vault: {
                path: this.vaultPath,
                activeSessions: activeCount,
                completedSessions: completedCount,
                totalSessions: activeSessions.length,
                isMonitoring: true
            },
            database: {
                connected: true,
                path: path.join(this.vaultPath, 'Database', 'tycoon_bim.db')
            },
            timestamp: new Date().toISOString()
        };
    }

    /**
     * Stop monitoring and cleanup
     */
    public async shutdown(): Promise<void> {
        this.monitor.stopMonitoring();
        await this.database.close();
        console.log('🛑 Streaming Tools shutdown complete');
    }
}

/**
 * MCP Tool definitions for streaming functionality
 */
export const streamingToolDefinitions = [
    {
        name: 'get_live_streaming_progress',
        description: '📊 Get real-time streaming progress for all active sessions',
        inputSchema: {
            type: 'object',
            properties: {},
            required: []
        }
    },
    {
        name: 'get_streaming_session',
        description: '📡 Get detailed information about a specific streaming session',
        inputSchema: {
            type: 'object',
            properties: {
                sessionId: {
                    type: 'string',
                    description: 'Session ID to retrieve'
                }
            },
            required: ['sessionId']
        }
    },
    {
        name: 'get_streaming_chunks',
        description: '📦 Get streaming data chunks for a session',
        inputSchema: {
            type: 'object',
            properties: {
                sessionId: {
                    type: 'string',
                    description: 'Session ID'
                },
                fromChunk: {
                    type: 'number',
                    description: 'Starting chunk number (default: 0)'
                },
                maxChunks: {
                    type: 'number',
                    description: 'Maximum chunks to return (default: 10)'
                }
            },
            required: ['sessionId']
        }
    },
    {
        name: 'get_all_sessions',
        description: '📋 Get list of all streaming sessions (active and historical)',
        inputSchema: {
            type: 'object',
            properties: {},
            required: []
        }
    },
    {
        name: 'get_session_stats',
        description: '📈 Get statistics for a streaming session',
        inputSchema: {
            type: 'object',
            properties: {
                sessionId: {
                    type: 'string',
                    description: 'Session ID for statistics'
                }
            },
            required: ['sessionId']
        }
    },
    {
        name: 'search_elements',
        description: '🔍 Search elements across all sessions',
        inputSchema: {
            type: 'object',
            properties: {
                sessionId: {
                    type: 'string',
                    description: 'Specific session to search (optional)'
                },
                category: {
                    type: 'string',
                    description: 'Element category to filter by'
                },
                familyName: {
                    type: 'string',
                    description: 'Family name to filter by'
                },
                limit: {
                    type: 'number',
                    description: 'Maximum results to return'
                }
            },
            required: []
        }
    },
    {
        name: 'get_vault_status',
        description: '🏗️ Get overall vault status and health information',
        inputSchema: {
            type: 'object',
            properties: {},
            required: []
        }
    }
];
