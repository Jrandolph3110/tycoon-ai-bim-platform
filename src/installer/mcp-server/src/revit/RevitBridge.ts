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
    type: 'selection' | 'create' | 'modify' | 'delete' | 'query' | 'ai_rename_panel_elements' | 'ai_modify_parameters' | 'ai_analyze_panel_structure' | 'flc_hybrid_operation' | 'flc_script_graduation_analytics';
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
    private port: number;
    private isConnected: boolean = false;
    private pendingCommands: Map<string, {
        resolve: Function;
        reject: Function;
        timeout: NodeJS.Timeout;
        progressMonitor?: NodeJS.Timeout;
        updateProgress?: Function;
    }> = new Map();

    // Expert-recommended timeout configuration (o3 pro insights)
    private readonly TRANSPORT_TIMEOUT = 5000; // 5s transport timeout
    private readonly EXECUTION_TIMEOUT = 30000; // 30s execution timeout
    private readonly PING_INTERVAL = 25000; // 25s ping interval (< 30s recommended)
    private pingTimer: NodeJS.Timeout | null = null;
    private commandTimeout: number = 30000; // 30 seconds
    private debugMode: boolean = true;

    constructor(preferredPort: number = 8765) {
        super();
        this.port = preferredPort;
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
            this.log(`🔍 Found available port: ${this.port}`);

            this.wss = new WebSocketServer({
                port: this.port,
                perMessageDeflate: false
            });

            this.wss.on('connection', (ws: WebSocket) => {
                this.log('🔗 Revit add-in connected');
                this.revitConnection = ws;
                this.isConnected = true;

                ws.on('message', (data: Buffer) => {
                    try {
                        const message = JSON.parse(data.toString());
                        this.handleRevitMessage(message);
                    } catch (error) {
                        this.logError('Failed to parse message from Revit', error);
                    }
                });

                ws.on('close', (code, reason) => {
                    this.log(`❌ Revit add-in disconnected - Code: ${code}, Reason: ${reason}`);
                    this.isConnected = false;
                    this.revitConnection = null;
                    this.stopPingPong();
                    this.emit('disconnected');
                });

                ws.on('error', (error) => {
                    this.logError('WebSocket error', error);
                    this.emit('error', error);
                });

                // Handle pong responses (expert recommendation)
                ws.on('pong', () => {
                    this.log('🏓 Pong received from Revit');
                });

                // Start ping/pong mechanism (expert recommendation)
                this.startPingPong();

                this.emit('connected');
            });

            this.log(`🚀 Tycoon RevitBridge listening on port ${this.port}`);
            
        } catch (error) {
            this.logError('Failed to initialize RevitBridge', error);
            throw error;
        }
    }

    /**
     * Send command to Revit and wait for response
     */
    async sendCommand(command: Omit<RevitCommand, 'id' | 'timestamp'>): Promise<RevitResponse> {
        if (!this.isConnected || !this.revitConnection) {
            throw new Error('Revit add-in not connected');
        }

        const fullCommand: RevitCommand = {
            ...command,
            id: this.generateCommandId(),
            timestamp: new Date().toISOString()
        };

        return new Promise((resolve, reject) => {
            let lastProgressTime = Date.now();
            let progressPingCount = 0;

            // Set up progress ping monitoring for long-running commands
            const progressMonitor = setInterval(() => {
                const timeSinceProgress = Date.now() - lastProgressTime;
                if (timeSinceProgress > 5000) { // 5 seconds without progress
                    progressPingCount++;
                    this.log(`⏳ [${fullCommand.id}] Waiting for command completion... (${progressPingCount * 5}s elapsed)`);

                    if (progressPingCount > 12) { // 60 seconds total
                        clearInterval(progressMonitor);
                        this.pendingCommands.delete(fullCommand.id);
                        reject(new Error(`Command timeout after 60s: ${fullCommand.type}`));
                    }
                }
            }, 5000); // Check every 5 seconds

            // Set up main timeout (expert-recommended values)
            const isAICommand = fullCommand.type.startsWith('ai_') || fullCommand.type.startsWith('flc_');
            const timeoutMs = isAICommand ? this.EXECUTION_TIMEOUT : this.TRANSPORT_TIMEOUT; // 30s for AI/FLC commands, 5s for others

            const timeout = setTimeout(() => {
                clearInterval(progressMonitor);
                this.pendingCommands.delete(fullCommand.id);
                reject(new Error(`Command timeout after ${timeoutMs}ms: ${fullCommand.type}`));
            }, timeoutMs);

            // Store pending command with progress tracking
            this.pendingCommands.set(fullCommand.id, {
                resolve: (response: RevitResponse) => {
                    clearInterval(progressMonitor);
                    clearTimeout(timeout);
                    resolve(response);
                },
                reject: (error: Error) => {
                    clearInterval(progressMonitor);
                    clearTimeout(timeout);
                    reject(error);
                },
                timeout,
                progressMonitor,
                updateProgress: () => {
                    lastProgressTime = Date.now();
                }
            });

            // Send command
            try {
                this.revitConnection!.send(JSON.stringify(fullCommand));
                this.log(`📤 Sent command: ${fullCommand.type} (${fullCommand.id}) | Timeout: ${timeoutMs}ms`);
            } catch (error) {
                clearInterval(progressMonitor);
                clearTimeout(timeout);
                this.pendingCommands.delete(fullCommand.id);
                reject(error);
            }
        });
    }

    /**
     * Get current Revit selection
     */
    async getSelection(): Promise<RevitSelection> {
        const response = await this.sendCommand({
            type: 'selection',
            payload: { action: 'get' }
        });

        if (!response.success) {
            throw new Error(`Failed to get selection: ${response.error}`);
        }

        return response.data as RevitSelection;
    }

    /**
     * Handle incoming messages from Revit
     */
    private handleRevitMessage(message: any): void {
        this.log(`📥 Received from Revit: ${message.type || 'unknown'}`);

        // Fix: Revit sends "id" but we were looking for "commandId"
        const commandId = message.commandId || message.id;

        if (commandId && this.pendingCommands.has(commandId)) {
            const pending = this.pendingCommands.get(commandId)!;

            // Handle progress ping
            if (message.type === 'progress') {
                this.log(`⏳ [${commandId}] Progress update: ${message.message || 'Processing...'}`);
                if (pending.updateProgress) {
                    pending.updateProgress();
                }
                return; // Don't resolve/reject on progress updates
            }

            // Handle command response (completion)
            this.log(`✅ [${commandId}] Received response: ${message.success ? 'Success' : 'Failed'}`);
            this.pendingCommands.delete(commandId);
            clearTimeout(pending.timeout);
            if (pending.progressMonitor) {
                clearInterval(pending.progressMonitor);
            }

            if (message.success) {
                pending.resolve(message);
            } else {
                pending.reject(new Error(message.error || message.message || 'Unknown error'));
            }
        } else if (message.type === 'selection_changed') {
            // Handle selection change notification
            this.emit('selectionChanged', message.data);
        } else if (message.type === 'heartbeat') {
            // Handle heartbeat
            this.sendHeartbeatResponse();
        } else {
            this.log(`⚠️ Unhandled message type: ${message.type}`);
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

    /**
     * Start ping/pong mechanism (expert recommendation)
     */
    private startPingPong(): void {
        if (this.pingTimer) {
            clearInterval(this.pingTimer);
        }

        this.pingTimer = setInterval(() => {
            if (this.revitConnection && this.isConnected) {
                this.log('🏓 Sending ping to Revit');
                this.revitConnection.ping();
            }
        }, this.PING_INTERVAL);
    }

    /**
     * Stop ping/pong mechanism
     */
    private stopPingPong(): void {
        if (this.pingTimer) {
            clearInterval(this.pingTimer);
            this.pingTimer = null;
        }
    }
}
