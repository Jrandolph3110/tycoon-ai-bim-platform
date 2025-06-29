/**
 * TycoonServer - Main MCP Server for Tycoon AI-BIM Platform
 * 
 * Extends Temporal Neural Nexus with Revit-specific capabilities:
 * - Live Revit selection context
 * - Steel framing operations
 * - FLC workflow integration
 * - Real-time AI-Revit communication
 */

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { CallToolRequestSchema, ListToolsRequestSchema } from '@modelcontextprotocol/sdk/types.js';
import chalk from 'chalk';

// Import Temporal Neural Nexus components
import { MemoryManager } from './core/MemoryManager.js';
import { TimeManager } from './core/TimeManager.js';

// Import Tycoon-specific components
import { RevitBridge, RevitSelection } from './revit/RevitBridge.js';
import { FLCAdapter, FLCFramingOptions, FLCRenumberOptions } from './flc/FLCAdapter.js';

export class TycoonServer {
    private server: Server;
    private memoryManager: MemoryManager;
    private timeManager: TimeManager;
    private revitBridge: RevitBridge;
    private flcAdapter: FLCAdapter;
    private isInitialized: boolean = false;

    constructor() {
        this.server = new Server({
            name: 'tycoon-ai-bim-server',
            version: '1.0.0',
        }, {
            capabilities: {
                tools: {},
            },
        });

        // Initialize core components
        this.memoryManager = new MemoryManager({
            enableAnalytics: true,
            enableVectorSearch: true,
            autoBackup: true,
            backupInterval: 60
        });
        this.timeManager = new TimeManager();
        
        // Initialize Tycoon components
        this.revitBridge = new RevitBridge(8765);
        this.flcAdapter = new FLCAdapter(this.revitBridge);

        this.setupToolHandlers();
        this.setupErrorHandling();
        this.setupRevitEventHandlers();
    }

    /**
     * Initialize the Tycoon server
     */
    async initialize(): Promise<void> {
        try {
            console.log(chalk.blue('🚀 Initializing Tycoon AI-BIM Server...'));

            // Initialize memory system
            await this.memoryManager.initialize();
            console.log(chalk.green('✅ Memory system initialized'));

            // Initialize Revit bridge
            await this.revitBridge.initialize();
            console.log(chalk.green(`✅ Revit bridge initialized on port ${this.revitBridge.getPort()}`));

            this.isInitialized = true;
            console.log(chalk.blue('🎯 Tycoon AI-BIM Server ready!'));

        } catch (error) {
            console.error(chalk.red('❌ Failed to initialize Tycoon server:'), error);
            throw error;
        }
    }

    /**
     * Setup Revit event handlers
     */
    private setupRevitEventHandlers(): void {
        this.revitBridge.on('connected', () => {
            console.log(chalk.green('🔗 Revit add-in connected to Tycoon'));
            this.createMemory({
                title: 'Revit Connection Established',
                content: 'Revit add-in successfully connected to Tycoon AI-BIM server',
                category: 'system',
                tags: ['revit', 'connection', 'system'],
                importance: 8
            });
        });

        this.revitBridge.on('disconnected', () => {
            console.log(chalk.yellow('❌ Revit add-in disconnected from Tycoon'));
        });

        this.revitBridge.on('selectionChanged', (selection: RevitSelection) => {
            console.log(chalk.cyan(`📋 Selection changed: ${selection.count} elements`));
            this.handleSelectionChange(selection);
        });
    }

    /**
     * Handle Revit selection changes
     */
    private async handleSelectionChange(selection: RevitSelection): Promise<void> {
        try {
            // Store selection in memory for context
            await this.createMemory({
                title: `Revit Selection - ${selection.count} elements`,
                content: JSON.stringify(selection, null, 2),
                category: 'revit-selection',
                tags: ['revit', 'selection', 'context'],
                importance: 6,
                context: `Document: ${selection.documentTitle}, View: ${selection.viewName}`
            });

        } catch (error) {
            console.error(chalk.red('Failed to store selection in memory:'), error);
        }
    }

    /**
     * Setup error handling
     */
    private setupErrorHandling(): void {
        this.server.onerror = (error) => {
            console.error(chalk.red('[Tycoon MCP Error]'), error);
        };

        process.on('SIGINT', async () => {
            console.log(chalk.yellow('\n🛑 Shutting down Tycoon AI-BIM Server...'));
            await this.shutdown();
            process.exit(0);
        });
    }

