/**
 * 🚀 FAFB Binary Streaming Handler - Optimized Transport Layer
 * 
 * Features:
 * - Binary deserialization with MessagePack (5-10x faster parsing)
 * - Chunked streaming reception (timeout-resistant)
 * - Decompression support (handles compressed payloads)
 * - Progress tracking (real-time feedback)
 * - Memory-efficient processing (streaming vs loading all)
 */

import { EventEmitter } from 'events';
import * as msgpack from '@msgpack/msgpack';
import * as zlib from 'zlib';
import { promisify } from 'util';

const gunzip = promisify(zlib.gunzip);

export interface StreamingMetadata {
    totalElements: number;
    processingTier: string;
    chunkSize: number;
    compressionEnabled: boolean;
    binaryMode: boolean;
    viewName?: string;
    documentTitle?: string;
}

export interface ElementChunk {
    chunkId: number;
    totalChunks: number;
    elements: any[];
    progress: number;
    memoryUsage: number;
    timestamp: string;
    isComplete: boolean;
}

export interface StreamingMessage {
    type: string;
    commandId: string;
    chunk?: ElementChunk;
    metadata?: StreamingMetadata;
}

export interface StreamingSession {
    commandId: string;
    metadata: StreamingMetadata;
    chunks: ElementChunk[];
    receivedChunks: number;
    totalChunks: number;
    startTime: Date;
    lastChunkTime: Date;
    isComplete: boolean;
    elements: any[];
}

export class BinaryStreamingHandler extends EventEmitter {
    private activeSessions = new Map<string, StreamingSession>();
    private debugMode: boolean;

    constructor(debugMode = true) {
        super();
        this.debugMode = debugMode;
    }

    /**
     * Handle incoming WebSocket message (binary or JSON)
     */
    async handleMessage(data: Buffer | string): Promise<void> {
        try {
            let message: StreamingMessage;

            if (Buffer.isBuffer(data)) {
                // Handle binary message
                message = await this.parseBinaryMessage(data);
            } else {
                // Handle JSON message
                message = JSON.parse(data);
            }

            await this.processStreamingMessage(message);
        } catch (error) {
            this.log(`❌ STREAMING: Failed to handle message: ${error instanceof Error ? error.message : String(error)}`);
            this.emit('error', error);
        }
    }

    /**
     * Parse binary message (MessagePack with optional compression)
     */
    private async parseBinaryMessage(data: Buffer): Promise<StreamingMessage> {
        try {
            // Check for MessagePack header
            const headerText = data.slice(0, 8).toString('utf8');
            
            if (headerText.startsWith('MSGPACK:')) {
                // Extract payload after header
                const payload = data.slice(8);
                
                // Check if compressed (simple heuristic - could be improved)
                let decodedData = payload;
                
                try {
                    // Try to decompress first
                    decodedData = await gunzip(payload);
                    this.log(`🗜️ DECOMPRESSION: Decompressed ${payload.length} → ${decodedData.length} bytes`);
                } catch {
                    // Not compressed, use original payload
                    decodedData = payload;
                }
                
                // Deserialize with MessagePack
                const message = msgpack.decode(decodedData) as StreamingMessage;
                this.log(`📦 BINARY: Parsed MessagePack message (${decodedData.length} bytes)`);
                
                return message;
            } else {
                throw new Error('Unknown binary message format');
            }
        } catch (error) {
            this.log(`❌ BINARY: Failed to parse binary message: ${error instanceof Error ? error.message : String(error)}`);
            throw error;
        }
    }

    /**
     * Process streaming message based on type
     */
    private async processStreamingMessage(message: StreamingMessage): Promise<void> {
        switch (message.type) {
            case 'streaming_metadata':
                await this.handleMetadata(message);
                break;
                
            case 'streaming_chunk':
                await this.handleChunk(message);
                break;
                
            case 'streaming_complete':
                await this.handleCompletion(message);
                break;
                
            default:
                this.log(`⚠️ STREAMING: Unknown message type: ${message.type}`);
        }
    }

