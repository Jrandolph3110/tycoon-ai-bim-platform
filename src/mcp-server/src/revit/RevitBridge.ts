/**
 * RevitBridge - Communication bridge between Tycoon MCP Server and Revit Add-In
 * 
 * Handles:
 * - WebSocket communication with Revit add-in
 * - Selection context management
 * - Element data serialization
 * - Real-time command execution
 * - Error handling and logging
 */

import { WebSocketServer, WebSocket } from 'ws';
import { EventEmitter } from 'events';
import { createServer } from 'net';
import chalk from 'chalk';
import { BinaryStreamingHandler } from './BinaryStreamingHandler.js';
import { RevitToBimAdapter } from '../adapters/RevitToBimAdapter.js';
import { BimVectorDatabase } from '../core/BimVectorDatabase.js';

export interface RevitElement {
    id: string;
    elementId: number;
    category: string;
    familyName?: string;
    typeName?: string;
    parameters: Record<string, any>;
    geometry?: {
        location?: { x: number; y: number; z: number };
        boundingBox?: {
            min: { x: number; y: number; z: number };
            max: { x: number; y: number; z: number };
        };
    };
    relationships?: {
        hostId?: string;
        dependentIds?: string[];
    };
}

export interface RevitSelection {
    elements: RevitElement[];
    count: number;
    timestamp: string;
    viewId?: string;
    viewName?: string;
    documentTitle?: string;
}

export interface RevitCommand {
    id: string;
    type: 'selection' | 'create' | 'modify' | 'delete' | 'query';
    payload: any;
    timestamp: string;
}

export interface RevitResponse {
    commandId: string;
    success: boolean;
    data?: any;
    error?: string;
    timestamp: string;
}

export class RevitBridge extends EventEmitter {
    private wss: WebSocketServer | null = null;
    private revitConnection: WebSocket | null = null;
    private streamingHandler: BinaryStreamingHandler;
    private bimAdapter: RevitToBimAdapter | null = null;
    private port: number;
    private isConnected: boolean = false;
    private pendingCommands: Map<string, { resolve: Function; reject: Function; timeout: NodeJS.Timeout }> = new Map();
    private commandTimeout: number = 30000; // 30 seconds
    private debugMode: boolean = true;

    constructor(preferredPort: number = 8765) {
        super();
        this.port = preferredPort;

        // Initialize binary streaming handler
        this.streamingHandler = new BinaryStreamingHandler(this.debugMode);
        this.setupStreamingHandlers();

        console.log("Bridge constructed"); // ChatGPT's test #2
    }

    /**
     * Set BIM Vector Database for direct streaming storage
     */
    setBimVectorDatabase(bimVectorDb: BimVectorDatabase): void {
        this.bimAdapter = new RevitToBimAdapter(bimVectorDb);
        console.log(chalk.green('✅ BIM Vector Database adapter configured'));
    }