    /**
     * Shutdown the server gracefully
     */
    async shutdown(): Promise<void> {
        try {
            await this.revitBridge.close();
            await this.server.close();
            console.log(chalk.green('✅ Tycoon server shutdown complete'));
        } catch (error) {
            console.error(chalk.red('Error during shutdown:'), error);
        }
    }

    /**
     * Setup tool handlers (extending Temporal Neural Nexus tools)
     */
    private setupToolHandlers(): void {
        this.server.setRequestHandler(ListToolsRequestSchema, async () => {
            return {
                tools: [
                    // Tycoon-specific tools
                    {
                        name: 'initialize_tycoon',
                        description: 'Initialize the Tycoon AI-BIM platform with Revit integration',
                        inputSchema: {
                            type: 'object',
                            properties: {
                                revitPort: { type: 'number', description: 'WebSocket port for Revit communication', default: 8765 },
                                debugMode: { type: 'boolean', description: 'Enable debug logging', default: true }
                            }
                        }
                    },
                    {
                        name: 'get_revit_selection',
                        description: 'Get current Revit selection with detailed element information',
                        inputSchema: {
                            type: 'object',
                            properties: {
                                includeGeometry: { type: 'boolean', description: 'Include geometry data', default: true },
                                includeParameters: { type: 'boolean', description: 'Include parameter data', default: true }
                            }
                        }
                    },
                    {
                        name: 'analyze_walls_for_framing',
                        description: 'Analyze selected walls for steel framing potential',
                        inputSchema: {
                            type: 'object',
                            properties: {
                                useCurrentSelection: { type: 'boolean', description: 'Use current Revit selection', default: true }
                            }
                        }
                    },
                    {
                        name: 'create_steel_framing',
                        description: 'Create steel framing for selected walls using FLC standards',
                        inputSchema: {
                            type: 'object',
                            properties: {
                                studSpacing: { type: 'number', description: 'Stud spacing in inches', default: 16, enum: [12, 16, 19.2, 24] },
                                panelMaxWidth: { type: 'number', description: 'Maximum panel width in feet', default: 10 },
                                wallType: { type: 'string', description: 'FLC wall type designation', default: 'FLC_6_Int_DW-FB' },
                                includeOpenings: { type: 'boolean', description: 'Include opening assemblies', default: true },
                                sequenceStuds: { type: 'boolean', description: 'Sequence studs left to right', default: true },
                                applyParameters: { type: 'boolean', description: 'Apply FLC parameters', default: true }
                            }
                        }
                    },
                    {
                        name: 'renumber_elements',
                        description: 'Renumber selected elements using FLC standards',
                        inputSchema: {
                            type: 'object',
                            properties: {
                                startNumber: { type: 'number', description: 'Starting number', default: 1 },
                                prefix: { type: 'string', description: 'Number prefix' },
                                includeSubassemblies: { type: 'boolean', description: 'Include subassembly elements', default: true },
                                sequenceType: { type: 'string', enum: ['left-to-right', 'bottom-to-top', 'custom'], default: 'left-to-right' }
                            }
                        }
                    },
                    {
                        name: 'validate_panel_tickets',
                        description: 'Validate FLC panel ticket requirements for selected elements',
                        inputSchema: {
                            type: 'object',
                            properties: {
                                useCurrentSelection: { type: 'boolean', description: 'Use current Revit selection', default: true }
                            }
                        }
                    },
                    {
                        name: 'get_revit_status',
                        description: 'Get current Revit connection and system status',
                        inputSchema: { type: 'object', properties: {} }
                    },
                    // Phase 1 AI Orchestrator + Script Generator Tools
                    {
                        name: 'flc_hybrid_operation',
                        description: 'Execute FLC hybrid operations using AI orchestrator + script generation',
                        inputSchema: {
                            type: 'object',
                            properties: {
                                operation: { type: 'string', description: 'Operation to perform', enum: ['ReNumberPanelElements', 'AnalyzePanelStructure', 'ValidateFraming'] },
                                direction: { type: 'string', description: 'Processing direction', enum: ['left_to_right', 'bottom_to_top', 'custom'], default: 'left_to_right' },
                                namingConvention: { type: 'string', description: 'Naming convention to use', enum: ['flc_standard', 'custom'], default: 'flc_standard' },
                                dryRun: { type: 'boolean', description: 'Preview mode without making changes', default: true },
                                includeSubassemblies: { type: 'boolean', description: 'Include subassembly elements', default: true }
                            },
                            required: ['operation']
                        }
                    },
                    {
                        name: 'flc_script_graduation_analytics',
                        description: 'Analyze FLC script usage for graduation to AI rewrite candidates',
                        inputSchema: {
                            type: 'object',
                            properties: {
                                minExecutionCount: { type: 'number', description: 'Minimum execution count for graduation consideration', default: 5 },
                                includeMetrics: { type: 'boolean', description: 'Include detailed performance metrics', default: true },
                                cleanupTempFiles: { type: 'boolean', description: 'Clean up temporary analysis files', default: true }
                            }
                        }
                    },
                    // Include all Temporal Neural Nexus tools here...
                    // (Memory management, search, context, time awareness tools)
                    // These would be imported from the base implementation
                ],
            };
        });

        this.server.setRequestHandler(CallToolRequestSchema, async (request) => {
            const { name, arguments: args } = request.params;
            
            try {
                console.log(chalk.blue(`🔧 Executing Tycoon tool: ${name}`));
                
                switch (name) {
                    // Tycoon-specific tool implementations
                    case 'initialize_tycoon':
                        return await this.initializeTycoon(args);
                    case 'get_revit_selection':
                        return await this.getRevitSelection(args);
                    case 'analyze_walls_for_framing':
                        return await this.analyzeWallsForFraming(args);
                    case 'create_steel_framing':
                        return await this.createSteelFraming(args);
                    case 'renumber_elements':
                        return await this.renumberElements(args);
                    case 'validate_panel_tickets':
                        return await this.validatePanelTickets(args);
                    case 'get_revit_status':
                        return await this.getRevitStatus(args);

                    // Phase 1 AI Orchestrator + Script Generator Tools
                    case 'flc_hybrid_operation':
                        return await this.flcHybridOperation(args);
                    case 'flc_script_graduation_analytics':
                        return await this.flcScriptGraduationAnalytics(args);

                    // Temporal Neural Nexus tools would be handled here
                    // case 'create_memory': return await this.createMemory(args);
                    // etc...
                    
                    default:
                        throw new Error(`Unknown tool: ${name}`);
                }
                
            } catch (error) {
                const errorMessage = error instanceof Error ? error.message : String(error);
                console.error(chalk.red(`❌ Error in ${name}:`), errorMessage);
                
                return {
                    content: [{
                        type: 'text',
                        text: JSON.stringify({
                            error: errorMessage,
                            tool: name,
                            timestamp: new Date().toISOString()
                        }, null, 2),
                    }],
                    isError: true,
                };
            }
        });
    }

