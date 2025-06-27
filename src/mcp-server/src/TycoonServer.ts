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
import { readFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

// Import Temporal Neural Nexus components
import { MemoryManager } from './core/MemoryManager.js';
import { TimeManager } from './core/TimeManager.js';
import { BimVectorDatabase } from './core/BimVectorDatabase.js';

// Import Tycoon-specific components
import { RevitBridge, RevitSelection } from './revit/RevitBridge.js';
import { FLCAdapter, FLCFramingOptions, FLCRenumberOptions } from './flc/FLCAdapter.js';

export class TycoonServer {
    private server: Server;
    private memoryManager: MemoryManager;
    private timeManager: TimeManager;
    private revitBridge: RevitBridge;
    private flcAdapter: FLCAdapter;
    private bimVectorDb: BimVectorDatabase;
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
        this.bimVectorDb = new BimVectorDatabase({
            chromaUrl: process.env.CHROMA_URL || 'http://localhost:8000',
            openaiApiKey: process.env.OPENAI_API_KEY,
            collectionName: 'tycoon_bim_elements'
        });

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
                    {
                        name: 'get_mcp_version',
                        description: 'Get the current Tycoon MCP server version and build information',
                        inputSchema: { type: 'object', properties: {} }
                    },
                    {
                        name: 'get_system_versions',
                        description: 'Get comprehensive version information for all system components: FAFB GPU MCP, Revit Add-in, and Tycoon MCP',
                        inputSchema: {
                            type: 'object',
                            properties: {
                                includeCapabilities: { type: 'boolean', description: 'Include detailed capability information', default: true }
                            }
                        }
                    },
                    {
                        name: 'search_bim_elements',
                        description: 'Search for BIM elements using semantic similarity in the vector database',
                        inputSchema: {
                            type: 'object',
                            properties: {
                                query: { type: 'string', description: 'Search query (natural language)' },
                                category: { type: 'string', description: 'Filter by element category' },
                                familyName: { type: 'string', description: 'Filter by family name' },
                                level: { type: 'string', description: 'Filter by level' },
                                maxResults: { type: 'number', description: 'Maximum number of results', default: 10 },
                                minSimilarity: { type: 'number', description: 'Minimum similarity threshold (0-1)', default: 0.5 },
                                includeGeometry: { type: 'boolean', description: 'Include geometry data', default: false },
                                includeParameters: { type: 'boolean', description: 'Include parameter data', default: true }
                            },
                            required: ['query']
                        }
                    },
                    {
                        name: 'get_bim_database_stats',
                        description: 'Get statistics about the BIM vector database',
                        inputSchema: { type: 'object', properties: {} }
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
                    case 'get_mcp_version':
                        return await this.getMcpVersion(args);
                    case 'get_system_versions':
                        return await this.getSystemVersions(args);
                    case 'search_bim_elements':
                        return await this.searchBimElements(args);
                    case 'get_bim_database_stats':
                        return await this.getBimDatabaseStats(args);

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
     * Get MCP server version and build information
     */
    private async getMcpVersion(args: any): Promise<any> {
        try {
            // Get the directory of the current module
            const __filename = fileURLToPath(import.meta.url);
            const __dirname = dirname(__filename);

            // Read package.json to get version info
            const packageJsonPath = join(__dirname, '..', 'package.json');
            const packageJson = JSON.parse(readFileSync(packageJsonPath, 'utf8'));

            const buildTime = new Date().toISOString();
            const nodeVersion = process.version;
            const platform = process.platform;
            const arch = process.arch;

            return {
                status: 'success',
                version: {
                    mcpServer: packageJson.version,
                    name: packageJson.name,
                    description: packageJson.description,
                    buildTime: buildTime,
                    runtime: {
                        node: nodeVersion,
                        platform: platform,
                        architecture: arch
                    },
                    capabilities: {
                        binaryStreaming: true,
                        memorySystem: true,
                        revitIntegration: true,
                        fafbOptimization: true
                    }
                }
            };
        } catch (error) {
            console.error(chalk.red('Failed to get MCP version:'), error);
            return {
                status: 'error',
                error: `Failed to retrieve version information: ${error instanceof Error ? error.message : String(error)}`
            };
        }
    }

    /**
     * Get comprehensive system version information for all components
     */
    private async getSystemVersions(args: any): Promise<any> {
        try {
            const includeCapabilities = args.includeCapabilities !== false;

            // Get Tycoon MCP version (this server)
            const tycoonMcpVersion = await this.getMcpVersion({});

            // Get Revit Add-in version (if connected)
            let revitAddinVersion: any = null;
            try {
                const revitStatus: any = await this.getRevitStatus({});
                if (revitStatus && revitStatus.revit && revitStatus.revit.connected) {
                    // Try to get version from Revit bridge
                    revitAddinVersion = {
                        status: 'connected',
                        version: 'Available via Revit connection',
                        note: 'Use get_revit_status for detailed Revit information'
                    };
                } else {
                    revitAddinVersion = {
                        status: 'disconnected',
                        version: 'Unknown - Revit not connected',
                        note: 'Connect to Revit to get add-in version information'
                    };
                }
            } catch (error) {
                revitAddinVersion = {
                    status: 'error',
                    version: 'Unknown - Error checking Revit connection',
                    error: error instanceof Error ? error.message : String(error)
                };
            }

            // Check for FAFB GPU MCP (this would need to be implemented based on how FAFB is accessible)
            let fafbGpuVersion = {
                status: 'unknown',
                version: 'Unknown - FAFB GPU MCP detection not implemented',
                note: 'FAFB GPU MCP version checking requires additional integration'
            };

            const systemInfo: any = {
                status: 'success',
                timestamp: new Date().toISOString(),
                components: {
                    tycoonMcp: {
                        name: 'Tycoon AI-BIM MCP Server',
                        ...tycoonMcpVersion.version,
                        status: 'running'
                    },
                    revitAddin: {
                        name: 'Tycoon Revit Add-in',
                        ...revitAddinVersion
                    },
                    fafbGpu: {
                        name: 'FAFB GPU MCP Server',
                        ...fafbGpuVersion
                    }
                }
            };

            if (includeCapabilities) {
                systemInfo.capabilities = {
                    binaryStreaming: true,
                    memorySystem: true,
                    revitIntegration: revitAddinVersion.status === 'connected',
                    fafbOptimization: true,
                    gpuAcceleration: fafbGpuVersion.status === 'running',
                    massiveSelectionProcessing: true,
                    chunkOptimization: true
                };
            }

            return systemInfo;

        } catch (error) {
            console.error(chalk.red('Failed to get system versions:'), error);
            return {
                status: 'error',
                error: `Failed to retrieve system version information: ${error instanceof Error ? error.message : String(error)}`
            };
        }
    }

    /**
     * Search BIM elements using semantic similarity
     */
    private async searchBimElements(args: any): Promise<any> {
        try {
            if (!this.bimVectorDb) {
                return {
                    status: 'error',
                    error: 'BIM Vector Database not initialized'
                };
            }

            const { query, category, familyName, level, maxResults, minSimilarity, includeGeometry, includeParameters } = args;

            if (!query) {
                return {
                    status: 'error',
                    error: 'Query parameter is required'
                };
            }

            const results = await this.bimVectorDb.searchSimilar(query, {
                category,
                familyName,
                level,
                maxResults: maxResults || 10,
                minSimilarity: minSimilarity || 0.5,
                includeGeometry: includeGeometry || false,
                includeParameters: includeParameters !== false
            });

            return {
                status: 'success',
                query,
                resultsCount: results.length,
                results: results.map(result => ({
                    element: result.element,
                    similarity: result.similarity,
                    distance: result.distance
                })),
                timestamp: new Date().toISOString()
            };

        } catch (error) {
            console.error(chalk.red('Failed to search BIM elements:'), error);
            return {
                status: 'error',
                error: `Search failed: ${error instanceof Error ? error.message : String(error)}`
            };
        }
    }

    /**
     * Get BIM database statistics
     */
    private async getBimDatabaseStats(args: any): Promise<any> {
        try {
            if (!this.bimVectorDb) {
                return {
                    status: 'error',
                    error: 'BIM Vector Database not initialized'
                };
            }

            const stats = await this.bimVectorDb.getStats();

            return {
                status: 'success',
                statistics: stats,
                timestamp: new Date().toISOString()
            };

        } catch (error) {
            console.error(chalk.red('Failed to get BIM database stats:'), error);
            return {
                status: 'error',
                error: `Failed to get statistics: ${error instanceof Error ? error.message : String(error)}`
            };
        }
    }

    /**
     * Start the server
     */
    async start(): Promise<void> {
        try {
            // Initialize BIM Vector Database
            console.log(chalk.blue('🔄 Initializing BIM Vector Database...'));
            await this.bimVectorDb.initialize();

            // Connect BIM Vector Database to RevitBridge for direct streaming
            this.revitBridge.setBimVectorDatabase(this.bimVectorDb);
            console.log(chalk.green('✅ BIM Vector Database connected to Revit streaming pipeline'));

            const transport = new StdioServerTransport();
            await this.server.connect(transport);
            console.log(chalk.green('🚀 Tycoon AI-BIM Server started with Vector Database Pipeline'));
        } catch (error) {
            console.error(chalk.red('❌ Failed to start server:'), error);
            console.log(chalk.yellow('⚠️ Starting server without Vector Database...'));

            const transport = new StdioServerTransport();
            await this.server.connect(transport);
            console.log(chalk.green('🚀 Tycoon AI-BIM Server started (Vector DB disabled)'));
        }
    }
}
