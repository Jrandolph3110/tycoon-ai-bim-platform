/**
 * FLCAdapter - Integration layer for F.L. Crane & Sons steel framing workflows
 * 
 * Provides:
 * - Steel framing operation abstractions
 * - FLC parameter management
 * - Panel processing logic
 * - Renumbering engine integration
 * - Extensible storage management
 */

import { RevitBridge, RevitElement, RevitSelection } from '../revit/RevitBridge.js';
import chalk from 'chalk';

export interface FLCPanel {
    id: string;
    containerValue: string;
    mainPanel: boolean;
    elements: RevitElement[];
    boundingBox?: {
        min: { x: number; y: number; z: number };
        max: { x: number; y: number; z: number };
    };
    width?: number;
    height?: number;
}

export interface FLCFramingOptions {
    studSpacing: number; // inches: 12, 16, 19.2, 24
    panelMaxWidth: number; // feet: typically 10
    wallType: string; // FLC_{thickness}_{Int/Ext}_{options}
    includeOpenings: boolean;
    sequenceStuds: boolean; // left to right
    applyParameters: boolean;
}

export interface FLCRenumberOptions {
    startNumber: number;
    prefix?: string;
    includeSubassemblies: boolean;
    sequenceType: 'left-to-right' | 'bottom-to-top' | 'custom';
}

export class FLCAdapter {
    private revitBridge: RevitBridge;
    private debugMode: boolean = true;

    constructor(revitBridge: RevitBridge) {
        this.revitBridge = revitBridge;
    }

    /**
     * Analyze selected walls for steel framing potential
     */
    async analyzeWallsForFraming(selection?: RevitSelection): Promise<{
        walls: RevitElement[];
        totalLength: number;
        estimatedPanels: number;
        recommendations: string[];
    }> {
        try {
            const currentSelection = selection || await this.revitBridge.getSelection();
            
            // Filter for walls
            const walls = currentSelection.elements.filter(el => 
                el.category === 'Walls' || el.category === 'OST_Walls'
            );

            if (walls.length === 0) {
                throw new Error('No walls found in selection');
            }

            let totalLength = 0;
            const recommendations: string[] = [];

            // Analyze each wall
            for (const wall of walls) {
                const length = this.getWallLength(wall);
                totalLength += length;

                // Check for existing framing
                if (this.hasExistingFraming(wall)) {
                    recommendations.push(`Wall ${wall.id}: Already has framing elements`);
                }

                // Check wall type compatibility
                if (!this.isCompatibleWallType(wall)) {
                    recommendations.push(`Wall ${wall.id}: Wall type may not be suitable for steel framing`);
                }
            }

            // Estimate panel count (assuming 10ft max panels)
            const estimatedPanels = Math.ceil(totalLength / 10);

            this.log(`Analyzed ${walls.length} walls, ${totalLength.toFixed(1)}ft total length`);

            return {
                walls,
                totalLength,
                estimatedPanels,
                recommendations
            };

        } catch (error) {
            this.logError('Failed to analyze walls for framing', error);
            throw error;
        }
    }

    /**
     * Create steel framing for selected walls
     */
    async createSteelFraming(options: FLCFramingOptions, selection?: RevitSelection): Promise<{
        success: boolean;
        panelsCreated: number;
        elementsCreated: number;
        errors: string[];
    }> {
        try {
            const analysis = await this.analyzeWallsForFraming(selection);
            
            if (analysis.walls.length === 0) {
                throw new Error('No suitable walls found for framing');
            }

            const response = await this.revitBridge.sendCommand({
                type: 'create',
                payload: {
                    operation: 'steel_framing',
                    walls: analysis.walls.map(w => w.id),
                    options: {
                        studSpacing: options.studSpacing,
                        panelMaxWidth: options.panelMaxWidth,
                        wallType: options.wallType,
                        includeOpenings: options.includeOpenings,
                        sequenceStuds: options.sequenceStuds,
                        applyParameters: options.applyParameters
                    }
                }
            });

            if (!response.success) {
                throw new Error(response.error || 'Failed to create steel framing');
            }

            this.log(`Created steel framing: ${response.data.panelsCreated} panels, ${response.data.elementsCreated} elements`);

            return {
                success: true,
                panelsCreated: response.data.panelsCreated || 0,
                elementsCreated: response.data.elementsCreated || 0,
                errors: response.data.errors || []
            };

        } catch (error) {
            this.logError('Failed to create steel framing', error);
            throw error;
        }
    }