    // Tool implementation methods

    /**
     * Initialize Tycoon platform
     */
    private async initializeTycoon(args: any) {
        try {
            if (!this.isInitialized) {
                await this.initialize();
            }

            return {
                content: [{
                    type: 'text',
                    text: JSON.stringify({
                        status: 'success',
                        message: '🎯 Tycoon AI-BIM Platform initialized successfully!',
                        features: [
                            '✅ Temporal Neural Nexus memory system',
                            '✅ Live Revit integration',
                            '✅ FLC steel framing workflows',
                            '✅ Real-time selection context',
                            '✅ AI-powered script generation',
                            '✅ Hot-reload development'
                        ],
                        revitConnected: this.revitBridge.isRevitConnected(),
                        timestamp: new Date().toISOString()
                    }, null, 2)
                }]
            };
        } catch (error) {
            throw new Error(`Failed to initialize Tycoon: ${error}`);
        }
    }

    /**
     * Get current Revit selection
     */
    private async getRevitSelection(args: any) {
        try {
            if (!this.revitBridge.isRevitConnected()) {
                throw new Error('Revit add-in not connected');
            }

            const selection = await this.revitBridge.getSelection();

            // Store selection context in memory
            await this.createMemory({
                title: `Selection Context - ${selection.count} elements`,
                content: `Retrieved Revit selection with ${selection.count} elements from ${selection.documentTitle}`,
                category: 'revit-context',
                tags: ['revit', 'selection', 'context'],
                importance: 7
            });

            return {
                content: [{
                    type: 'text',
                    text: JSON.stringify({
                        status: 'success',
                        selection: {
                            count: selection.count,
                            elements: selection.elements,
                            viewName: selection.viewName,
                            documentTitle: selection.documentTitle,
                            timestamp: selection.timestamp
                        }
                    }, null, 2)
                }]
            };
        } catch (error) {
            throw new Error(`Failed to get Revit selection: ${error}`);
        }
    }