    /**
     * Handle streaming metadata
     */
    private async handleMetadata(message: StreamingMessage): Promise<void> {
        if (!message.metadata) {
            throw new Error('Metadata message missing metadata');
        }

        const session: StreamingSession = {
            commandId: message.commandId,
            metadata: message.metadata,
            chunks: [],
            receivedChunks: 0,
            totalChunks: 0,
            startTime: new Date(),
            lastChunkTime: new Date(),
            isComplete: false,
            elements: []
        };

        this.activeSessions.set(message.commandId, session);

        this.log(`📊 METADATA: Starting stream for ${message.metadata.totalElements} elements ` +
                 `(${message.metadata.processingTier} tier, Binary: ${message.metadata.binaryMode}, ` +
                 `Compression: ${message.metadata.compressionEnabled})`);

        this.emit('streamingStarted', {
            commandId: message.commandId,
            metadata: message.metadata
        });
    }

    /**
     * Handle streaming chunk
     */
    private async handleChunk(message: StreamingMessage): Promise<void> {
        if (!message.chunk) {
            throw new Error('Chunk message missing chunk data');
        }

        const session = this.activeSessions.get(message.commandId);
        if (!session) {
            throw new Error(`No active session for command ${message.commandId}`);
        }

        const chunk = message.chunk;
        session.chunks.push(chunk);
        session.receivedChunks++;
        session.totalChunks = chunk.totalChunks;
        session.lastChunkTime = new Date();
        session.elements.push(...chunk.elements);

        this.log(`📦 CHUNK ${chunk.chunkId}/${chunk.totalChunks}: Received ${chunk.elements.length} elements ` +
                 `(${chunk.progress.toFixed(1)}%, Memory: ${chunk.memoryUsage}MB)`);

        this.emit('chunkReceived', {
            commandId: message.commandId,
            chunk: chunk,
            session: session
        });

        // Check if complete
        if (chunk.isComplete || session.receivedChunks >= session.totalChunks) {
            await this.completeSession(message.commandId);
        }
    }

    /**
     * Handle streaming completion
     */
    private async handleCompletion(message: StreamingMessage): Promise<void> {
        await this.completeSession(message.commandId);
    }

    /**
     * Complete streaming session
     */
    private async completeSession(commandId: string): Promise<void> {
        const session = this.activeSessions.get(commandId);
        if (!session) {
            this.log(`⚠️ COMPLETION: No session found for ${commandId}`);
            return;
        }

        session.isComplete = true;
        const endTime = new Date();
        const totalTime = (endTime.getTime() - session.startTime.getTime()) / 1000;

        this.log(`🎉 STREAMING COMPLETE: ${session.elements.length} elements received in ` +
                 `${totalTime.toFixed(1)}s (${session.receivedChunks} chunks)`);

        // Create final selection data
        const selectionData = {
            elements: session.elements,
            count: session.elements.length,
            timestamp: new Date().toISOString(),
            viewName: session.metadata.viewName,
            documentTitle: session.metadata.documentTitle
        };

        this.emit('streamingComplete', {
            commandId: commandId,
            selectionData: selectionData,
            session: session,
            totalTime: totalTime
        });

        // Clean up session
        this.activeSessions.delete(commandId);
    }

    /**
     * Get active streaming sessions
     */
    getActiveSessions(): StreamingSession[] {
        return Array.from(this.activeSessions.values());
    }

    /**
     * Get session by command ID
     */
    getSession(commandId: string): StreamingSession | undefined {
        return this.activeSessions.get(commandId);
    }

    /**
     * Cancel streaming session
     */
    cancelSession(commandId: string): boolean {
        const session = this.activeSessions.get(commandId);
        if (session) {
            this.activeSessions.delete(commandId);
            this.log(`🚫 CANCELLED: Streaming session ${commandId}`);
            this.emit('streamingCancelled', { commandId });
            return true;
        }
        return false;
    }

    /**
     * Get streaming statistics
     */
    getStreamingStats() {
        const sessions = Array.from(this.activeSessions.values());
        return {
            activeSessions: sessions.length,
            totalElements: sessions.reduce((sum, s) => sum + s.elements.length, 0),
            avgChunkSize: sessions.length > 0 ? 
                sessions.reduce((sum, s) => sum + s.metadata.chunkSize, 0) / sessions.length : 0
        };
    }

    /**
     * Debug logging
     */
    private log(message: string): void {
        if (this.debugMode) {
            console.log(`[${new Date().toISOString()}] [STREAMING] ${message}`);
        }
    }
}
