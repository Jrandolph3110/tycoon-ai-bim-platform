/**
 * Memory Manager - Simplified TypeScript version for Tycoon
 * Core memory management with advanced features
 */

import fs from 'fs/promises';
import path from 'path';
import { v4 as uuidv4 } from 'uuid';
import chalk from 'chalk';

export interface Memory {
    id: string;
    title: string;
    content: string;
    tags: string[];
    category: string;
    importance: number;
    accessCount: number;
    lastAccessed: Date;
    created: Date;
    modified: Date;
    context: string;
    location?: string;
    relationships: string[];
    summary?: string;
    keyInsights?: string[];
    mood?: string;
    environment?: string;
}

export interface MemoryInput {
    title: string;
    content: string;
    tags?: string[];
    category?: string;
    importance?: number;
    context?: string;
    location?: string;
    relationships?: string[];
    mood?: string;
    environment?: string;
}

export interface MemoryContext {
    id: string;
    name: string;
    description: string;
    isActive: boolean;
    memoryIds: string[];
    created: Date;
    lastUsed: Date;
    settings: {
        autoTag?: boolean;
        defaultCategory?: string;
        importanceThreshold?: number;
    };
}

export interface MemoryManagerOptions {
    basePath?: string;
    autoBackup?: boolean;
    backupInterval?: number;
    enableAnalytics?: boolean;
    enableVectorSearch?: boolean;
    maxMemories?: number;
}

export class MemoryManager {
    private basePath: string;
    private memoriesPath: string;
    private metadataPath: string;
    private backupsPath: string;
    private memories = new Map<string, Memory>();
    private contexts = new Map<string, MemoryContext>();
    private options: Required<MemoryManagerOptions>;
    private currentContext = 'default';
    private isInitialized = false;

    constructor(options: MemoryManagerOptions = {}) {
        this.options = {
            basePath: options.basePath || process.cwd(),
            autoBackup: options.autoBackup ?? true,
            backupInterval: options.backupInterval || 60,
            enableAnalytics: options.enableAnalytics ?? true,
            enableVectorSearch: options.enableVectorSearch ?? true,
            maxMemories: options.maxMemories || 10000,
        };

        this.basePath = path.join(this.options.basePath, 'tycoon-memory-bank');
        this.memoriesPath = path.join(this.basePath, 'memories');
        this.metadataPath = path.join(this.basePath, 'metadata');
        this.backupsPath = path.join(this.basePath, 'backups');
    }

    /**
     * Initialize the memory manager
     */
    async initialize(): Promise<void> {
        if (this.isInitialized) return;

        console.log(chalk.blue('🧠 Initializing Tycoon Memory System...'));

        // Create directory structure
        await this.ensureDirectory(this.basePath);
        await this.ensureDirectory(this.memoriesPath);
        await this.ensureDirectory(this.metadataPath);
        await this.ensureDirectory(this.backupsPath);

        // Load existing data
        await this.loadMemories();
        await this.loadContexts();

        // Create default context if none exists
        if (this.contexts.size === 0) {
            await this.createContext({
                name: 'default',
                description: 'Default memory context',
                isActive: true,
                settings: {
                    autoTag: true,
                    defaultCategory: 'general',
                    importanceThreshold: 5
                }
            });
        }

        this.isInitialized = true;
        console.log(chalk.green(`✅ Tycoon Memory System initialized with ${this.memories.size} memories`));
    }

    /**
     * Create a new memory
     */
    async createMemory(input: MemoryInput): Promise<Memory> {
        const now = new Date();
        const id = uuidv4();

        // Auto-generate tags if enabled
        let tags = input.tags || [];
        const context = this.contexts.get(this.currentContext);
        if (context?.settings.autoTag && tags.length === 0) {
            tags = this.generateAutoTags(input.content, 5);
        }

        const memory: Memory = {
            id,
            title: input.title,
            content: input.content,
            tags,
            category: input.category || context?.settings.defaultCategory || 'general',
            importance: input.importance || 5,
            accessCount: 0,
            lastAccessed: now,
            created: now,
            modified: now,
            context: input.context || this.currentContext,
            location: input.location,
            relationships: input.relationships || [],
            summary: this.generateSummary(input.content),
            keyInsights: this.extractKeyPhrases(input.content),
            mood: input.mood,
            environment: input.environment
        };

        // Store memory
        this.memories.set(id, memory);
        await this.saveMemory(memory);

        console.log(chalk.green(`📝 Created memory: ${memory.title}`));
        return memory;
    }

    /**
     * Read a memory by ID
     */
    async readMemory(id: string): Promise<Memory | null> {
        const memory = this.memories.get(id);
        if (!memory) return null;

        // Update access tracking
        memory.accessCount++;
        memory.lastAccessed = new Date();
        this.memories.set(id, memory);
        await this.saveMemory(memory);

        return memory;
    }