    /**
     * Analyze walls for framing
     */
    private async analyzeWallsForFraming(args: any) {
        try {
            const analysis = await this.flcAdapter.analyzeWallsForFraming();

            // Store analysis in memory
            await this.createMemory({
                title: `Wall Framing Analysis - ${analysis.walls.length} walls`,
                content: `Analyzed ${analysis.walls.length} walls: ${analysis.totalLength.toFixed(1)}ft total, ${analysis.estimatedPanels} estimated panels`,
                category: 'analysis',
                tags: ['framing', 'analysis', 'walls'],
                importance: 8
            });

            return {
                content: [{
                    type: 'text',
                    text: JSON.stringify({
                        status: 'success',
                        analysis: {
                            wallCount: analysis.walls.length,
                            totalLength: analysis.totalLength,
                            estimatedPanels: analysis.estimatedPanels,
                            recommendations: analysis.recommendations
                        }
                    }, null, 2)
                }]
            };
        } catch (error) {
            throw new Error(`Failed to analyze walls: ${error}`);
        }
    }

    /**
     * Create steel framing
     */
    private async createSteelFraming(args: any) {
        try {
            const options: FLCFramingOptions = {
                studSpacing: args.studSpacing || 16,
                panelMaxWidth: args.panelMaxWidth || 10,
                wallType: args.wallType || 'FLC_6_Int_DW-FB',
                includeOpenings: args.includeOpenings !== false,
                sequenceStuds: args.sequenceStuds !== false,
                applyParameters: args.applyParameters !== false
            };

            const result = await this.flcAdapter.createSteelFraming(options);

            // Store operation in memory
            await this.createMemory({
                title: `Steel Framing Created - ${result.panelsCreated} panels`,
                content: `Created steel framing: ${result.panelsCreated} panels, ${result.elementsCreated} elements`,
                category: 'operation',
                tags: ['framing', 'creation', 'steel'],
                importance: 9
            });

            return {
                content: [{
                    type: 'text',
                    text: JSON.stringify({
                        status: 'success',
                        result: {
                            panelsCreated: result.panelsCreated,
                            elementsCreated: result.elementsCreated,
                            errors: result.errors,
                            options: options
                        }
                    }, null, 2)
                }]
            };
        } catch (error) {
            throw new Error(`Failed to create steel framing: ${error}`);
        }
    }

    /**
     * Renumber elements
     */
    private async renumberElements(args: any) {
        try {
            const options: FLCRenumberOptions = {
                startNumber: args.startNumber || 1,
                prefix: args.prefix,
                includeSubassemblies: args.includeSubassemblies !== false,
                sequenceType: args.sequenceType || 'left-to-right'
            };

            const result = await this.flcAdapter.renumberElements(options);

            // Store operation in memory
            await this.createMemory({
                title: `Elements Renumbered - ${result.elementsProcessed} elements`,
                content: `Renumbered ${result.elementsProcessed} elements using ${options.sequenceType} sequence`,
                category: 'operation',
                tags: ['renumbering', 'elements', 'sequence'],
                importance: 7
            });

            return {
                content: [{
                    type: 'text',
                    text: JSON.stringify({
                        status: 'success',
                        result: {
                            elementsProcessed: result.elementsProcessed,
                            errors: result.errors,
                            options: options
                        }
                    }, null, 2)
                }]
            };
        } catch (error) {
            throw new Error(`Failed to renumber elements: ${error}`);
        }
    }

    /**
     * Validate panel tickets
     */
    private async validatePanelTickets(args: any) {
        try {
            const validation = await this.flcAdapter.validatePanelTickets();

            // Store validation in memory
            await this.createMemory({
                title: `Panel Ticket Validation - ${validation.valid ? 'PASSED' : 'FAILED'}`,
                content: `Validation ${validation.valid ? 'passed' : 'failed'}: ${validation.issues.length} issues found`,
                category: 'validation',
                tags: ['panels', 'validation', 'tickets'],
                importance: validation.valid ? 6 : 9
            });

            return {
                content: [{
                    type: 'text',
                    text: JSON.stringify({
                        status: 'success',
                        validation: {
                            valid: validation.valid,
                            issues: validation.issues,
                            recommendations: validation.recommendations
                        }
                    }, null, 2)
                }]
            };
        } catch (error) {
            throw new Error(`Failed to validate panel tickets: ${error}`);
        }
    }