    /**
     * Setup streaming event handlers
     */
    private setupStreamingHandlers(): void {
        this.streamingHandler.on('streamingStarted', async (event) => {
            this.log(`🚀 STREAMING: Started for ${event.metadata.totalElements} elements (${event.metadata.processingTier} tier)`);

            // Start BIM adapter session if available
            if (this.bimAdapter) {
                this.bimAdapter.startStreamingSession({
                    documentTitle: event.metadata.documentTitle || 'Unknown Project',
                    viewName: event.metadata.viewName,
                    processingTier: event.metadata.processingTier,
                    chunkNumber: 0,
                    streamingSession: event.commandId,
                    totalElements: event.metadata.totalElements,
                    processedElements: 0
                });
            }
        });

        this.streamingHandler.on('chunkReceived', async (event) => {
            this.log(`📦 CHUNK: ${event.chunk.chunkId}/${event.chunk.totalChunks} (${event.chunk.progress.toFixed(1)}%)`);

            // Process chunk through BIM adapter if available
            if (this.bimAdapter && event.chunk.elements) {
                try {
                    const result = await this.bimAdapter.processChunk(
                        event.chunk.elements,
                        {
                            documentTitle: event.session.metadata.documentTitle || 'Unknown Project',
                            viewName: event.session.metadata.viewName,
                            processingTier: event.session.metadata.processingTier,
                            chunkNumber: event.chunk.chunkId,
                            streamingSession: event.commandId,
                            totalElements: event.session.metadata.totalElements,
                            processedElements: event.chunk.chunkId * event.session.metadata.chunkSize
                        }
                    );

                    if (result.errors.length > 0) {
                        this.logError(`BIM adapter errors in chunk ${event.chunk.chunkId}`, result.errors);
                    }
                } catch (error) {
                    this.logError(`Failed to process chunk ${event.chunk.chunkId} through BIM adapter`, error);
                }
            }
        });

        this.streamingHandler.on('streamingComplete', async (event) => {
            this.log(`🎉 STREAMING: Complete! ${event.selectionData.count} elements in ${event.totalTime.toFixed(1)}s`);

            // Complete BIM adapter session if available
            let bimStats = null;
            if (this.bimAdapter) {
                try {
                    bimStats = await this.bimAdapter.completeSession({
                        documentTitle: event.session.metadata.documentTitle || 'Unknown Project',
                        viewName: event.session.metadata.viewName,
                        processingTier: event.session.metadata.processingTier,
                        chunkNumber: event.session.receivedChunks,
                        streamingSession: event.commandId,
                        totalElements: event.session.metadata.totalElements,
                        processedElements: event.selectionData.count
                    });
                } catch (error) {
                    this.logError('Failed to complete BIM adapter session', error);
                }
            }

            // Emit the completed selection data with BIM stats
            this.emit('selectionReceived', {
                commandId: event.commandId,
                selection: event.selectionData,
                streamingStats: {
                    totalTime: event.totalTime,
                    chunksReceived: event.session.receivedChunks,
                    avgChunkSize: event.session.metadata.chunkSize
                },
                bimVectorStats: bimStats
            });
        });

        this.streamingHandler.on('error', (error) => {
            this.logError('Streaming error', error);
        });
    }

    /**
     * Find an available port starting from the preferred port
     */
    private async findAvailablePort(startPort: number = 8765): Promise<number> {
        const maxAttempts = 100; // Try 100 ports

        for (let port = startPort; port < startPort + maxAttempts; port++) {
            if (await this.isPortAvailable(port)) {
                return port;
            }
        }

        throw new Error(`No available port found in range ${startPort}-${startPort + maxAttempts}`);
    }

    /**
     * Check if a port is available
     */
    private async isPortAvailable(port: number): Promise<boolean> {
        return new Promise((resolve) => {
            const server = createServer();

            server.listen(port, () => {
                server.once('close', () => {
                    resolve(true);
                });
                server.close();
            });

            server.on('error', () => {
                resolve(false);
            });
        });
    }

