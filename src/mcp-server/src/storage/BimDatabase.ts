import * as sqlite3 from 'sqlite3';
import { Database, open } from 'sqlite';
import * as path from 'path';
import * as fs from 'fs';

/**
 * 🚀 TYCOON BIM DATABASE - Structured storage for BIM data
 * 
 * Features:
 * - SQLite for structured data
 * - JSON storage for complex objects
 * - Element versioning
 * - Relationship mapping
 * - Efficient indexing
 * - Historical tracking
 */
export class BimDatabase {
    private db: Database | null = null;
    private dbPath: string;
    private jsonStorePath: string;

    constructor(vaultPath: string) {
        this.dbPath = path.join(vaultPath, 'Database', 'tycoon_bim.db');
        this.jsonStorePath = path.join(vaultPath, 'Database', 'json_store');
        
        // Ensure directories exist
        fs.mkdirSync(path.dirname(this.dbPath), { recursive: true });
        fs.mkdirSync(this.jsonStorePath, { recursive: true });
    }

    /**
     * Initialize database with schema
     */
    public async initialize(): Promise<void> {
        this.db = await open({
            filename: this.dbPath,
            driver: sqlite3.Database
        });

        await this.createSchema();
        console.log(`🗄️ BIM Database initialized: ${this.dbPath}`);
    }

    /**
     * Create database schema
     */
    private async createSchema(): Promise<void> {
        if (!this.db) throw new Error('Database not initialized');

        // Sessions table
        await this.db.exec(`
            CREATE TABLE IF NOT EXISTS sessions (
                session_id TEXT PRIMARY KEY,
                start_time DATETIME,
                end_time DATETIME,
                document_title TEXT,
                view_name TEXT,
                total_elements INTEGER,
                processed_elements INTEGER,
                processing_tier TEXT,
                revit_version TEXT,
                tycoon_version TEXT,
                status TEXT,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP
            )
        `);

        // Elements table
        await this.db.exec(`
            CREATE TABLE IF NOT EXISTS elements (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT,
                element_id TEXT,
                revit_element_id INTEGER,
                category TEXT,
                family_name TEXT,
                type_name TEXT,
                level_name TEXT,
                workset TEXT,
                phase_created TEXT,
                location_x REAL,
                location_y REAL,
                location_z REAL,
                bbox_min_x REAL,
                bbox_min_y REAL,
                bbox_min_z REAL,
                bbox_max_x REAL,
                bbox_max_y REAL,
                bbox_max_z REAL,
                volume REAL,
                area REAL,
                length REAL,
                json_data_path TEXT,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (session_id) REFERENCES sessions (session_id)
            )
        `);

        // Parameters table
        await this.db.exec(`
            CREATE TABLE IF NOT EXISTS parameters (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                element_id INTEGER,
                parameter_name TEXT,
                parameter_value TEXT,
                parameter_type TEXT,
                is_shared BOOLEAN,
                is_read_only BOOLEAN,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (element_id) REFERENCES elements (id)
            )
        `);

        // Relationships table
        await this.db.exec(`
            CREATE TABLE IF NOT EXISTS relationships (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                parent_element_id INTEGER,
                child_element_id INTEGER,
                relationship_type TEXT,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (parent_element_id) REFERENCES elements (id),
                FOREIGN KEY (child_element_id) REFERENCES elements (id)
            )
        `);

        // FLC specific tables
        await this.db.exec(`
            CREATE TABLE IF NOT EXISTS flc_panels (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                element_id INTEGER,
                bimsf_container TEXT,
                bimsf_label TEXT,
                bimsf_id TEXT,
                master_id TEXT,
                panel_type TEXT,
                stud_spacing REAL,
                panel_width REAL,
                panel_height REAL,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (element_id) REFERENCES elements (id)
            )
        `);

        await this.db.exec(`
            CREATE TABLE IF NOT EXISTS flc_framing (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                element_id INTEGER,
                framing_type TEXT,
                member_size TEXT,
                cut_length REAL,
                structural_usage TEXT,
                cross_section_rotation REAL,
                start_level_offset REAL,
                end_level_offset REAL,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (element_id) REFERENCES elements (id)
            )
        `);

        // Create indexes for performance
        await this.createIndexes();
    }

    /**
     * Create database indexes
     */
    private async createIndexes(): Promise<void> {
        if (!this.db) return;

        const indexes = [
            'CREATE INDEX IF NOT EXISTS idx_elements_session ON elements (session_id)',
            'CREATE INDEX IF NOT EXISTS idx_elements_category ON elements (category)',
            'CREATE INDEX IF NOT EXISTS idx_elements_revit_id ON elements (revit_element_id)',
            'CREATE INDEX IF NOT EXISTS idx_parameters_element ON parameters (element_id)',
            'CREATE INDEX IF NOT EXISTS idx_parameters_name ON parameters (parameter_name)',
            'CREATE INDEX IF NOT EXISTS idx_relationships_parent ON relationships (parent_element_id)',
            'CREATE INDEX IF NOT EXISTS idx_relationships_child ON relationships (child_element_id)',
            'CREATE INDEX IF NOT EXISTS idx_flc_panels_container ON flc_panels (bimsf_container)',
            'CREATE INDEX IF NOT EXISTS idx_flc_framing_type ON flc_framing (framing_type)'
        ];

        for (const indexSql of indexes) {
            await this.db.exec(indexSql);
        }
    }