    /**
     * Update a memory
     */
    async updateMemory(id: string, updates: Partial<MemoryInput>): Promise<Memory | null> {
        const memory = this.memories.get(id);
        if (!memory) return null;

        const updatedMemory: Memory = {
            ...memory,
            ...updates,
            id, // Ensure ID doesn't change
            modified: new Date(),
            lastAccessed: new Date()
        };

        // Regenerate summary and insights if content changed
        if (updates.content) {
            updatedMemory.summary = this.generateSummary(updatedMemory.content);
            updatedMemory.keyInsights = this.extractKeyPhrases(updatedMemory.content);
        }

        this.memories.set(id, updatedMemory);
        await this.saveMemory(updatedMemory);

        console.log(chalk.yellow(`📝 Updated memory: ${updatedMemory.title}`));
        return updatedMemory;
    }

    /**
     * Delete a memory
     */
    async deleteMemory(id: string): Promise<boolean> {
        const memory = this.memories.get(id);
        if (!memory) return false;

        // Remove from memory map
        this.memories.delete(id);

        // Remove file
        const filePath = path.join(this.memoriesPath, `${id}.json`);
        try {
            await fs.unlink(filePath);
        } catch {
            // File might not exist
        }

        console.log(chalk.red(`🗑️ Deleted memory: ${memory.title}`));
        return true;
    }

    /**
     * Search memories (simplified)
     */
    async searchMemories(query: string): Promise<Memory[]> {
        const searchTerm = query.toLowerCase();
        return Array.from(this.memories.values()).filter(memory =>
            memory.title.toLowerCase().includes(searchTerm) ||
            memory.content.toLowerCase().includes(searchTerm) ||
            memory.tags.some(tag => tag.toLowerCase().includes(searchTerm))
        );
    }

    /**
     * Get all memories
     */
    getAllMemories(): Memory[] {
        return Array.from(this.memories.values());
    }

    /**
     * Create a new context
     */
    async createContext(input: {
        name: string;
        description: string;
        isActive?: boolean;
        settings?: MemoryContext['settings'];
    }): Promise<MemoryContext> {
        const id = uuidv4();
        const context: MemoryContext = {
            id,
            name: input.name,
            description: input.description,
            isActive: input.isActive ?? false,
            memoryIds: [],
            created: new Date(),
            lastUsed: new Date(),
            settings: input.settings || {}
        };

        this.contexts.set(id, context);
        await this.saveContext(context);

        console.log(chalk.blue(`🎯 Created context: ${context.name}`));
        return context;
    }

    // Helper methods
    private async ensureDirectory(dirPath: string): Promise<void> {
        try {
            await fs.access(dirPath);
        } catch {
            await fs.mkdir(dirPath, { recursive: true });
        }
    }

    private async saveMemory(memory: Memory): Promise<void> {
        const filePath = path.join(this.memoriesPath, `${memory.id}.json`);
        await fs.writeFile(filePath, JSON.stringify(memory, null, 2));
    }

    private async saveContext(context: MemoryContext): Promise<void> {
        const filePath = path.join(this.metadataPath, `context-${context.id}.json`);
        await fs.writeFile(filePath, JSON.stringify(context, null, 2));
    }

    private async loadMemories(): Promise<void> {
        try {
            const files = await fs.readdir(this.memoriesPath);
            for (const file of files) {
                if (file.endsWith('.json')) {
                    const filePath = path.join(this.memoriesPath, file);
                    const data = await fs.readFile(filePath, 'utf-8');
                    const memory: Memory = JSON.parse(data);
                    this.memories.set(memory.id, memory);
                }
            }
        } catch {
            // Directory might not exist yet
        }
    }

    private async loadContexts(): Promise<void> {
        try {
            const files = await fs.readdir(this.metadataPath);
            for (const file of files) {
                if (file.startsWith('context-') && file.endsWith('.json')) {
                    const filePath = path.join(this.metadataPath, file);
                    const data = await fs.readFile(filePath, 'utf-8');
                    const context: MemoryContext = JSON.parse(data);
                    this.contexts.set(context.id, context);
                }
            }
        } catch {
            // Directory might not exist yet
        }
    }

    private generateAutoTags(content: string, maxTags: number): string[] {
        // Simple auto-tag generation
        const words = content.toLowerCase().split(/\s+/);
        const commonWords = new Set(['the', 'a', 'an', 'and', 'or', 'but', 'in', 'on', 'at', 'to', 'for', 'of', 'with', 'by']);
        const wordCount = new Map<string, number>();

        words.forEach(word => {
            const cleaned = word.replace(/[^\w]/g, '');
            if (cleaned.length > 3 && !commonWords.has(cleaned)) {
                wordCount.set(cleaned, (wordCount.get(cleaned) || 0) + 1);
            }
        });

        return Array.from(wordCount.entries())
            .sort((a, b) => b[1] - a[1])
            .slice(0, maxTags)
            .map(([word]) => word);
    }

    private generateSummary(content: string): string {
        // Simple summary generation - first sentence or first 100 chars
        const sentences = content.split(/[.!?]+/);
        return sentences[0]?.trim() || content.substring(0, 100) + '...';
    }

    private extractKeyPhrases(content: string): string[] {
        // Simple key phrase extraction
        const words = content.toLowerCase().split(/\s+/);
        const phrases: string[] = [];
        
        for (let i = 0; i < words.length - 1; i++) {
            const phrase = `${words[i]} ${words[i + 1]}`;
            if (phrase.length > 6) {
                phrases.push(phrase);
            }
        }
        
        return phrases.slice(0, 3);
    }
}