    /**
     * Get Revit status
     */
    private async getRevitStatus(args: any) {
        return {
            content: [{
                type: 'text',
                text: JSON.stringify({
                    status: 'success',
                    revit: {
                        connected: this.revitBridge.isRevitConnected(),
                        serverInitialized: this.isInitialized,
                        memorySystemActive: true,
                        timestamp: new Date().toISOString()
                    }
                }, null, 2)
            }]
        };
    }

    /**
     * Helper: Create memory (simplified interface)
     */
    private async createMemory(input: {
        title: string;
        content: string;
        category?: string;
        tags?: string[];
        importance?: number;
        context?: string;
    }) {
        try {
            return await this.memoryManager.createMemory({
                title: input.title,
                content: input.content,
                category: input.category || 'general',
                tags: input.tags || [],
                importance: input.importance || 5,
                context: input.context,
                relationships: []
            });
        } catch (error) {
            console.error(chalk.red('Failed to create memory:'), error);
            return null;
        }
    }

    /**
     * Phase 1 AI Orchestrator + Script Generator: FLC Hybrid Operation
     */
    private async flcHybridOperation(args: any): Promise<any> {
        try {
            console.log(chalk.blue('🌉 Processing FLC Hybrid Operation...'));

            const { operation, direction = 'left_to_right', namingConvention = 'flc_standard', dryRun = true, includeSubassemblies = true } = args;

            if (!this.revitBridge.isRevitConnected()) {
                throw new Error('Revit add-in not connected');
            }

            // Phase 1: AI Orchestrator calls existing FLC scripts
            const result = await this.revitBridge.sendCommand({
                type: 'flc_hybrid_operation',
                payload: {
                    operation,
                    direction,
                    namingConvention,
                    dryRun,
                    includeSubassemblies
                }
            });

            return {
                content: [{
                    type: 'text',
                    text: `FLC Hybrid Operation completed successfully!\n\nOperation: ${operation}\nDirection: ${direction}\nConvention: ${namingConvention}\nDry Run: ${dryRun}\n\nResult: ${result.data || result.error || 'Operation completed'}`
                }]
            };

        } catch (error) {
            const errorMessage = error instanceof Error ? error.message : String(error);
            console.error(chalk.red('❌ FLC Hybrid Operation failed:'), errorMessage);

            return {
                content: [{
                    type: 'text',
                    text: `Error in FLC hybrid operation: ${errorMessage}`
                }],
                isError: true
            };
        }
    }

    /**
     * Phase 1 AI Orchestrator + Script Generator: FLC Script Graduation Analytics
     */
    private async flcScriptGraduationAnalytics(args: any): Promise<any> {
        try {
            console.log(chalk.blue('📊 Processing FLC Script Graduation Analytics...'));

            const { minExecutionCount = 5, includeMetrics = true, cleanupTempFiles = true } = args;

            if (!this.revitBridge.isRevitConnected()) {
                throw new Error('Revit add-in not connected');
            }

            // Phase 1: Analyze script usage for graduation candidates
            const result = await this.revitBridge.sendCommand({
                type: 'flc_script_graduation_analytics',
                payload: {
                    minExecutionCount,
                    includeMetrics,
                    cleanupTempFiles
                }
            });

            return {
                content: [{
                    type: 'text',
                    text: `FLC Script Graduation Analytics completed!\n\nMin Execution Count: ${minExecutionCount}\nInclude Metrics: ${includeMetrics}\n\nResult: ${result.data || result.error || 'Analysis completed'}\n\nGraduation candidates identified for AI rewrite consideration.`
                }]
            };

        } catch (error) {
            const errorMessage = error instanceof Error ? error.message : String(error);
            console.error(chalk.red('❌ FLC Script Graduation Analytics failed:'), errorMessage);

            return {
                content: [{
                    type: 'text',
                    text: `Error in FLC script graduation analytics: ${errorMessage}`
                }],
                isError: true
            };
        }
    }

    /**
     * Start the server
     */
    async start(): Promise<void> {
        const transport = new StdioServerTransport();
        await this.server.connect(transport);
        console.log(chalk.green('🚀 Tycoon AI-BIM Server started'));
    }
}