    /**
     * Renumber selected elements using FLC standards
     */
    async renumberElements(options: FLCRenumberOptions, selection?: RevitSelection): Promise<{
        success: boolean;
        elementsProcessed: number;
        errors: string[];
    }> {
        try {
            const currentSelection = selection || await this.revitBridge.getSelection();
            
            if (currentSelection.elements.length === 0) {
                throw new Error('No elements selected for renumbering');
            }

            const response = await this.revitBridge.sendCommand({
                type: 'modify',
                payload: {
                    operation: 'renumber_elements',
                    elements: currentSelection.elements.map(e => e.id),
                    options: {
                        startNumber: options.startNumber,
                        prefix: options.prefix,
                        includeSubassemblies: options.includeSubassemblies,
                        sequenceType: options.sequenceType
                    }
                }
            });

            if (!response.success) {
                throw new Error(response.error || 'Failed to renumber elements');
            }

            this.log(`Renumbered ${response.data.elementsProcessed} elements`);

            return {
                success: true,
                elementsProcessed: response.data.elementsProcessed || 0,
                errors: response.data.errors || []
            };

        } catch (error) {
            this.logError('Failed to renumber elements', error);
            throw error;
        }
    }

    /**
     * Get FLC panels from current selection
     */
    async getFLCPanels(selection?: RevitSelection): Promise<FLCPanel[]> {
        try {
            const currentSelection = selection || await this.revitBridge.getSelection();
            
            const response = await this.revitBridge.sendCommand({
                type: 'query',
                payload: {
                    operation: 'get_flc_panels',
                    elements: currentSelection.elements.map(e => e.id)
                }
            });

            if (!response.success) {
                throw new Error(response.error || 'Failed to get FLC panels');
            }

            return response.data.panels || [];

        } catch (error) {
            this.logError('Failed to get FLC panels', error);
            throw error;
        }
    }

    /**
     * Validate panel ticket requirements
     */
    async validatePanelTickets(selection?: RevitSelection): Promise<{
        valid: boolean;
        issues: string[];
        recommendations: string[];
    }> {
        try {
            const panels = await this.getFLCPanels(selection);
            
            const issues: string[] = [];
            const recommendations: string[] = [];

            for (const panel of panels) {
                // Check for required parameters
                if (!panel.containerValue) {
                    issues.push(`Panel ${panel.id}: Missing BIMSF_Container value`);
                }

                // Check panel width
                if (panel.width && panel.width > 10) {
                    issues.push(`Panel ${panel.id}: Width ${panel.width}ft exceeds 10ft transportation limit`);
                }

                // Check for elements
                if (panel.elements.length === 0) {
                    issues.push(`Panel ${panel.id}: No framing elements found`);
                }
            }

            if (panels.length === 0) {
                recommendations.push('No FLC panels found in selection. Consider running steel framing operation first.');
            }

            return {
                valid: issues.length === 0,
                issues,
                recommendations
            };

        } catch (error) {
            this.logError('Failed to validate panel tickets', error);
            throw error;
        }
    }

    /**
     * Helper: Get wall length from element
     */
    private getWallLength(wall: RevitElement): number {
        // Try to get length from parameters
        if (wall.parameters?.Length) {
            return parseFloat(wall.parameters.Length) || 0;
        }
        
        // Fallback to bounding box calculation
        if (wall.geometry?.boundingBox) {
            const bb = wall.geometry.boundingBox;
            const dx = bb.max.x - bb.min.x;
            const dy = bb.max.y - bb.min.y;
            return Math.sqrt(dx * dx + dy * dy);
        }

        return 0;
    }

    /**
     * Helper: Check if wall has existing framing
     */
    private hasExistingFraming(wall: RevitElement): boolean {
        // Check for BIMSF parameters or related elements
        return !!(wall.parameters?.BIMSF_Container || wall.relationships?.dependentIds?.length);
    }

    /**
     * Helper: Check if wall type is compatible with steel framing
     */
    private isCompatibleWallType(wall: RevitElement): boolean {
        const typeName = wall.typeName?.toLowerCase() || '';
        
        // Check for FLC wall types or compatible types
        return typeName.includes('flc_') || 
               typeName.includes('steel') || 
               typeName.includes('frame') ||
               !typeName.includes('concrete') && !typeName.includes('masonry');
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
            console.log(chalk.green('[FLCAdapter]'), message);
        }
    }

    /**
     * Log error messages
     */
    private logError(message: string, error?: any): void {
        console.error(chalk.red('[FLCAdapter Error]'), message, error || '');
    }
}