    /**
     * Initialize the WebSocket server for Revit communication
     */
    async initialize(): Promise<void> {
        try {
            // Find an available port
            this.port = await this.findAvailablePort(this.port);
            console.log(`🔍 DEBUG: Found available port: ${this.port}`);
            this.log(`🔍 Found available port: ${this.port}`);

            console.log(`🚀 DEBUG: Creating WebSocketServer on port ${this.port}`);
            this.wss = new WebSocketServer({
                port: this.port,
                perMessageDeflate: false
            });
            console.log(`✅ DEBUG: WebSocketServer created successfully`);

            console.log(`🔧 DEBUG: Registering connection event handler...`);
            this.wss.on('connection', (ws: WebSocket) => {
                console.log("CONN!", Date.now()); // ChatGPT's test #1
                console.log(`🎉 DEBUG: CONNECTION EVENT FIRED! isConnected will be set to true`);
                this.log('🔗 Revit add-in connected');
                this.revitConnection = ws;
                this.isConnected = true;
                console.log(`✅ DEBUG: isConnected is now: ${this.isConnected}`);

                ws.on('message', async (data: Buffer) => {
                    try {
                        // Check if this is a streaming message (binary or JSON)
                        if (this.isStreamingMessage(data)) {
                            // Handle via streaming handler
                            await this.streamingHandler.handleMessage(data);
                        } else {
                            // Handle as traditional JSON message
                            const message = JSON.parse(data.toString());
                            this.handleRevitMessage(message);
                        }
                    } catch (error) {
                        this.logError('Failed to parse message from Revit', error);
                    }
                });

                ws.on('close', () => {
                    this.log('❌ Revit add-in disconnected');
                    this.isConnected = false;
                    this.revitConnection = null;
                    this.emit('disconnected');
                });

                ws.on('error', (error) => {
                    this.logError('WebSocket error', error);
                    this.emit('error', error);
                });

                this.emit('connected');
            });
            console.log(`✅ DEBUG: Connection event handler registered successfully`);

            console.log(`🚀 DEBUG: About to start listening on port ${this.port}`);
            this.log(`🚀 Tycoon RevitBridge listening on port ${this.port}`);
            console.log(`✅ DEBUG: WebSocket server is now listening and ready for connections`);
            
        } catch (error) {
            this.logError('Failed to initialize RevitBridge', error);
            throw error;
        }
    }

    /**
     * 🦝💨 FAFB Enhanced Send command to Revit with dynamic timeout support
     */
    async sendCommand(command: Omit<RevitCommand, 'id' | 'timestamp'>, customTimeout?: number): Promise<RevitResponse> {
        if (!this.isConnected || !this.revitConnection) {
            throw new Error('Revit add-in not connected');
        }

        const fullCommand: RevitCommand = {
            ...command,
            id: this.generateCommandId(),
            timestamp: new Date().toISOString()
        };

        return new Promise((resolve, reject) => {
            // 🦝💨 FAFB Enhanced timeout with custom timeout support
            const timeoutMs = customTimeout || this.commandTimeout;
            const timeout = setTimeout(() => {
                this.pendingCommands.delete(fullCommand.id);
                reject(new Error(`Command timeout: ${fullCommand.type} (${timeoutMs}ms)`));
            }, timeoutMs);

            // Store pending command
            this.pendingCommands.set(fullCommand.id, { resolve, reject, timeout });

            // Send command
            try {
                this.revitConnection!.send(JSON.stringify(fullCommand));
                this.log(`📤 Sent command: ${fullCommand.type} (${fullCommand.id})`);
            } catch (error) {
                this.pendingCommands.delete(fullCommand.id);
                clearTimeout(timeout);
                reject(error);
            }
        });
    }

    /**
     * 🦝💨 FAFB Enhanced Get current Revit selection with massive selection support
     */
    async getSelection(): Promise<RevitSelection> {
        const response = await this.sendCommand({
            type: 'selection',
            payload: { action: 'get' }
        }, this.getSelectionTimeout());

        if (!response.success) {
            throw new Error(`Failed to get selection: ${response.error}`);
        }

        return response.data as RevitSelection;
    }

    /**
     * 🦝💨 FAFB Dynamic timeout calculation based on selection size
     */
    private getSelectionTimeout(): number {
        // Default timeout for unknown selection sizes
        // The Revit add-in will determine the actual tier and adjust processing accordingly
        return 20 * 60 * 1000; // 20 minutes for maximum safety
    }

    /**
     * 🚀 FAFB Handle progress updates during large selections
     */
    private handleProgressUpdate(message: any): void {
        const { id, status, processed, total, message: progressMessage } = message;

        this.log(`📊 PROGRESS: ${status} - ${progressMessage} (${processed}/${total})`);

        // If this is for a pending command, extend its timeout
        if (id && this.pendingCommands.has(id)) {
            const pending = this.pendingCommands.get(id)!;

            // Clear old timeout and set new one
            clearTimeout(pending.timeout);

            // Extend timeout based on progress - give more time for large selections
            const remainingElements = total - processed;
            const estimatedTimeMs = Math.max(60000, remainingElements * 10); // At least 1 minute, 10ms per element

            pending.timeout = setTimeout(() => {
                this.pendingCommands.delete(id);
                pending.reject(new Error(`Command timeout: selection (extended timeout after progress)`));
            }, estimatedTimeMs);

            this.log(`⏱️ Extended timeout for ${id}: ${estimatedTimeMs/1000}s (${remainingElements} elements remaining)`);
        }
    }

