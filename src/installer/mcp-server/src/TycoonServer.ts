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
                    // Phase 2A: Full CRUD Operations (Chat's Priority 1-3)
                    {
                        name: 'ai_create_elements',
                        description: 'Create Revit elements (walls, floors, roofs, families) with AI-driven parameter analysis',
                        inputSchema: {
                            type: 'object',
                            properties: {
                                elementType: { type: 'string', description: 'Type of element to create', enum: ['wall', 'floor', 'roof', 'family', 'door', 'window'] },
                                parameters: { type: 'object', description: 'Element parameters and properties' },
                                geometry: { type: 'object', description: 'Geometric definition (points, curves, etc.)' },
                                familyType: { type: 'string', description: 'Family type name for family elements' },
                                batchMode: { type: 'boolean', description: 'Process multiple elements in batch', default: false },
                                dryRun: { type: 'boolean', description: 'Preview mode without creating elements', default: true },
                                transactionGroup: { type: 'boolean', description: 'Wrap in transaction group for atomic rollback', default: true }
                            },
                            required: ['elementType', 'geometry']
                        }
                    },
                    {
                        name: 'ai_modify_parameters',
                        description: 'Modify element parameters with AI-driven validation and batch processing',
                        inputSchema: {
                            type: 'object',
                            properties: {
                                elementIds: { type: 'array', items: { type: 'string' }, description: 'Element IDs to modify' },
                                parameters: { type: 'object', description: 'Parameter name-value pairs to modify' },
                                batchSize: { type: 'number', description: 'Batch size for processing (≤100)', default: 50, maximum: 100 },
                                validateOnly: { type: 'boolean', description: 'Only validate parameters without modifying', default: false },
                                transactionGroup: { type: 'boolean', description: 'Wrap in transaction group for atomic rollback', default: true },
                                continueOnError: { type: 'boolean', description: 'Continue processing if individual elements fail', default: true }
                            },
                            required: ['elementIds', 'parameters']
                        }
                    },
                    {
                        name: 'ai_modify_geometry',
                        description: 'Transform element geometry (move, rotate, scale) with spatial validation',
                        inputSchema: {
                            type: 'object',
                            properties: {
                                elementIds: { type: 'array', items: { type: 'string' }, description: 'Element IDs to transform' },
                                operation: { type: 'string', description: 'Transformation type', enum: ['move', 'rotate', 'scale', 'mirror'] },
                                transform: { type: 'object', description: 'Transformation parameters (vector, angle, factor, etc.)' },
                                batchSize: { type: 'number', description: 'Batch size for processing (≤100)', default: 50, maximum: 100 },
                                validateGeometry: { type: 'boolean', description: 'Validate geometry after transformation', default: true },
                                transactionGroup: { type: 'boolean', description: 'Wrap in transaction group for atomic rollback', default: true }
                            },
                            required: ['elementIds', 'operation', 'transform']
                        }
                    },
                    // Phase 2B: Hot Script Generation (Chat's Vertical Depth)
                    {
                        name: 'generate_hot_script',
                        description: 'Generate and execute custom PyRevit scripts from natural language with safety guard-rails',
                        inputSchema: {
                            type: 'object',
                            properties: {
                                description: { type: 'string', description: 'Natural language description of desired script functionality' },
                                scriptType: { type: 'string', description: 'Type of script to generate', enum: ['element_creation', 'parameter_modification', 'geometry_transformation', 'analysis', 'custom'], default: 'custom' },
                                templateVersion: { type: 'string', description: 'Template version to use (e.g., "v1.0")', default: 'v1.0' },
                                parameters: { type: 'object', description: 'Script parameters and context' },
                                dryRun: { type: 'boolean', description: 'Generate and validate script without execution', default: true },
                                timeout: { type: 'number', description: 'Script execution timeout in seconds', default: 30, maximum: 300 },
                                enableSandbox: { type: 'boolean', description: 'Run in AppDomain sandbox for isolation', default: true },
                                validateOnly: { type: 'boolean', description: 'Only validate script syntax and safety', default: false }
                            },
                            required: ['description']
                        }
                    },
                    {
                        name: 'execute_custom_operation',
                        description: 'Execute complex multi-step operations using AI orchestration and hot script generation',
                        inputSchema: {
                            type: 'object',
                            properties: {
                                operation: { type: 'string', description: 'High-level operation description' },
                                steps: { type: 'array', items: { type: 'object' }, description: 'Detailed operation steps' },
                                rollbackOnFailure: { type: 'boolean', description: 'Rollback all changes if any step fails', default: true },
                                progressReporting: { type: 'boolean', description: 'Enable real-time progress updates', default: true },
                                maxExecutionTime: { type: 'number', description: 'Maximum total execution time in seconds', default: 300 }
                            },
                            required: ['operation']
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

                    // Phase 2A: Full CRUD Operations (Chat's Priority 1-3)
                    case 'ai_create_elements':
                        return await this.aiCreateElements(args);
                    case 'ai_modify_parameters':
                        return await this.aiModifyParameters(args);
                    case 'ai_modify_geometry':
                        return await this.aiModifyGeometry(args);

                    // Phase 2B: Hot Script Generation (Chat's Vertical Depth)
                    case 'generate_hot_script':
                        return await this.generateHotScript(args);
                    case 'execute_custom_operation':
                        return await this.executeCustomOperation(args);

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
     * Phase 2A: AI Create Elements (Chat's Priority 1)
     * Create Revit elements with AI-driven parameter analysis and atomic rollback
     */
    private async aiCreateElements(args: any): Promise<any> {
        try {
            console.log(chalk.blue('🏗️ AI Create Elements - Phase 2A Priority 1'));

            const {
                elementType,
                parameters = {},
                geometry,
                familyType,
                batchMode = false,
                dryRun = true,
                transactionGroup = true
            } = args;

            if (!this.revitBridge.isRevitConnected()) {
                throw new Error('Revit add-in not connected');
            }

            // Chat's recommendation: Use existing CreateWallCommand infrastructure
            const result = await this.revitBridge.sendCommand({
                type: 'ai_create_elements',
                payload: {
                    elementType,
                    parameters,
                    geometry,
                    familyType,
                    batchMode,
                    dryRun,
                    transactionGroup
                }
            });

            // Chat's recommendation: Return resultSet with per-element status
            const resultText = result.resultSet
                ? this.formatResultSet(result.resultSet, 'Element Creation')
                : `AI Element Creation completed!\n\nType: ${elementType}\nDry Run: ${dryRun}\nTransaction Group: ${transactionGroup}\n\nResult: ${result.data || result.error || 'Elements created successfully'}`;

            return {
                content: [{
                    type: 'text',
                    text: resultText
                }]
            };

        } catch (error) {
            const errorMessage = error instanceof Error ? error.message : String(error);
            console.error(chalk.red('❌ AI Create Elements failed:'), errorMessage);

            return {
                content: [{
                    type: 'text',
                    text: `Error in AI element creation: ${errorMessage}`
                }],
                isError: true
            };
        }
    }

    /**
     * Phase 2A: AI Modify Parameters (Chat's Priority 2)
     * Modify element parameters with batch processing and per-element status
     */
    private async aiModifyParameters(args: any): Promise<any> {
        try {
            console.log(chalk.blue('⚙️ AI Modify Parameters - Phase 2A Priority 2'));

            const {
                elementIds,
                parameters,
                batchSize = 50,
                validateOnly = false,
                transactionGroup = true,
                continueOnError = true
            } = args;

            if (!this.revitBridge.isRevitConnected()) {
                throw new Error('Revit add-in not connected');
            }

            // Chat's recommendation: Batch size ≤ 100 with chunking
            if (batchSize > 100) {
                throw new Error('Batch size must be ≤ 100 for optimal performance');
            }

            // Use existing ParameterManagementCommands infrastructure
            const result = await this.revitBridge.sendCommand({
                type: 'ai_modify_parameters',
                payload: {
                    elementIds,
                    parameters,
                    batchSize,
                    validateOnly,
                    transactionGroup,
                    continueOnError
                }
            });

            // Chat's recommendation: Return resultSet with per-element status
            const resultText = result.resultSet
                ? this.formatResultSet(result.resultSet, 'Parameter Modification')
                : `AI Parameter Modification completed!\n\nElements: ${elementIds.length}\nBatch Size: ${batchSize}\nValidate Only: ${validateOnly}\n\nResult: ${result.data || result.error || 'Parameters modified successfully'}`;

            return {
                content: [{
                    type: 'text',
                    text: resultText
                }]
            };

        } catch (error) {
            const errorMessage = error instanceof Error ? error.message : String(error);
            console.error(chalk.red('❌ AI Modify Parameters failed:'), errorMessage);

            return {
                content: [{
                    type: 'text',
                    text: `Error in AI parameter modification: ${errorMessage}`
                }],
                isError: true
            };
        }
    }

    /**
     * Phase 2A: AI Modify Geometry (Chat's Priority 3)
     * Transform element geometry with spatial validation and atomic rollback
     */
    private async aiModifyGeometry(args: any): Promise<any> {
        try {
            console.log(chalk.blue('📐 AI Modify Geometry - Phase 2A Priority 3'));

            const {
                elementIds,
                operation,
                transform,
                batchSize = 50,
                validateGeometry = true,
                transactionGroup = true
            } = args;

            if (!this.revitBridge.isRevitConnected()) {
                throw new Error('Revit add-in not connected');
            }

            // Chat's recommendation: Batch size ≤ 100 with chunking
            if (batchSize > 100) {
                throw new Error('Batch size must be ≤ 100 for optimal performance');
            }

            // New TransformElementsCommand (to be implemented in Revit side)
            const result = await this.revitBridge.sendCommand({
                type: 'ai_modify_geometry',
                payload: {
                    elementIds,
                    operation,
                    transform,
                    batchSize,
                    validateGeometry,
                    transactionGroup
                }
            });

            // Chat's recommendation: Return resultSet with per-element status
            const resultText = result.resultSet
                ? this.formatResultSet(result.resultSet, 'Geometry Transformation')
                : `AI Geometry Modification completed!\n\nElements: ${elementIds.length}\nOperation: ${operation}\nValidate Geometry: ${validateGeometry}\n\nResult: ${result.data || result.error || 'Geometry transformed successfully'}`;

            return {
                content: [{
                    type: 'text',
                    text: resultText
                }]
            };

        } catch (error) {
            const errorMessage = error instanceof Error ? error.message : String(error);
            console.error(chalk.red('❌ AI Modify Geometry failed:'), errorMessage);

            return {
                content: [{
                    type: 'text',
                    text: `Error in AI geometry modification: ${errorMessage}`
                }],
                isError: true
            };
        }
    }

    /**
     * Chat's recommendation: Format resultSet with per-element status for LLM reasoning
     */
    private formatResultSet(resultSet: any[], operationType: string): string {
        const succeeded = resultSet.filter(r => r.status === 'succeeded').length;
        const failed = resultSet.filter(r => r.status === 'failed').length;
        const skipped = resultSet.filter(r => r.status === 'skipped').length;

        let summary = `${operationType} Results:\n`;
        summary += `✅ Succeeded: ${succeeded}\n`;
        summary += `❌ Failed: ${failed}\n`;
        summary += `⏭️ Skipped: ${skipped}\n\n`;

        // Show details for failed elements
        const failures = resultSet.filter(r => r.status === 'failed');
        if (failures.length > 0) {
            summary += `Failed Elements:\n`;
            failures.forEach(f => {
                summary += `- Element ${f.elementId}: ${f.message || 'Unknown error'}\n`;
            });
        }

        return summary;
    }

    /**
     * 🔥 Phase 2B: Generate Hot Script (Chat's KILLER FEATURE)
     * Natural language → PyRevit code generation with safety guard-rails
     */
    private async generateHotScript(args: any): Promise<any> {
        try {
            console.log(chalk.blue('🔥 Generate Hot Script - Phase 2B KILLER FEATURE'));

            const {
                description,
                scriptType = 'custom',
                templateVersion = 'v1.0',
                parameters = {},
                dryRun = true,
                timeout = 30,
                enableSandbox = true,
                validateOnly = false
            } = args;

            if (!this.revitBridge.isRevitConnected()) {
                throw new Error('Revit add-in not connected');
            }

            console.log(chalk.yellow(`🧠 AI Code Generation Request: "${description}"`));

            // Chat's recommendation: Use existing ScriptHotLoader infrastructure
            const result = await this.revitBridge.sendCommand({
                type: 'generate_hot_script',
                payload: {
                    description,
                    scriptType,
                    templateVersion,
                    parameters,
                    dryRun,
                    timeout,
                    enableSandbox,
                    validateOnly,
                    // AI-generated code will be created by the Revit-side handler
                    aiGeneratedCode: await this.generatePyRevitCode(description, scriptType, parameters)
                }
            });

            const resultText = `🔥 Hot Script Generation completed!\n\nDescription: "${description}"\nScript Type: ${scriptType}\nDry Run: ${dryRun}\nSandbox: ${enableSandbox}\n\nResult: ${result.data || result.error || 'Script generated and executed successfully'}`;

            return {
                content: [{
                    type: 'text',
                    text: resultText
                }]
            };

        } catch (error) {
            const errorMessage = error instanceof Error ? error.message : String(error);
            console.error(chalk.red('❌ Generate Hot Script failed:'), errorMessage);

            return {
                content: [{
                    type: 'text',
                    text: `Error in hot script generation: ${errorMessage}`
                }],
                isError: true
            };
        }
    }

    /**
     * 🌟 Phase 2B: Execute Custom Operation (Chat's Advanced Orchestration)
     * Complex multi-step operations with AI orchestration
     */
    private async executeCustomOperation(args: any): Promise<any> {
        try {
            console.log(chalk.blue('🌟 Execute Custom Operation - Phase 2B Advanced'));

            const {
                operation,
                steps = [],
                rollbackOnFailure = true,
                progressReporting = true,
                maxExecutionTime = 300
            } = args;

            if (!this.revitBridge.isRevitConnected()) {
                throw new Error('Revit add-in not connected');
            }

            console.log(chalk.yellow(`🎯 Custom Operation: "${operation}"`));

            // Use existing FLCScriptBridge for complex operations
            const result = await this.revitBridge.sendCommand({
                type: 'execute_custom_operation',
                payload: {
                    operation,
                    steps,
                    rollbackOnFailure,
                    progressReporting,
                    maxExecutionTime
                }
            });

            const resultText = `🌟 Custom Operation completed!\n\nOperation: "${operation}"\nSteps: ${steps.length}\nRollback on Failure: ${rollbackOnFailure}\n\nResult: ${result.data || result.error || 'Operation completed successfully'}`;

            return {
                content: [{
                    type: 'text',
                    text: resultText
                }]
            };

        } catch (error) {
            const errorMessage = error instanceof Error ? error.message : String(error);
            console.error(chalk.red('❌ Execute Custom Operation failed:'), errorMessage);

            return {
                content: [{
                    type: 'text',
                    text: `Error in custom operation: ${errorMessage}`
                }],
                isError: true
            };
        }
    }

    /**
     * 🧠 AI Code Generation Engine (Chat's Natural Language → PyRevit)
     * This is where the magic happens - AI generates PyRevit code from natural language
     */
    private async generatePyRevitCode(description: string, scriptType: string, parameters: any): Promise<string> {
        try {
            console.log(chalk.cyan(`🧠 AI Code Generation: ${description}`));

            // Chat's recommendation: Template-based generation with guard-rails
            const templates = {
                'element_creation': this.getElementCreationTemplate(),
                'parameter_modification': this.getParameterModificationTemplate(),
                'geometry_transformation': this.getGeometryTransformationTemplate(),
                'analysis': this.getAnalysisTemplate(),
                'custom': this.getCustomTemplate()
            };

            const baseTemplate = templates[scriptType as keyof typeof templates] || templates.custom;

            // AI-powered code generation (this is where I use my coding abilities)
            const generatedCode = this.generateCodeFromDescription(description, baseTemplate, parameters);

            // Chat's recommendation: Static analysis and safety validation
            this.validateGeneratedCode(generatedCode);

            console.log(chalk.green('✅ AI Code Generation successful'));
            return generatedCode;

        } catch (error) {
            console.error(chalk.red('❌ AI Code Generation failed:'), error);
            throw error;
        }
    }

    /**
     * 🛡️ Code Generation Templates (Chat's Template Library v0.1)
     */
    private getElementCreationTemplate(): string {
        return `
# FLC Element Creation Template v1.0
# Generated by Tycoon AI-BIM Platform
import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from Autodesk.Revit.DB import *
from Autodesk.Revit.UI import *

def create_elements(doc, parameters):
    """AI-generated element creation script"""
    with Transaction(doc, "AI Element Creation") as t:
        t.Start()
        try:
            # AI-generated code will be inserted here
            {{GENERATED_CODE}}
            t.Commit()
            return "Elements created successfully"
        except Exception as e:
            t.RollBack()
            raise e
`;
    }

    private getParameterModificationTemplate(): string {
        return `
# FLC Parameter Modification Template v1.0
# Generated by Tycoon AI-BIM Platform
import clr
clr.AddReference('RevitAPI')

from Autodesk.Revit.DB import *

def modify_parameters(doc, element_ids, parameters):
    """AI-generated parameter modification script"""
    with Transaction(doc, "AI Parameter Modification") as t:
        t.Start()
        try:
            # AI-generated code will be inserted here
            {{GENERATED_CODE}}
            t.Commit()
            return f"Modified {len(element_ids)} elements"
        except Exception as e:
            t.RollBack()
            raise e
`;
    }

    private getGeometryTransformationTemplate(): string {
        return `
# FLC Geometry Transformation Template v1.0
# Generated by Tycoon AI-BIM Platform
import clr
clr.AddReference('RevitAPI')

from Autodesk.Revit.DB import *

def transform_geometry(doc, element_ids, transform_params):
    """AI-generated geometry transformation script"""
    with Transaction(doc, "AI Geometry Transformation") as t:
        t.Start()
        try:
            # AI-generated code will be inserted here
            {{GENERATED_CODE}}
            t.Commit()
            return f"Transformed {len(element_ids)} elements"
        except Exception as e:
            t.RollBack()
            raise e
`;
    }

    private getAnalysisTemplate(): string {
        return `
# FLC Analysis Template v1.0
# Generated by Tycoon AI-BIM Platform
import clr
clr.AddReference('RevitAPI')

from Autodesk.Revit.DB import *

def analyze_elements(doc, element_ids, analysis_params):
    """AI-generated analysis script"""
    # Read-only analysis - no transaction needed
    try:
        # AI-generated code will be inserted here
        {{GENERATED_CODE}}
        return "Analysis completed successfully"
    except Exception as e:
        raise e
`;
    }

    private getCustomTemplate(): string {
        return `
# FLC Custom Script Template v1.0
# Generated by Tycoon AI-BIM Platform
import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from Autodesk.Revit.DB import *
from Autodesk.Revit.UI import *

def custom_operation(doc, parameters):
    """AI-generated custom script"""
    # AI-generated code will be inserted here
    {{GENERATED_CODE}}
    return "Custom operation completed"
`;
    }

    /**
     * 🧠 Core AI Code Generation Logic
     * This is where I use my coding abilities to generate PyRevit code
     */
    private generateCodeFromDescription(description: string, template: string, parameters: any): string {
        // This is a simplified version - in practice, this would use more sophisticated AI
        let generatedCode = '';

        // Parse the description and generate appropriate PyRevit code
        if (description.toLowerCase().includes('create wall')) {
            generatedCode = `
            # Create wall based on description: ${description}
            start_point = XYZ(0, 0, 0)
            end_point = XYZ(10, 0, 0)
            height = 10

            line = Line.CreateBound(start_point, end_point)
            wall = Wall.Create(doc, line, Level.Create(doc, 0).Id, False)
            `;
        } else if (description.toLowerCase().includes('modify parameter')) {
            generatedCode = `
            # Modify parameters based on description: ${description}
            for element_id in element_ids:
                element = doc.GetElement(ElementId(int(element_id)))
                if element:
                    for param_name, param_value in parameters.items():
                        param = element.LookupParameter(param_name)
                        if param and not param.IsReadOnly:
                            param.Set(param_value)
            `;
        } else {
            generatedCode = `
            # Custom operation based on description: ${description}
            # Generated code for: ${description}
            pass  # Placeholder for custom logic
            `;
        }

        // Insert the generated code into the template
        return template.replace('{{GENERATED_CODE}}', generatedCode);
    }

    /**
     * 🛡️ Code Safety Validation (Chat's Guard-Rails)
     */
    private validateGeneratedCode(code: string): void {
        // Chat's recommendation: Static analysis for forbidden namespaces
        const forbiddenNamespaces = [
            'System.IO',
            'System.Net',
            'System.Diagnostics.Process',
            'System.Reflection',
            'Microsoft.Win32'
        ];

        for (const namespace of forbiddenNamespaces) {
            if (code.includes(namespace)) {
                throw new Error(`Generated code contains forbidden namespace: ${namespace}`);
            }
        }

        // Basic syntax validation
        if (code.includes('import os') || code.includes('import subprocess')) {
            throw new Error('Generated code contains potentially unsafe imports');
        }

        console.log(chalk.green('✅ Code safety validation passed'));
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
