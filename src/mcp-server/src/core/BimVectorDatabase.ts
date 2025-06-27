/**
 * BIM Vector Database Manager
 * High-performance vector database for BIM element storage and semantic search
 */

import { ChromaClient, OpenAIEmbeddingFunction, Collection } from 'chromadb';
import chalk from 'chalk';
import { v4 as uuidv4 } from 'uuid';

export interface BimElement {
    id: string;
    elementId: string;
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
    relationships?: string[];
    projectInfo: {
        documentTitle: string;
        viewName?: string;
        level?: string;
        phase?: string;
    };
    metadata: {
        created: Date;
        lastModified: Date;
        processingTier: string;
        chunkNumber?: number;
        streamingSession?: string;
    };
}

export interface BimSearchResult {
    element: BimElement;
    similarity: number;
    distance: number;
}

export interface BimQueryOptions {
    category?: string;
    familyName?: string;
    level?: string;
    phase?: string;
    minSimilarity?: number;
    maxResults?: number;
    includeGeometry?: boolean;
    includeParameters?: boolean;
}

export class BimVectorDatabase {
    private client: ChromaClient;
    private collection: Collection | null = null;
    private embeddingFunction: OpenAIEmbeddingFunction;
    private isInitialized = false;
    private collectionName = 'bim_elements';

    constructor(
        private options: {
            chromaUrl?: string;
            openaiApiKey?: string;
            collectionName?: string;
            embeddingModel?: string;
        } = {}
    ) {
        this.collectionName = options.collectionName || 'bim_elements';
        
        // Initialize Chroma client
        this.client = new ChromaClient({
            path: options.chromaUrl || 'http://localhost:8000'
        });

        // Initialize embedding function
        this.embeddingFunction = new OpenAIEmbeddingFunction({
            openai_api_key: options.openaiApiKey || process.env.OPENAI_API_KEY || '',
            openai_model: options.embeddingModel || 'text-embedding-3-small'
        });
    }

    /**
     * Initialize the vector database
     */
    async initialize(): Promise<void> {
        try {
            console.log(chalk.blue('🚀 Initializing BIM Vector Database...'));

            // Create or get collection
            try {
                this.collection = await this.client.getCollection({
                    name: this.collectionName,
                    embeddingFunction: this.embeddingFunction
                });
                console.log(chalk.green(`✅ Connected to existing collection: ${this.collectionName}`));
            } catch (error) {
                // Collection doesn't exist, create it
                this.collection = await this.client.createCollection({
                    name: this.collectionName,
                    embeddingFunction: this.embeddingFunction,
                    metadata: {
                        description: 'BIM elements with semantic embeddings',
                        created: new Date().toISOString(),
                        version: '1.0.0'
                    }
                });
                console.log(chalk.green(`✅ Created new collection: ${this.collectionName}`));
            }

            // Get collection info
            const count = await this.collection.count();
            console.log(chalk.green(`📊 Collection contains ${count} BIM elements`));

            this.isInitialized = true;
            console.log(chalk.green('✅ BIM Vector Database initialized successfully'));

        } catch (error) {
            console.error(chalk.red('❌ Failed to initialize BIM Vector Database:'), error);
            throw error;
        }
    }

    /**
     * Store BIM elements in the vector database
     */
    async storeBimElements(elements: BimElement[]): Promise<void> {
        if (!this.isInitialized || !this.collection) {
            throw new Error('BIM Vector Database not initialized');
        }

        try {
            console.log(chalk.blue(`📥 Storing ${elements.length} BIM elements...`));

            // Prepare data for Chroma
            const ids: string[] = [];
            const documents: string[] = [];
            const metadatas: any[] = [];

            for (const element of elements) {
                // Create unique ID
                const id = `${element.projectInfo.documentTitle}_${element.elementId}_${Date.now()}`;
                ids.push(id);

                // Create searchable document text
                const document = this.createSearchableText(element);
                documents.push(document);

                // Create metadata
                const metadata = {
                    elementId: element.elementId,
                    category: element.category,
                    familyName: element.familyName || '',
                    typeName: element.typeName || '',
                    documentTitle: element.projectInfo.documentTitle,
                    viewName: element.projectInfo.viewName || '',
                    level: element.projectInfo.level || '',
                    phase: element.projectInfo.phase || '',
                    processingTier: element.metadata.processingTier,
                    chunkNumber: element.metadata.chunkNumber || 0,
                    streamingSession: element.metadata.streamingSession || '',
                    created: element.metadata.created.toISOString(),
                    lastModified: element.metadata.lastModified.toISOString(),
                    hasGeometry: !!element.geometry,
                    parameterCount: Object.keys(element.parameters).length,
                    // Store geometry as JSON string if present
                    geometry: element.geometry ? JSON.stringify(element.geometry) : '',
                    // Store parameters as JSON string
                    parameters: JSON.stringify(element.parameters)
                };
                metadatas.push(metadata);
            }

            // Add to collection
            await this.collection.add({
                ids,
                documents,
                metadatas
            });

            console.log(chalk.green(`✅ Successfully stored ${elements.length} BIM elements`));

        } catch (error) {
            console.error(chalk.red('❌ Failed to store BIM elements:'), error);
            throw error;
        }
    }

