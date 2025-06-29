import { LiveStreamMonitor, SessionInfo, StreamingProgress } from '../storage/LiveStreamMonitor.js';
import { BimDatabase } from '../storage/BimDatabase.js';
import { RevitBridge } from '../revit/RevitBridge.js';
import * as path from 'path';

/**
 * 🚀 TYCOON STREAMING TOOLS - MCP tools for accessing streaming data
 */
export class StreamingTools {
    private monitor: LiveStreamMonitor;
    private database: BimDatabase;
    private vaultPath: string;
    private revitBridge: RevitBridge;

    constructor(vaultPath?: string) {
        this.vaultPath = vaultPath || path.join(
            process.env.LOCALAPPDATA || process.env.HOME || '.',
            'Tycoon',
            'DataVault'
        );
        
        this.monitor = new LiveStreamMonitor(this.vaultPath);
        this.database = new BimDatabase(this.vaultPath);
        this.revitBridge = new RevitBridge();
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

    // ==========================================
    // 🤖 AI PARAMETER MANAGEMENT TOOLS
    // ==========================================

    /**
     * Get detailed parameter information for selected elements
     */
    public async getElementParameters(elementIds?: string[]): Promise<any> {
        try {
            console.log('🔍 Getting element parameters...');

            // If no element IDs provided, get current selection
            if (!elementIds || elementIds.length === 0) {
                const selectionResult = await this.revitBridge.sendCommand({
                    type: 'selection',
                    payload: {
                        includeParameters: true,
                        includeGeometry: false
                    }
                });

                if (!selectionResult.success || !selectionResult.data) {
                    return {
                        success: false,
                        message: 'No elements selected in Revit',
                        data: { elements: [] }
                    };
                }

                return {
                    success: true,
                    message: `Retrieved parameters for selected elements`,
                    data: {
                        elements: selectionResult.data,
                        selectionMethod: 'current_selection'
                    }
                };
            } else {
                // Get specific elements by ID
                const result = await this.revitBridge.sendCommand({
                    type: 'query',
                    payload: {
                        elementIds: elementIds,
                        includeParameters: true,
                        includeGeometry: false
                    }
                });

                return {
                    success: result.success,
                    message: result.success ?
                        `Retrieved parameters for ${elementIds.length} elements` :
                        `Failed to get elements`,
                    data: result.data || { elements: [] }
                };
            }
        } catch (error) {
            console.error('❌ Error getting element parameters:', error);
            return {
                success: false,
                message: `Error getting element parameters: ${error instanceof Error ? error.message : 'Unknown error'}`,
                data: { elements: [] }
            };
        }
    }

    /**
     * Analyze parameters and suggest improvements using AI logic
     */
    public async analyzeParameters(elements: any[], analysisType: string = 'general'): Promise<any> {
        try {
            console.log(`🧠 Analyzing parameters for ${elements.length} elements (${analysisType})...`);

            const analysis = {
                summary: {
                    totalElements: elements.length,
                    analysisType: analysisType,
                    timestamp: new Date().toISOString()
                },
                issues: [] as any[],
                recommendations: [] as any[],
                statistics: {} as any
            };

            // Analyze each element
            for (const element of elements) {
                const elementAnalysis = this.analyzeElementParameters(element, analysisType);
                analysis.issues.push(...elementAnalysis.issues);
                analysis.recommendations.push(...elementAnalysis.recommendations);
            }

            // Generate statistics
            analysis.statistics = this.generateParameterStatistics(elements);

            // Add FLC-specific analysis if applicable
            if (analysisType === 'flc' || analysisType === 'steel_framing') {
                const flcAnalysis = this.analyzeFLCParameters(elements);
                analysis.issues.push(...flcAnalysis.issues);
                analysis.recommendations.push(...flcAnalysis.recommendations);
            }

            return {
                success: true,
                message: `Analysis complete: ${analysis.issues.length} issues found, ${analysis.recommendations.length} recommendations`,
                data: analysis
            };
        } catch (error) {
            console.error('❌ Error analyzing parameters:', error);
            return {
                success: false,
                message: `Error analyzing parameters: ${error instanceof Error ? error.message : 'Unknown error'}`,
                data: null
            };
        }
    }

    /**
     * Modify element parameters based on AI recommendations
     */
    public async modifyParameters(modifications: any[], dryRun: boolean = true): Promise<any> {
        try {
            console.log(`🔧 ${dryRun ? 'Previewing' : 'Applying'} ${modifications.length} parameter modifications...`);

            if (dryRun) {
                // Preview mode - validate and show what would change
                const preview = {
                    modifications: modifications,
                    validation: [] as any[],
                    summary: {
                        totalModifications: modifications.length,
                        elementsAffected: new Set(modifications.map((m: any) => m.elementId)).size,
                        dryRun: true
                    }
                };

                // Validate each modification
                for (const mod of modifications) {
                    const validation = this.validateParameterModification(mod);
                    preview.validation.push(validation);
                }

                return {
                    success: true,
                    message: `Preview complete: ${modifications.length} modifications validated`,
                    data: preview
                };
            } else {
                // Actually apply modifications
                const result = await this.revitBridge.sendCommand({
                    type: 'modify',
                    payload: {
                        modifications: modifications,
                        createTransaction: true,
                        transactionName: 'AI Parameter Modifications'
                    }
                });

                return {
                    success: result.success,
                    message: result.success ?
                        `Successfully applied ${modifications.length} parameter modifications` :
                        `Failed to apply modifications`,
                    data: result.data
                };
            }
        } catch (error) {
            console.error('❌ Error modifying parameters:', error);
            return {
                success: false,
                message: `Error modifying parameters: ${error instanceof Error ? error.message : 'Unknown error'}`,
                data: null
            };
        }
    }

    /**
     * Execute complete AI parameter workflow: Get → Analyze → Recommend → Apply
     */
    public async executeAIParameterWorkflow(options: any = {}): Promise<any> {
        try {
            console.log('🚀 Starting AI Parameter Workflow...');

            const workflow = {
                steps: [] as string[],
                results: {} as any,
                success: true,
                startTime: new Date().toISOString(),
                endTime: ''
            };

            // Step 1: Get element parameters
            workflow.steps.push('Getting element parameters');
            const elementsResult = await this.getElementParameters(options.elementIds);
            workflow.results.elements = elementsResult;

            if (!elementsResult.success) {
                workflow.success = false;
                return {
                    success: false,
                    message: 'Failed to get element parameters',
                    data: workflow
                };
            }

            // Step 2: Analyze parameters
            workflow.steps.push('Analyzing parameters');
            const analysisResult = await this.analyzeParameters(
                elementsResult.data.elements,
                options.analysisType || 'general'
            );
            workflow.results.analysis = analysisResult;

            if (!analysisResult.success) {
                workflow.success = false;
                return {
                    success: false,
                    message: 'Failed to analyze parameters',
                    data: workflow
                };
            }

            // Step 3: Generate modifications (if recommendations exist)
            if (analysisResult.data.recommendations.length > 0) {
                workflow.steps.push('Generating modifications');
                const modifications = this.generateModificationsFromRecommendations(
                    analysisResult.data.recommendations
                );
                workflow.results.modifications = modifications;

                // Step 4: Preview modifications
                if (modifications.length > 0) {
                    workflow.steps.push('Previewing modifications');
                    const previewResult = await this.modifyParameters(modifications, true);
                    workflow.results.preview = previewResult;

                    // Step 5: Apply modifications (if not dry run)
                    if (!options.dryRun && options.autoApply) {
                        workflow.steps.push('Applying modifications');
                        const applyResult = await this.modifyParameters(modifications, false);
                        workflow.results.applied = applyResult;
                        workflow.success = applyResult.success;
                    }
                }
            }

            workflow.endTime = new Date().toISOString();

            return {
                success: workflow.success,
                message: `AI Parameter Workflow completed: ${workflow.steps.length} steps executed`,
                data: workflow
            };
        } catch (error) {
            console.error('❌ Error in AI parameter workflow:', error);
            return {
                success: false,
                message: `AI Parameter Workflow failed: ${error instanceof Error ? error.message : 'Unknown error'}`,
                data: null
            };
        }
    }

    // ==========================================
    // 🔧 HELPER METHODS FOR AI PARAMETER ANALYSIS
    // ==========================================

    /**
     * Analyze parameters for a single element
     */
    private analyzeElementParameters(element: any, analysisType: string): any {
        const issues: any[] = [];
        const recommendations: any[] = [];

        if (!element.parameters) {
            issues.push({
                elementId: element.id,
                type: 'missing_parameters',
                severity: 'warning',
                message: 'Element has no parameter data'
            });
            return { issues, recommendations };
        }

        // Check for common parameter issues
        for (const [paramName, paramValue] of Object.entries(element.parameters)) {
            // Check for empty required parameters
            if (this.isRequiredParameter(paramName, element.category) && (!paramValue || paramValue === '')) {
                issues.push({
                    elementId: element.id,
                    type: 'empty_required_parameter',
                    severity: 'error',
                    parameter: paramName,
                    message: `Required parameter '${paramName}' is empty`
                });

                recommendations.push({
                    elementId: element.id,
                    type: 'fill_parameter',
                    parameter: paramName,
                    suggestedValue: this.getSuggestedValue(paramName, element),
                    reason: 'Required parameter should not be empty'
                });
            }

            // Check for invalid values
            const validation = this.validateParameterValue(paramName, paramValue, element);
            if (!validation.isValid) {
                issues.push({
                    elementId: element.id,
                    type: 'invalid_parameter_value',
                    severity: validation.severity,
                    parameter: paramName,
                    currentValue: paramValue,
                    message: validation.message
                });

                if (validation.suggestedValue) {
                    recommendations.push({
                        elementId: element.id,
                        type: 'correct_parameter',
                        parameter: paramName,
                        currentValue: paramValue,
                        suggestedValue: validation.suggestedValue,
                        reason: validation.reason
                    });
                }
            }
        }

        return { issues, recommendations };
    }

    /**
     * Generate parameter statistics
     */
    private generateParameterStatistics(elements: any[]): any {
        const stats = {
            totalElements: elements.length,
            parameterCounts: {} as any,
            emptyParameters: {} as any,
            uniqueValues: {} as any,
            categories: {} as any
        };

        for (const element of elements) {
            // Count by category
            const category = element.category || 'Unknown';
            stats.categories[category] = (stats.categories[category] || 0) + 1;

            if (element.parameters) {
                for (const [paramName, paramValue] of Object.entries(element.parameters)) {
                    // Count parameter occurrences
                    stats.parameterCounts[paramName] = (stats.parameterCounts[paramName] || 0) + 1;

                    // Count empty parameters
                    if (!paramValue || paramValue === '') {
                        stats.emptyParameters[paramName] = (stats.emptyParameters[paramName] || 0) + 1;
                    }

                    // Track unique values
                    if (!stats.uniqueValues[paramName]) {
                        stats.uniqueValues[paramName] = new Set();
                    }
                    stats.uniqueValues[paramName].add(paramValue);
                }
            }
        }

        // Convert Sets to arrays for JSON serialization
        for (const paramName in stats.uniqueValues) {
            stats.uniqueValues[paramName] = Array.from(stats.uniqueValues[paramName]);
        }

        return stats;
    }

    /**
     * Analyze FLC-specific parameters
     */
    private analyzeFLCParameters(elements: any[]): any {
        const issues: any[] = [];
        const recommendations: any[] = [];

        for (const element of elements) {
            if (element.category === 'Walls' || element.category === 'Structural Framing') {
                // Check FLC-specific parameters
                const flcParams = ['BIMSF_Id', 'BIMSF_Label', 'BIMSF_Container', 'BIMSF_Subassembly'];

                for (const paramName of flcParams) {
                    const paramValue = element.parameters?.[paramName];

                    if (!paramValue || paramValue === '') {
                        issues.push({
                            elementId: element.id,
                            type: 'missing_flc_parameter',
                            severity: 'error',
                            parameter: paramName,
                            message: `FLC parameter '${paramName}' is missing or empty`
                        });

                        recommendations.push({
                            elementId: element.id,
                            type: 'add_flc_parameter',
                            parameter: paramName,
                            suggestedValue: this.generateFLCParameterValue(paramName, element),
                            reason: 'Required for FLC steel framing workflow'
                        });
                    }
                }

                // Check FLC naming conventions
                if (element.parameters?.['Type Name']) {
                    const typeName = element.parameters['Type Name'];
                    if (!this.validateFLCNaming(typeName)) {
                        issues.push({
                            elementId: element.id,
                            type: 'invalid_flc_naming',
                            severity: 'warning',
                            parameter: 'Type Name',
                            currentValue: typeName,
                            message: 'Type name does not follow FLC naming convention'
                        });

                        recommendations.push({
                            elementId: element.id,
                            type: 'fix_flc_naming',
                            parameter: 'Type Name',
                            currentValue: typeName,
                            suggestedValue: this.suggestFLCNaming(typeName, element),
                            reason: 'Follow FLC naming convention: FLC_[thickness]_[Int/Ext]_[options]'
                        });
                    }
                }
            }
        }

        return { issues, recommendations };
    }

    /**
     * Validate parameter modification
     */
    private validateParameterModification(modification: any): any {
        const validation = {
            elementId: modification.elementId,
            parameter: modification.parameter,
            isValid: true,
            warnings: [] as string[],
            errors: [] as string[]
        };

        // Check if parameter exists
        if (!modification.parameter) {
            validation.isValid = false;
            validation.errors.push('Parameter name is required');
        }

        // Check if new value is valid
        if (modification.newValue !== undefined) {
            const valueValidation = this.validateParameterValue(
                modification.parameter,
                modification.newValue,
                { id: modification.elementId }
            );

            if (!valueValidation.isValid) {
                validation.isValid = false;
                validation.errors.push(valueValidation.message || 'Invalid parameter value');
            }
        }

        return validation;
    }

    /**
     * Generate modifications from recommendations
     */
    private generateModificationsFromRecommendations(recommendations: any[]): any[] {
        const modifications: any[] = [];

        for (const rec of recommendations) {
            if (rec.type === 'fill_parameter' || rec.type === 'correct_parameter' || rec.type === 'add_flc_parameter') {
                modifications.push({
                    elementId: rec.elementId,
                    parameter: rec.parameter,
                    currentValue: rec.currentValue,
                    newValue: rec.suggestedValue,
                    reason: rec.reason,
                    type: rec.type
                });
            }
        }

        return modifications;
    }

    // ==========================================
    // 🔍 PARAMETER VALIDATION HELPERS
    // ==========================================

    private isRequiredParameter(paramName: string, category: string): boolean {
        const requiredParams = {
            'Walls': ['Type Name', 'Level'],
            'Structural Framing': ['Type Name', 'Level', 'BIMSF_Id'],
            'Generic': ['Type Name']
        };

        return (requiredParams as any)[category]?.includes(paramName) ||
               (requiredParams as any)['Generic']?.includes(paramName) ||
               false;
    }

    private validateParameterValue(paramName: string, value: any, element: any): any {
        // Basic validation - can be extended
        if (paramName === 'Level' && value && !value.toString().includes('Level')) {
            return {
                isValid: false,
                severity: 'warning',
                message: 'Level parameter should reference a valid level',
                suggestedValue: 'Level 1',
                reason: 'Ensure element is associated with correct level'
            };
        }

        return { isValid: true };
    }

    private getSuggestedValue(paramName: string, element: any): any {
        // Generate suggested values based on parameter name and element context
        const suggestions = {
            'BIMSF_Id': `${element.category?.substring(0, 2) || 'EL'}-${Date.now().toString().slice(-6)}`,
            'BIMSF_Label': `${element.category || 'Element'}-001`,
            'BIMSF_Container': '01-1001',
            'BIMSF_Subassembly': 'Main',
            'Level': 'Level 1'
        };

        return (suggestions as any)[paramName] || 'TBD';
    }

    private generateFLCParameterValue(paramName: string, element: any): string {
        switch (paramName) {
            case 'BIMSF_Id':
                return `FLC-${element.category?.substring(0, 3) || 'ELM'}-${Date.now().toString().slice(-6)}`;
            case 'BIMSF_Label':
                return `${element.category || 'Element'}-001`;
            case 'BIMSF_Container':
                return '01-1001';
            case 'BIMSF_Subassembly':
                return 'Main Panel';
            default:
                return 'FLC-TBD';
        }
    }

    private validateFLCNaming(typeName: string): boolean {
        // FLC naming convention: FLC_[thickness]_[Int/Ext]_[options]
        const flcPattern = /^FLC_\d+_(Int|Ext)_[A-Z\-]+$/;
        return flcPattern.test(typeName);
    }

    private suggestFLCNaming(currentName: string, element: any): string {
        // Try to convert existing name to FLC convention
        if (currentName.includes('Interior') || currentName.includes('Int')) {
            return 'FLC_6_Int_DW-FB';
        } else if (currentName.includes('Exterior') || currentName.includes('Ext')) {
            return 'FLC_6_Ext_DW-FB';
        }
        return 'FLC_6_Int_DW-FB'; // Default suggestion
    }

    // ==========================================
    // 🎯 SPECIALIZED AI PANEL TOOLS
    // ==========================================

    /**
     * Smart renaming of panel elements following FLC conventions
     */
    public async renamePanelElements(options: any = {}): Promise<any> {
        try {
            console.log('🎯 Starting AI panel element renaming...');

            // Get elements to rename
            const elementsResult = await this.getElementParameters(options.elementIds);
            if (!elementsResult.success) {
                return elementsResult;
            }

            const elements = elementsResult.data.elements;
            const namingConvention = options.namingConvention || 'flc_standard';
            const direction = options.direction || 'left_to_right';
            const dryRun = options.dryRun !== false; // Default to true

            // Analyze panel structure
            const structureAnalysis = this.analyzePanelElementStructure(elements);

            // Generate new names based on spatial analysis
            const renamingPlan = this.generateRenamingPlan(
                structureAnalysis,
                namingConvention,
                direction
            );

            if (dryRun) {
                return {
                    success: true,
                    message: `Preview: ${renamingPlan.modifications.length} elements would be renamed`,
                    data: {
                        dryRun: true,
                        renamingPlan: renamingPlan,
                        structureAnalysis: structureAnalysis
                    }
                };
            } else {
                // Apply the renaming
                const modifications = renamingPlan.modifications.map((mod: any) => ({
                    elementId: mod.elementId,
                    parameter: 'BIMSF_Label',
                    newValue: mod.newName,
                    reason: mod.reason
                }));

                // Also update the Label parameter
                const labelModifications = renamingPlan.modifications.map((mod: any) => ({
                    elementId: mod.elementId,
                    parameter: 'Label',
                    newValue: mod.newName,
                    reason: mod.reason
                }));

                const allModifications = [...modifications, ...labelModifications];
                const result = await this.modifyParameters(allModifications, false);

                return {
                    success: result.success,
                    message: result.success ?
                        `Successfully renamed ${renamingPlan.modifications.length} panel elements` :
                        `Failed to rename elements: ${result.message}`,
                    data: {
                        applied: result.success,
                        renamingPlan: renamingPlan,
                        modificationResult: result.data
                    }
                };
            }
        } catch (error) {
            console.error('❌ Error renaming panel elements:', error);
            return {
                success: false,
                message: `Error renaming panel elements: ${error instanceof Error ? error.message : 'Unknown error'}`,
                data: null
            };
        }
    }

    /**
     * Analyze panel structure and detect components
     */
    public async analyzePanelStructure(options: any = {}): Promise<any> {
        try {
            console.log('🧠 Analyzing panel structure...');

            // Get elements to analyze
            const elementsResult = await this.getElementParameters(options.elementIds);
            if (!elementsResult.success) {
                return elementsResult;
            }

            const elements = elementsResult.data.elements;
            const analysisDepth = options.analysisDepth || 'detailed';
            const includeRecommendations = options.includeRecommendations !== false;

            // Perform structure analysis
            const analysis = this.analyzePanelElementStructure(elements);

            // Add detailed analysis based on depth
            if (analysisDepth === 'detailed' || analysisDepth === 'comprehensive') {
                analysis.spatialAnalysis = this.performSpatialAnalysis(elements);
                analysis.componentAnalysis = this.analyzeComponentTypes(elements);
            }

            if (analysisDepth === 'comprehensive') {
                analysis.qualityAnalysis = this.analyzeQualityIssues(elements);
                analysis.performanceMetrics = this.calculatePerformanceMetrics(elements);
            }

            // Generate recommendations if requested
            if (includeRecommendations) {
                analysis.recommendations = this.generatePanelRecommendations(analysis);
            }

            return {
                success: true,
                message: `Panel structure analysis complete for ${elements.length} elements`,
                data: analysis
            };
        } catch (error) {
            console.error('❌ Error analyzing panel structure:', error);
            return {
                success: false,
                message: `Error analyzing panel structure: ${error instanceof Error ? error.message : 'Unknown error'}`,
                data: null
            };
        }
    }

    // ==========================================
    // 🔧 PANEL ANALYSIS HELPER METHODS
    // ==========================================

    /**
     * Analyze the structure of panel elements
     */
    private analyzePanelElementStructure(elements: any[]): any {
        const structure = {
            totalElements: elements.length,
            elementTypes: {} as any,
            panels: {} as any,
            spatialBounds: null,
            issues: [] as any[],
            timestamp: new Date().toISOString()
        };

        // Group elements by type and container
        for (const element of elements) {
            const category = element.category || 'Unknown';
            const container = element.parameters?.['BIMSF_Container'] || 'Unassigned';

            // Count by type
            structure.elementTypes[category] = (structure.elementTypes[category] || 0) + 1;

            // Group by panel container
            if (!structure.panels[container]) {
                structure.panels[container] = {
                    elements: [],
                    types: {},
                    bounds: null
                };
            }

            structure.panels[container].elements.push(element);
            structure.panels[container].types[category] =
                (structure.panels[container].types[category] || 0) + 1;
        }

        return structure;
    }

    /**
     * Generate renaming plan based on spatial analysis
     */
    private generateRenamingPlan(structure: any, convention: string, direction: string): any {
        const plan = {
            convention: convention,
            direction: direction,
            modifications: [] as any[],
            summary: {} as any
        };

        // Process each panel separately
        for (const [panelName, panelData] of Object.entries(structure.panels) as any) {
            const panelElements = panelData.elements;

            // Sort elements spatially
            const sortedElements = this.sortElementsSpatially(panelElements, direction);

            // Generate names based on convention
            const elementNames = this.generateElementNames(sortedElements, convention);

            // Create modification entries
            for (let i = 0; i < sortedElements.length; i++) {
                const element = sortedElements[i];
                const newName = elementNames[i];
                const currentName = element.parameters?.['BIMSF_Label'] || element.parameters?.['Label'] || 'Unnamed';

                if (newName !== currentName) {
                    plan.modifications.push({
                        elementId: element.id.toString(),
                        currentName: currentName,
                        newName: newName,
                        elementType: element.category,
                        panel: panelName,
                        reason: `Rename ${element.category} from '${currentName}' to '${newName}' following ${convention} convention`
                    });
                }
            }
        }

        plan.summary = {
            totalModifications: plan.modifications.length,
            panelsAffected: Object.keys(structure.panels).length,
            elementTypes: [...new Set(plan.modifications.map((m: any) => m.elementType))]
        };

        return plan;
    }

    /**
     * Sort elements spatially based on direction
     */
    private sortElementsSpatially(elements: any[], direction: string): any[] {
        return elements.sort((a, b) => {
            const aLoc = a.geometry?.location || { x: 0, y: 0, z: 0 };
            const bLoc = b.geometry?.location || { x: 0, y: 0, z: 0 };

            switch (direction) {
                case 'left_to_right':
                    return aLoc.x - bLoc.x;
                case 'bottom_to_top':
                    return aLoc.z - bLoc.z;
                case 'front_to_back':
                    return aLoc.y - bLoc.y;
                default:
                    return aLoc.x - bLoc.x; // Default to left-to-right
            }
        });
    }

    /**
     * Generate element names based on convention
     */
    private generateElementNames(sortedElements: any[], convention: string): string[] {
        const names: string[] = [];
        const typeCounts: any = {};

        for (const element of sortedElements) {
            const category = element.category;
            const description = element.parameters?.['BIMSF_Description'] || '';

            let name = '';

            switch (convention) {
                case 'flc_standard':
                    if (category === 'Structural Framing') {
                        if (description.includes('TTOP') || description.includes('TOP')) {
                            name = 'Top Track';
                        } else if (description.includes('TBOT') || description.includes('BOT')) {
                            name = 'Bottom Track';
                        } else if (description.includes('EV') || description.includes('STUD')) {
                            typeCounts['Stud'] = (typeCounts['Stud'] || 0) + 1;
                            name = `Stud ${typeCounts['Stud']}`;
                        } else {
                            typeCounts[category] = (typeCounts[category] || 0) + 1;
                            name = `${category} ${typeCounts[category]}`;
                        }
                    } else {
                        typeCounts[category] = (typeCounts[category] || 0) + 1;
                        name = `${category} ${typeCounts[category]}`;
                    }
                    break;

                case 'sequential_numbers':
                    typeCounts[category] = (typeCounts[category] || 0) + 1;
                    name = `${category} ${typeCounts[category]}`;
                    break;

                default:
                    name = element.parameters?.['BIMSF_Label'] || `Element ${names.length + 1}`;
            }

            names.push(name);
        }

        return names;
    }

    /**
     * Perform spatial analysis of elements
     */
    private performSpatialAnalysis(elements: any[]): any {
        const analysis = {
            bounds: { min: { x: Infinity, y: Infinity, z: Infinity }, max: { x: -Infinity, y: -Infinity, z: -Infinity } },
            center: { x: 0, y: 0, z: 0 },
            distribution: { x: [] as number[], y: [] as number[], z: [] as number[] },
            clustering: [] as any[]
        };

        // Calculate bounds and collect positions
        for (const element of elements) {
            const loc = element.geometry?.location;
            if (loc) {
                analysis.bounds.min.x = Math.min(analysis.bounds.min.x, loc.x);
                analysis.bounds.min.y = Math.min(analysis.bounds.min.y, loc.y);
                analysis.bounds.min.z = Math.min(analysis.bounds.min.z, loc.z);
                analysis.bounds.max.x = Math.max(analysis.bounds.max.x, loc.x);
                analysis.bounds.max.y = Math.max(analysis.bounds.max.y, loc.y);
                analysis.bounds.max.z = Math.max(analysis.bounds.max.z, loc.z);

                analysis.distribution.x.push(loc.x);
                analysis.distribution.y.push(loc.y);
                analysis.distribution.z.push(loc.z);
            }
        }

        // Calculate center
        if (elements.length > 0) {
            analysis.center.x = (analysis.bounds.min.x + analysis.bounds.max.x) / 2;
            analysis.center.y = (analysis.bounds.min.y + analysis.bounds.max.y) / 2;
            analysis.center.z = (analysis.bounds.min.z + analysis.bounds.max.z) / 2;
        }

        return analysis;
    }

    /**
     * Analyze component types and their relationships
     */
    private analyzeComponentTypes(elements: any[]): any {
        const analysis = {
            categories: {} as any,
            families: {} as any,
            types: {} as any,
            relationships: [] as any[]
        };

        for (const element of elements) {
            const category = element.category || 'Unknown';
            const family = element.familyName || 'Unknown';
            const type = element.typeName || 'Unknown';

            // Count categories
            analysis.categories[category] = (analysis.categories[category] || 0) + 1;

            // Count families
            analysis.families[family] = (analysis.families[family] || 0) + 1;

            // Count types
            analysis.types[type] = (analysis.types[type] || 0) + 1;

            // Analyze relationships
            if (element.relationships?.dependentIds?.length > 0) {
                analysis.relationships.push({
                    elementId: element.id,
                    dependents: element.relationships.dependentIds.length,
                    hasHost: !!element.relationships.hostId
                });
            }
        }

        return analysis;
    }

    /**
     * Analyze quality issues in the panel
     */
    private analyzeQualityIssues(elements: any[]): any {
        const issues = {
            missing_parameters: [] as any[],
            invalid_values: [] as any[],
            naming_issues: [] as any[],
            spatial_issues: [] as any[],
            summary: { total: 0, critical: 0, warnings: 0 }
        };

        for (const element of elements) {
            // Check for missing FLC parameters
            const requiredParams = ['BIMSF_Id', 'BIMSF_Container', 'BIMSF_Label'];
            for (const param of requiredParams) {
                if (!element.parameters?.[param]) {
                    issues.missing_parameters.push({
                        elementId: element.id,
                        parameter: param,
                        severity: 'critical'
                    });
                }
            }

            // Check naming conventions
            const label = element.parameters?.['BIMSF_Label'];
            if (label && !this.validateFLCNaming(label)) {
                issues.naming_issues.push({
                    elementId: element.id,
                    currentName: label,
                    issue: 'Does not follow FLC naming convention',
                    severity: 'warning'
                });
            }
        }

        // Calculate summary
        issues.summary.total = issues.missing_parameters.length + issues.invalid_values.length +
                              issues.naming_issues.length + issues.spatial_issues.length;
        issues.summary.critical = issues.missing_parameters.filter((i: any) => i.severity === 'critical').length;
        issues.summary.warnings = issues.summary.total - issues.summary.critical;

        return issues;
    }

    /**
     * Calculate performance metrics for the panel
     */
    private calculatePerformanceMetrics(elements: any[]): any {
        return {
            elementCount: elements.length,
            categoryCoverage: Object.keys(this.analyzeComponentTypes(elements).categories).length,
            parameterCompleteness: this.calculateParameterCompleteness(elements),
            spatialEfficiency: this.calculateSpatialEfficiency(elements),
            namingConsistency: this.calculateNamingConsistency(elements)
        };
    }

    /**
     * Generate recommendations for panel improvements
     */
    private generatePanelRecommendations(analysis: any): any[] {
        const recommendations = [];

        // Check for missing parameters
        if (analysis.qualityAnalysis?.missing_parameters?.length > 0) {
            recommendations.push({
                type: 'missing_parameters',
                priority: 'high',
                description: `${analysis.qualityAnalysis.missing_parameters.length} elements are missing required FLC parameters`,
                action: 'Add missing BIMSF_Id, BIMSF_Container, and BIMSF_Label parameters'
            });
        }

        // Check for naming issues
        if (analysis.qualityAnalysis?.naming_issues?.length > 0) {
            recommendations.push({
                type: 'naming_convention',
                priority: 'medium',
                description: `${analysis.qualityAnalysis.naming_issues.length} elements have naming convention issues`,
                action: 'Standardize element names to follow FLC convention'
            });
        }

        // Check for spatial organization
        if (analysis.spatialAnalysis && analysis.elementTypes['Structural Framing'] > 2) {
            recommendations.push({
                type: 'spatial_organization',
                priority: 'medium',
                description: 'Panel elements could benefit from left-to-right renumbering',
                action: 'Apply spatial renaming to organize studs and tracks'
            });
        }

        return recommendations;
    }

    /**
     * Helper methods for metrics calculation
     */
    private calculateParameterCompleteness(elements: any[]): number {
        const requiredParams = ['BIMSF_Id', 'BIMSF_Container', 'BIMSF_Label'];
        let totalRequired = elements.length * requiredParams.length;
        let totalPresent = 0;

        for (const element of elements) {
            for (const param of requiredParams) {
                if (element.parameters?.[param]) {
                    totalPresent++;
                }
            }
        }

        return totalRequired > 0 ? (totalPresent / totalRequired) * 100 : 0;
    }

    private calculateSpatialEfficiency(elements: any[]): number {
        // Simple metric based on spatial distribution
        const spatial = this.performSpatialAnalysis(elements);
        const volume = (spatial.bounds.max.x - spatial.bounds.min.x) *
                      (spatial.bounds.max.y - spatial.bounds.min.y) *
                      (spatial.bounds.max.z - spatial.bounds.min.z);
        return volume > 0 ? Math.min(100, (elements.length / volume) * 1000) : 0;
    }

    private calculateNamingConsistency(elements: any[]): number {
        let consistentNames = 0;
        for (const element of elements) {
            const label = element.parameters?.['BIMSF_Label'];
            if (label && this.validateFLCNaming(label)) {
                consistentNames++;
            }
        }
        return elements.length > 0 ? (consistentNames / elements.length) * 100 : 0;
    }

    // ==========================================
    // 🚀 ADVANCED AI PARAMETER TOOLS
    // ==========================================

    /**
     * Mass parameter processing with FAFB performance
     */
    public async massParameterUpdate(options: any = {}): Promise<any> {
        try {
            console.log('🚀 Starting mass parameter update...');

            const elementIds = options.elementIds || [];
            const operations = options.operations || [];
            const chunkSize = options.chunkSize || 250;
            const maxConcurrency = options.maxConcurrency || 3;

            if (elementIds.length === 0) {
                return {
                    success: false,
                    message: 'No elements provided for mass update',
                    data: null
                };
            }

            // Start monitoring session
            const sessionId = `mass_update_${Date.now()}`;
            console.log(`📡 Starting mass update session: ${sessionId} (${elementIds.length} elements)`);

            const results = {
                sessionId,
                totalElements: elementIds.length,
                totalOperations: operations.length,
                processed: 0,
                successful: 0,
                failed: 0,
                chunks: [] as any[],
                startTime: new Date().toISOString(),
                endTime: null as string | null
            };

            // Process in chunks for performance
            const chunks = this.chunkArray(elementIds as string[], chunkSize);
            let processedElements = 0;

            for (let i = 0; i < chunks.length; i++) {
                const chunk = chunks[i];
                console.log(`📦 Processing chunk ${i + 1}/${chunks.length} (${chunk.length} elements)`);

                try {
                    // Get elements for this chunk
                    const elementsResult = await this.getElementParameters(chunk);

                    if (elementsResult.success) {
                        // Apply operations to each element in chunk
                        const chunkModifications = [];

                        for (const element of elementsResult.data.elements) {
                            for (const operation of operations) {
                                const modification = this.generateModificationFromOperation(element, operation);
                                if (modification) {
                                    chunkModifications.push(modification);
                                }
                            }
                        }

                        // Apply modifications if any
                        if (chunkModifications.length > 0) {
                            const modifyResult = await this.modifyParameters(chunkModifications, false);
                            results.successful += modifyResult.success ? chunkModifications.length : 0;
                            results.failed += modifyResult.success ? 0 : chunkModifications.length;
                        }

                        results.chunks.push({
                            chunkIndex: i,
                            elementCount: chunk.length,
                            modificationsApplied: chunkModifications.length,
                            success: true
                        });
                    } else {
                        results.failed += chunk.length;
                        results.chunks.push({
                            chunkIndex: i,
                            elementCount: chunk.length,
                            modificationsApplied: 0,
                            success: false,
                            error: elementsResult.message
                        });
                    }
                } catch (chunkError) {
                    console.error(`❌ Error processing chunk ${i + 1}:`, chunkError);
                    results.failed += chunk.length;
                    results.chunks.push({
                        chunkIndex: i,
                        elementCount: chunk.length,
                        modificationsApplied: 0,
                        success: false,
                        error: chunkError instanceof Error ? chunkError.message : 'Unknown error'
                    });
                }

                processedElements += chunk.length;
                results.processed = processedElements;

                // Update progress
                console.log(`📊 Progress: ${((processedElements / elementIds.length) * 100).toFixed(1)}% (${processedElements}/${elementIds.length})`);

                // Small delay to prevent overwhelming Revit
                await this.delay(100);
            }

            results.endTime = new Date().toISOString();
            console.log(`✅ Mass update session completed: ${sessionId}`);

            return {
                success: results.successful > 0,
                message: `Mass update complete: ${results.successful} successful, ${results.failed} failed`,
                data: results
            };
        } catch (error) {
            console.error('❌ Error in mass parameter update:', error);
            return {
                success: false,
                message: `Mass parameter update failed: ${error instanceof Error ? error.message : 'Unknown error'}`,
                data: null
            };
        }
    }

    /**
     * Validate and fix FLC parameter issues automatically
     */
    public async fixFLCParameters(options: any = {}): Promise<any> {
        try {
            console.log('🔧 Starting FLC parameter fix...');

            const elementsResult = await this.getElementParameters(options.elementIds);
            if (!elementsResult.success) {
                return elementsResult;
            }

            const elements = elementsResult.data.elements;
            const fixes = [];
            const issues = [];

            for (const element of elements) {
                const elementFixes = this.generateFLCFixes(element);
                fixes.push(...elementFixes.fixes);
                issues.push(...elementFixes.issues);
            }

            if (options.dryRun !== false) {
                return {
                    success: true,
                    message: `Preview: ${fixes.length} FLC parameter fixes identified`,
                    data: {
                        dryRun: true,
                        fixes: fixes,
                        issues: issues,
                        summary: {
                            totalElements: elements.length,
                            elementsWithIssues: new Set(issues.map((i: any) => i.elementId)).size,
                            totalFixes: fixes.length
                        }
                    }
                };
            } else {
                // Apply fixes
                const result = await this.modifyParameters(fixes, false);
                return {
                    success: result.success,
                    message: result.success ?
                        `Successfully applied ${fixes.length} FLC parameter fixes` :
                        `Failed to apply FLC fixes: ${result.message}`,
                    data: {
                        applied: result.success,
                        fixes: fixes,
                        issues: issues,
                        modificationResult: result.data
                    }
                };
            }
        } catch (error) {
            console.error('❌ Error fixing FLC parameters:', error);
            return {
                success: false,
                message: `Error fixing FLC parameters: ${error instanceof Error ? error.message : 'Unknown error'}`,
                data: null
            };
        }
    }

    /**
     * Detect and group panels automatically
     */
    public async detectPanelGroups(options: any = {}): Promise<any> {
        try {
            console.log('🔍 Detecting panel groups...');

            const elementsResult = await this.getElementParameters(options.elementIds);
            if (!elementsResult.success) {
                return elementsResult;
            }

            const elements = elementsResult.data.elements;
            const panels = this.groupElementsByPanel(elements);
            const analysis = this.analyzePanelGroups(panels);

            return {
                success: true,
                message: `Detected ${Object.keys(panels).length} panel groups`,
                data: {
                    panels: panels,
                    analysis: analysis,
                    summary: {
                        totalElements: elements.length,
                        totalPanels: Object.keys(panels).length,
                        averageElementsPerPanel: elements.length / Math.max(Object.keys(panels).length, 1),
                        panelTypes: analysis.panelTypes
                    }
                }
            };
        } catch (error) {
            console.error('❌ Error detecting panel groups:', error);
            return {
                success: false,
                message: `Error detecting panel groups: ${error instanceof Error ? error.message : 'Unknown error'}`,
                data: null
            };
        }
    }

    // ==========================================
    // 🔧 HELPER METHODS FOR ADVANCED TOOLS
    // ==========================================

    /**
     * Chunk array into smaller arrays
     */
    private chunkArray<T>(array: T[], chunkSize: number): T[][] {
        const chunks: T[][] = [];
        for (let i = 0; i < array.length; i += chunkSize) {
            chunks.push(array.slice(i, i + chunkSize));
        }
        return chunks;
    }

    /**
     * Generate modification from operation
     */
    private generateModificationFromOperation(element: any, operation: any): any {
        switch (operation.type) {
            case 'set_parameter':
                return {
                    elementId: element.id,
                    parameter: operation.parameter,
                    newValue: operation.value,
                    reason: operation.reason || `Set ${operation.parameter} to ${operation.value}`
                };

            case 'fix_missing_bimsf':
                if (!element.parameters?.[operation.parameter]) {
                    return {
                        elementId: element.id,
                        parameter: operation.parameter,
                        newValue: this.generateFLCParameterValue(operation.parameter, element),
                        reason: `Add missing FLC parameter ${operation.parameter}`
                    };
                }
                break;

            case 'standardize_naming':
                const currentName = element.parameters?.['BIMSF_Label'];
                const standardName = this.generateStandardName(element, operation.convention);
                if (currentName !== standardName) {
                    return {
                        elementId: element.id,
                        parameter: 'BIMSF_Label',
                        newValue: standardName,
                        reason: `Standardize naming to ${operation.convention} convention`
                    };
                }
                break;
        }
        return null;
    }

    /**
     * Generate FLC fixes for an element
     */
    private generateFLCFixes(element: any): any {
        const fixes = [];
        const issues = [];

        // Check for missing required FLC parameters
        const requiredParams = ['BIMSF_Id', 'BIMSF_Container', 'BIMSF_Label'];
        for (const param of requiredParams) {
            if (!element.parameters?.[param]) {
                issues.push({
                    elementId: element.id,
                    type: 'missing_parameter',
                    parameter: param,
                    severity: 'critical'
                });

                fixes.push({
                    elementId: element.id,
                    parameter: param,
                    newValue: this.generateFLCParameterValue(param, element),
                    reason: `Add missing required FLC parameter ${param}`
                });
            }
        }

        // Check naming convention
        const label = element.parameters?.['BIMSF_Label'];
        if (label && !this.validateFLCNaming(label)) {
            issues.push({
                elementId: element.id,
                type: 'naming_convention',
                parameter: 'BIMSF_Label',
                currentValue: label,
                severity: 'warning'
            });

            fixes.push({
                elementId: element.id,
                parameter: 'BIMSF_Label',
                newValue: this.suggestFLCNaming(label, element),
                reason: 'Fix naming convention to follow FLC standards'
            });
        }

        return { fixes, issues };
    }

    /**
     * Group elements by panel
     */
    private groupElementsByPanel(elements: any[]): any {
        const panels: any = {};

        for (const element of elements) {
            const container = element.parameters?.['BIMSF_Container'] ||
                            element.parameters?.['MasterContainer'] ||
                            'Unassigned';

            if (!panels[container]) {
                panels[container] = {
                    name: container,
                    elements: [],
                    types: {},
                    bounds: null,
                    center: null
                };
            }

            panels[container].elements.push(element);

            const category = element.category || 'Unknown';
            panels[container].types[category] = (panels[container].types[category] || 0) + 1;
        }

        // Calculate bounds and center for each panel
        for (const panel of Object.values(panels) as any[]) {
            panel.bounds = this.calculatePanelBounds(panel.elements);
            panel.center = this.calculatePanelCenter(panel.elements);
        }

        return panels;
    }

    /**
     * Analyze panel groups
     */
    private analyzePanelGroups(panels: any): any {
        const analysis = {
            panelTypes: {},
            totalElements: 0,
            averageSize: 0,
            spatialDistribution: {},
            recommendations: []
        };

        for (const panel of Object.values(panels) as any[]) {
            analysis.totalElements += panel.elements.length;

            // Classify panel type
            const panelType = this.classifyPanelType(panel);
            (analysis.panelTypes as any)[panelType] = ((analysis.panelTypes as any)[panelType] || 0) + 1;
        }

        analysis.averageSize = analysis.totalElements / Math.max(Object.keys(panels).length, 1);

        return analysis;
    }

    /**
     * Helper methods for panel analysis
     */
    private calculatePanelBounds(elements: any[]): any {
        const bounds = {
            min: { x: Infinity, y: Infinity, z: Infinity },
            max: { x: -Infinity, y: -Infinity, z: -Infinity }
        };

        for (const element of elements) {
            const loc = element.geometry?.location;
            if (loc) {
                bounds.min.x = Math.min(bounds.min.x, loc.x);
                bounds.min.y = Math.min(bounds.min.y, loc.y);
                bounds.min.z = Math.min(bounds.min.z, loc.z);
                bounds.max.x = Math.max(bounds.max.x, loc.x);
                bounds.max.y = Math.max(bounds.max.y, loc.y);
                bounds.max.z = Math.max(bounds.max.z, loc.z);
            }
        }

        return bounds;
    }

    private calculatePanelCenter(elements: any[]): any {
        let totalX = 0, totalY = 0, totalZ = 0, count = 0;

        for (const element of elements) {
            const loc = element.geometry?.location;
            if (loc) {
                totalX += loc.x;
                totalY += loc.y;
                totalZ += loc.z;
                count++;
            }
        }

        return count > 0 ? {
            x: totalX / count,
            y: totalY / count,
            z: totalZ / count
        } : { x: 0, y: 0, z: 0 };
    }

    private classifyPanelType(panel: any): string {
        const types = panel.types;

        if (types['Walls'] && types['Structural Framing']) {
            return 'framed_wall';
        } else if (types['Structural Framing']) {
            return 'framing_only';
        } else if (types['Walls']) {
            return 'wall_only';
        } else {
            return 'other';
        }
    }

    private generateStandardName(element: any, convention: string): string {
        // Implementation for generating standard names based on convention
        return element.parameters?.['BIMSF_Label'] || 'Standard Name';
    }

    private delay(ms: number): Promise<void> {
        return new Promise(resolve => setTimeout(resolve, ms));
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