    /**
     * Check if message is a streaming message
     */
    private isStreamingMessage(data: Buffer): boolean {
        try {
            // Check for binary MessagePack header
            if (data.length > 8) {
                const header = data.slice(0, 8).toString('utf8');
                if (header.startsWith('MSGPACK:')) {
                    return true;
                }
            }

            // Check for JSON streaming message types
            const text = data.toString('utf8');
            if (text.includes('"type":"streaming_')) {
                return true;
            }

            return false;
        } catch {
            return false;
        }
    }

    /**
     * Handle incoming messages from Revit
     */
    private handleRevitMessage(message: any): void {
        this.log(`📥 Received from Revit: ${message.type || 'unknown'}`);

        if (message.commandId && this.pendingCommands.has(message.commandId)) {
            // Handle command response
            const pending = this.pendingCommands.get(message.commandId)!;
            this.pendingCommands.delete(message.commandId);
            clearTimeout(pending.timeout);

            if (message.success) {
                pending.resolve(message);
            } else {
                pending.reject(new Error(message.error || 'Unknown error'));
            }
        } else if (message.type === 'selection_changed') {
            // Handle selection change notification
            this.emit('selectionChanged', message.data);
        } else if (message.type === 'heartbeat') {
            // Handle heartbeat
            this.sendHeartbeatResponse();
        } else if (message.type === 'heartbeat_response') {
            // Handle heartbeat response - this is normal, don't log as unknown
            // Just ignore it silently
        } else if (message.type === 'selection_progress') {
            // 🚀 FAFB Handle progress updates during large selections
            this.handleProgressUpdate(message);
        } else {
            console.warn("Unknown", message.type); // ChatGPT's test #3
            this.log(`⚠️ Unhandled message type: ${message.type}`);
            // DON'T flip any state here
        }
    }

    /**
     * Send heartbeat response to keep connection alive
     */
    private sendHeartbeatResponse(): void {
        if (this.revitConnection) {
            this.revitConnection.send(JSON.stringify({
                type: 'heartbeat_response',
                timestamp: new Date().toISOString()
            }));
        }
    }

    /**
     * Generate unique command ID
     */
    private generateCommandId(): string {
        return `cmd_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
    }

    /**
     * Check if connected to Revit
     */
    isRevitConnected(): boolean {
        console.log(`🔍 DEBUG: isRevitConnected() called - returning: ${this.isConnected}`);
        console.log(`🔍 DEBUG: revitConnection exists: ${this.revitConnection !== null}`);
        console.log(`🔍 DEBUG: wss exists: ${this.wss !== null}`);
        return this.isConnected;
    }

    /**
     * Get the current port being used
     */
    getPort(): number {
        return this.port;
    }

    /**
     * Close the bridge
     */
    async close(): Promise<void> {
        if (this.revitConnection) {
            this.revitConnection.close();
        }
        if (this.wss) {
            this.wss.close();
        }
        this.log('🛑 RevitBridge closed');
    }

    /**
     * Enable/disable debug logging
     */
    setDebugMode(enabled: boolean): void {
        this.debugMode = enabled;
    }

    /**
     * Log debug messages
     */
    private log(message: string): void {
        if (this.debugMode) {
            console.log(chalk.cyan('[RevitBridge]'), message);
        }
    }

    /**
     * Log error messages
     */
    private logError(message: string, error?: any): void {
        console.error(chalk.red('[RevitBridge Error]'), message, error || '');
    }
}