    /**
     * Search for similar BIM elements
     */
    async searchSimilar(
        query: string,
        options: BimQueryOptions = {}
    ): Promise<BimSearchResult[]> {
        if (!this.isInitialized || !this.collection) {
            throw new Error('BIM Vector Database not initialized');
        }

        try {
            console.log(chalk.blue(`🔍 Searching for: "${query}"`));

            // Build where clause for filtering
            const whereClause: any = {};
            if (options.category) whereClause.category = options.category;
            if (options.familyName) whereClause.familyName = options.familyName;
            if (options.level) whereClause.level = options.level;
            if (options.phase) whereClause.phase = options.phase;

            // Perform semantic search
            const results = await this.collection.query({
                queryTexts: [query],
                nResults: options.maxResults || 10,
                where: Object.keys(whereClause).length > 0 ? whereClause : undefined
            });

            // Convert results to BimSearchResult format
            const searchResults: BimSearchResult[] = [];
            
            if (results.ids && results.ids[0] && results.metadatas && results.metadatas[0] && results.distances && results.distances[0]) {
                for (let i = 0; i < results.ids[0].length; i++) {
                    const metadata = results.metadatas[0][i] as any;
                    const distance = results.distances[0][i];
                    const similarity = 1 - distance; // Convert distance to similarity

                    // Skip results below similarity threshold
                    if (options.minSimilarity && similarity < options.minSimilarity) {
                        continue;
                    }

                    // Reconstruct BIM element
                    const element: BimElement = {
                        id: results.ids[0][i],
                        elementId: metadata.elementId,
                        category: metadata.category,
                        familyName: metadata.familyName,
                        typeName: metadata.typeName,
                        parameters: options.includeParameters ? JSON.parse(metadata.parameters || '{}') : {},
                        geometry: options.includeGeometry && metadata.geometry ? JSON.parse(metadata.geometry) : undefined,
                        relationships: [],
                        projectInfo: {
                            documentTitle: metadata.documentTitle,
                            viewName: metadata.viewName,
                            level: metadata.level,
                            phase: metadata.phase
                        },
                        metadata: {
                            created: new Date(metadata.created),
                            lastModified: new Date(metadata.lastModified),
                            processingTier: metadata.processingTier,
                            chunkNumber: metadata.chunkNumber,
                            streamingSession: metadata.streamingSession
                        }
                    };

                    searchResults.push({
                        element,
                        similarity,
                        distance
                    });
                }
            }

            console.log(chalk.green(`✅ Found ${searchResults.length} similar elements`));
            return searchResults;

        } catch (error) {
            console.error(chalk.red('❌ Search failed:'), error);
            throw error;
        }
    }

    /**
     * Get collection statistics
     */
    async getStats(): Promise<{
        totalElements: number;
        categories: Record<string, number>;
        projects: Record<string, number>;
        processingTiers: Record<string, number>;
    }> {
        if (!this.isInitialized || !this.collection) {
            throw new Error('BIM Vector Database not initialized');
        }

        try {
            const count = await this.collection.count();
            
            // Get all metadata for statistics
            const results = await this.collection.get({
                limit: count
            });

            const stats = {
                totalElements: count,
                categories: {} as Record<string, number>,
                projects: {} as Record<string, number>,
                processingTiers: {} as Record<string, number>
            };

            if (results.metadatas) {
                for (const metadata of results.metadatas as any[]) {
                    // Count categories
                    const category = metadata.category || 'Unknown';
                    stats.categories[category] = (stats.categories[category] || 0) + 1;

                    // Count projects
                    const project = metadata.documentTitle || 'Unknown';
                    stats.projects[project] = (stats.projects[project] || 0) + 1;

                    // Count processing tiers
                    const tier = metadata.processingTier || 'Unknown';
                    stats.processingTiers[tier] = (stats.processingTiers[tier] || 0) + 1;
                }
            }

            return stats;

        } catch (error) {
            console.error(chalk.red('❌ Failed to get stats:'), error);
            throw error;
        }
    }

    /**
     * Create searchable text from BIM element
     */
    private createSearchableText(element: BimElement): string {
        const parts: string[] = [];

        // Basic info
        parts.push(element.category);
        if (element.familyName) parts.push(element.familyName);
        if (element.typeName) parts.push(element.typeName);

        // Project info
        parts.push(element.projectInfo.documentTitle);
        if (element.projectInfo.level) parts.push(element.projectInfo.level);
        if (element.projectInfo.phase) parts.push(element.projectInfo.phase);

        // Parameters (key-value pairs)
        for (const [key, value] of Object.entries(element.parameters)) {
            if (value && typeof value === 'string') {
                parts.push(`${key}: ${value}`);
            }
        }

        // Geometry info
        if (element.geometry) {
            if (element.geometry.volume) parts.push(`volume: ${element.geometry.volume}`);
            if (element.geometry.area) parts.push(`area: ${element.geometry.area}`);
        }

        return parts.join(' ');
    }

    /**
     * Clear all data (use with caution!)
     */
    async clearAll(): Promise<void> {
        if (!this.isInitialized || !this.collection) {
            throw new Error('BIM Vector Database not initialized');
        }

        try {
            await this.client.deleteCollection({ name: this.collectionName });
            console.log(chalk.yellow(`⚠️ Cleared all data from collection: ${this.collectionName}`));
            
            // Reinitialize
            await this.initialize();
        } catch (error) {
            console.error(chalk.red('❌ Failed to clear data:'), error);
            throw error;
        }
    }
}
