/**
 * Revit to BIM Vector Database Adapter
 * Converts streaming Revit data directly to BIM vector database format
 */

import { BimElement, BimVectorDatabase } from '../core/BimVectorDatabase.js';
import chalk from 'chalk';
import { v4 as uuidv4 } from 'uuid';

export interface RevitElementData {
    id: string;
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
        volume?: number;
        area?: number;
    };
}

export interface StreamingMetadata {
    documentTitle: string;
    viewName?: string;
    processingTier: string;
    chunkNumber: number;
    streamingSession: string;
    totalElements: number;
    processedElements: number;
}

export class RevitToBimAdapter {
    private bimVectorDb: BimVectorDatabase;
    private currentSession: string;
    private elementsProcessed = 0;
    private elementsStored = 0;

    constructor(bimVectorDb: BimVectorDatabase) {
        this.bimVectorDb = bimVectorDb;
        this.currentSession = uuidv4();
    }

    /**
     * Start a new streaming session
     */
    startStreamingSession(metadata: StreamingMetadata): void {
        this.currentSession = uuidv4();
        this.elementsProcessed = 0;
        this.elementsStored = 0;
        
        console.log(chalk.blue(`🚀 Starting BIM streaming session: ${this.currentSession}`));
        console.log(chalk.blue(`📊 Project: ${metadata.documentTitle}, Tier: ${metadata.processingTier}`));
    }

    /**
     * Process and store a chunk of Revit elements directly to vector database
     */
    async processChunk(
        elements: RevitElementData[],
        metadata: StreamingMetadata
    ): Promise<{
        processed: number;
        stored: number;
        errors: string[];
    }> {
        const errors: string[] = [];
        let processed = 0;
        let stored = 0;

        try {
            console.log(chalk.blue(`📥 Processing chunk ${metadata.chunkNumber}: ${elements.length} elements`));

            // Convert Revit elements to BIM elements
            const bimElements: BimElement[] = [];

            for (const revitElement of elements) {
                try {
                    const bimElement = this.convertRevitToBim(revitElement, metadata);
                    bimElements.push(bimElement);
                    processed++;
                } catch (error) {
                    errors.push(`Failed to convert element ${revitElement.id}: ${error instanceof Error ? error.message : String(error)}`);
                }
            }

            // Store directly in vector database
            if (bimElements.length > 0) {
                await this.bimVectorDb.storeBimElements(bimElements);
                stored = bimElements.length;
                
                console.log(chalk.green(`✅ Stored ${stored} elements in vector database`));
            }

            this.elementsProcessed += processed;
            this.elementsStored += stored;

            // Log progress
            const progress = (metadata.processedElements / metadata.totalElements) * 100;
            console.log(chalk.green(`📊 Session progress: ${this.elementsStored} stored, ${progress.toFixed(1)}% complete`));

            return { processed, stored, errors };

        } catch (error) {
            const errorMsg = `Failed to process chunk ${metadata.chunkNumber}: ${error instanceof Error ? error.message : String(error)}`;
            console.error(chalk.red(`❌ ${errorMsg}`));
            errors.push(errorMsg);
            
            return { processed, stored, errors };
        }
    }

    /**
     * Complete the streaming session
     */
    async completeSession(metadata: StreamingMetadata): Promise<{
        sessionId: string;
        totalProcessed: number;
        totalStored: number;
        success: boolean;
    }> {
        try {
            console.log(chalk.green(`🎉 Completing BIM streaming session: ${this.currentSession}`));
            console.log(chalk.green(`📊 Final stats: ${this.elementsStored} elements stored in vector database`));

            // Get updated database stats
            const stats = await this.bimVectorDb.getStats();
            console.log(chalk.green(`📈 Database now contains ${stats.totalElements} total elements`));

            return {
                sessionId: this.currentSession,
                totalProcessed: this.elementsProcessed,
                totalStored: this.elementsStored,
                success: true
            };

        } catch (error) {
            console.error(chalk.red(`❌ Failed to complete session: ${error instanceof Error ? error.message : String(error)}`));
            
            return {
                sessionId: this.currentSession,
                totalProcessed: this.elementsProcessed,
                totalStored: this.elementsStored,
                success: false
            };
        }
    }

    /**
     * Convert Revit element to BIM element format
     */
    private convertRevitToBim(revitElement: RevitElementData, metadata: StreamingMetadata): BimElement {
        const now = new Date();

        // Extract level from parameters
        const level = this.extractLevel(revitElement.parameters);
        
        // Extract phase from parameters
        const phase = this.extractPhase(revitElement.parameters);

        const bimElement: BimElement = {
            id: `${metadata.documentTitle}_${revitElement.id}_${this.currentSession}`,
            elementId: revitElement.id,
            category: revitElement.category,
            familyName: revitElement.familyName,
            typeName: revitElement.typeName,
            parameters: this.cleanParameters(revitElement.parameters),
            geometry: revitElement.geometry,
            relationships: [], // Could be populated with spatial relationships
            projectInfo: {
                documentTitle: metadata.documentTitle,
                viewName: metadata.viewName,
                level: level,
                phase: phase
            },
            metadata: {
                created: now,
                lastModified: now,
                processingTier: metadata.processingTier,
                chunkNumber: metadata.chunkNumber,
                streamingSession: this.currentSession
            }
        };

        return bimElement;
    }

    /**
     * Extract level information from parameters
     */
    private extractLevel(parameters: Record<string, any>): string | undefined {
        // Common level parameter names in Revit
        const levelParams = ['Level', 'Base Level', 'Top Level', 'Reference Level'];
        
        for (const param of levelParams) {
            if (parameters[param] && typeof parameters[param] === 'string') {
                return parameters[param];
            }
        }

        return undefined;
    }

    /**
     * Extract phase information from parameters
     */
    private extractPhase(parameters: Record<string, any>): string | undefined {
        // Common phase parameter names in Revit
        const phaseParams = ['Phase Created', 'Phase Demolished', 'Phase'];
        
        for (const param of phaseParams) {
            if (parameters[param] && typeof parameters[param] === 'string') {
                return parameters[param];
            }
        }

        return undefined;
    }

    /**
     * Clean and normalize parameters for vector database storage
     */
    private cleanParameters(parameters: Record<string, any>): Record<string, any> {
        const cleaned: Record<string, any> = {};

        for (const [key, value] of Object.entries(parameters)) {
            // Skip null, undefined, or empty values
            if (value == null || value === '') {
                continue;
            }

            // Convert complex objects to strings
            if (typeof value === 'object') {
                cleaned[key] = JSON.stringify(value);
            } else {
                cleaned[key] = value;
            }
        }

        return cleaned;
    }

    /**
     * Get current session statistics
     */
    getSessionStats(): {
        sessionId: string;
        elementsProcessed: number;
        elementsStored: number;
    } {
        return {
            sessionId: this.currentSession,
            elementsProcessed: this.elementsProcessed,
            elementsStored: this.elementsStored
        };
    }
}