    /**
     * Store session data
     */
    public async storeSession(sessionData: any): Promise<void> {
        if (!this.db) throw new Error('Database not initialized');

        await this.db.run(`
            INSERT OR REPLACE INTO sessions (
                session_id, start_time, end_time, document_title, view_name,
                total_elements, processed_elements, processing_tier,
                revit_version, tycoon_version, status
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        `, [
            sessionData.sessionId,
            sessionData.startTime,
            sessionData.endTime,
            sessionData.documentTitle,
            sessionData.viewName,
            sessionData.totalElements,
            sessionData.processedElements,
            sessionData.processingTier,
            sessionData.revitVersion,
            sessionData.tycoonVersion,
            sessionData.status
        ]);
    }

    /**
     * Store element data
     */
    public async storeElement(sessionId: string, elementData: any): Promise<number> {
        if (!this.db) throw new Error('Database not initialized');

        // Store complex JSON data separately
        const jsonDataPath = await this.storeJsonData(sessionId, elementData.id, elementData);

        const result = await this.db.run(`
            INSERT INTO elements (
                session_id, element_id, revit_element_id, category, family_name, type_name,
                level_name, workset, phase_created, location_x, location_y, location_z,
                bbox_min_x, bbox_min_y, bbox_min_z, bbox_max_x, bbox_max_y, bbox_max_z,
                volume, area, length, json_data_path
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        `, [
            sessionId,
            elementData.id,
            elementData.elementId,
            elementData.category,
            elementData.familyName,
            elementData.typeName,
            elementData.parameters?.['Level'],
            elementData.parameters?.['Workset'],
            elementData.parameters?.['Phase Created'],
            elementData.geometry?.location?.x,
            elementData.geometry?.location?.y,
            elementData.geometry?.location?.z,
            elementData.geometry?.boundingBox?.min?.x,
            elementData.geometry?.boundingBox?.min?.y,
            elementData.geometry?.boundingBox?.min?.z,
            elementData.geometry?.boundingBox?.max?.x,
            elementData.geometry?.boundingBox?.max?.y,
            elementData.geometry?.boundingBox?.max?.z,
            elementData.parameters?.['Volume'],
            elementData.parameters?.['Area'],
            elementData.parameters?.['Length'],
            jsonDataPath
        ]);

        const elementRowId = result.lastID as number;

        // Store parameters
        if (elementData.parameters) {
            await this.storeParameters(elementRowId, elementData.parameters);
        }

        // Store FLC-specific data
        if (elementData.category === 'Structural Framing') {
            await this.storeFlcFraming(elementRowId, elementData);
        }

        return elementRowId;
    }

    /**
     * Store parameters for an element
     */
    private async storeParameters(elementRowId: number, parameters: any): Promise<void> {
        if (!this.db) return;

        for (const [name, value] of Object.entries(parameters)) {
            await this.db.run(`
                INSERT INTO parameters (element_id, parameter_name, parameter_value, parameter_type)
                VALUES (?, ?, ?, ?)
            `, [elementRowId, name, String(value), typeof value]);
        }
    }

    /**
     * Store FLC framing data
     */
    private async storeFlcFraming(elementRowId: number, elementData: any): Promise<void> {
        if (!this.db) return;

        const params = elementData.parameters || {};
        
        await this.db.run(`
            INSERT INTO flc_framing (
                element_id, framing_type, member_size, cut_length, structural_usage,
                cross_section_rotation, start_level_offset, end_level_offset
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
        `, [
            elementRowId,
            params['BIMSF_Description'] || 'Unknown',
            params['Type'] || 'Unknown',
            params['Cut Length'],
            params['Structural Usage'],
            params['Cross-Section Rotation'],
            params['Start Level Offset'],
            params['End Level Offset']
        ]);
    }

    /**
     * Store JSON data to file system
     */
    private async storeJsonData(sessionId: string, elementId: string, data: any): Promise<string> {
        const sessionDir = path.join(this.jsonStorePath, sessionId);
        fs.mkdirSync(sessionDir, { recursive: true });
        
        const fileName = `${elementId}.json`;
        const filePath = path.join(sessionDir, fileName);
        
        fs.writeFileSync(filePath, JSON.stringify(data, null, 2));
        
        return path.relative(this.jsonStorePath, filePath);
    }

    /**
     * Query elements by criteria
     */
    public async queryElements(criteria: ElementQueryCriteria): Promise<any[]> {
        if (!this.db) throw new Error('Database not initialized');

        let sql = 'SELECT * FROM elements WHERE 1=1';
        const params: any[] = [];

        if (criteria.sessionId) {
            sql += ' AND session_id = ?';
            params.push(criteria.sessionId);
        }

        if (criteria.category) {
            sql += ' AND category = ?';
            params.push(criteria.category);
        }

        if (criteria.familyName) {
            sql += ' AND family_name = ?';
            params.push(criteria.familyName);
        }

        if (criteria.limit) {
            sql += ' LIMIT ?';
            params.push(criteria.limit);
        }

        return await this.db.all(sql, params);
    }

    /**
     * Get session statistics
     */
    public async getSessionStats(sessionId: string): Promise<any> {
        if (!this.db) throw new Error('Database not initialized');

        const stats = await this.db.get(`
            SELECT 
                COUNT(*) as total_elements,
                COUNT(DISTINCT category) as unique_categories,
                COUNT(DISTINCT family_name) as unique_families,
                MIN(created_at) as first_element,
                MAX(created_at) as last_element
            FROM elements 
            WHERE session_id = ?
        `, [sessionId]);

        const categories = await this.db.all(`
            SELECT category, COUNT(*) as count
            FROM elements 
            WHERE session_id = ?
            GROUP BY category
            ORDER BY count DESC
        `, [sessionId]);

        return { ...stats, categories };
    }

    /**
     * Close database connection
     */
    public async close(): Promise<void> {
        if (this.db) {
            await this.db.close();
            this.db = null;
        }
    }
}

// Type definitions
export interface ElementQueryCriteria {
    sessionId?: string;
    category?: string;
    familyName?: string;
    limit?: number;
}
