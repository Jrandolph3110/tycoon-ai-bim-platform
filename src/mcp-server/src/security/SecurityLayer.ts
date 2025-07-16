/**
 * SecurityLayer - Comprehensive security for log streaming
 * 
 * Implements Phase 2 security enhancements:
 * - WSS (WebSocket Secure) encryption for all log streaming
 * - Per-user access controls and source ACLs
 * - Enhanced PII redaction with compliance features
 * - Audit logging for tracking access and usage
 */

import { readFileSync } from 'fs';
import { createServer as createHttpsServer } from 'https';
import { WebSocketServer } from 'ws';
import { EventEmitter } from 'events';
import chalk from 'chalk';
import * as crypto from 'crypto';

export interface SecurityConfig {
    enableTLS: boolean;
    tlsCertPath?: string;
    tlsKeyPath?: string;
    enableUserAuth: boolean;
    enableAuditLogging: boolean;
    enablePiiRedaction: boolean;
    complianceMode: 'none' | 'gdpr' | 'hipaa' | 'pci' | 'sox';
    sessionTimeoutMinutes: number;
    maxFailedAttempts: number;
    rateLimitRequestsPerMinute: number;
}

export interface UserSession {
    userId: string;
    sessionId: string;
    createdAt: Date;
    lastActivity: Date;
    permissions: Set<string>;
    ipAddress: string;
    userAgent: string;
    isAuthenticated: boolean;
    failedAttempts: number;
}

export interface SecurityAuditEvent {
    eventId: string;
    timestamp: Date;
    userId: string;
    sessionId: string;
    eventType: 'LOGIN' | 'LOGOUT' | 'ACCESS_GRANTED' | 'ACCESS_DENIED' | 'PERMISSION_CHANGED' | 'SECURITY_VIOLATION';
    resource: string;
    ipAddress: string;
    userAgent: string;
    details: any;
    severity: 'info' | 'warning' | 'error' | 'critical';
}

/**
 * User authentication and session management
 */
class AuthenticationManager {
    private sessions: Map<string, UserSession> = new Map();
    private userCredentials: Map<string, { hashedPassword: string, salt: string, permissions: Set<string> }> = new Map();
    private rateLimiter: Map<string, { count: number, resetTime: Date }> = new Map();
    private config: SecurityConfig;

    constructor(config: SecurityConfig) {
        this.config = config;
        this.initializeDefaultUsers();
        this.startSessionCleanup();
    }

    private initializeDefaultUsers(): void {
        // In production, load from secure configuration
        this.createUser('admin', 'admin123', new Set(['tycoon', 'scripts', 'revit_journal']));
        this.createUser('developer', 'dev123', new Set(['tycoon', 'scripts']));
        this.createUser('user', 'user123', new Set(['scripts']));
    }

    private createUser(username: string, password: string, permissions: Set<string>): void {
        const salt = crypto.randomBytes(32).toString('hex');
        const hashedPassword = crypto.pbkdf2Sync(password, salt, 10000, 64, 'sha512').toString('hex');
        
        this.userCredentials.set(username, {
            hashedPassword,
            salt,
            permissions
        });
    }

    async authenticateUser(username: string, password: string, ipAddress: string, userAgent: string): Promise<UserSession | null> {
        // Check rate limiting
        if (!this.checkRateLimit(ipAddress)) {
            throw new Error('Rate limit exceeded. Please try again later.');
        }

        const userCreds = this.userCredentials.get(username);
        if (!userCreds) {
            return null;
        }

        const hashedPassword = crypto.pbkdf2Sync(password, userCreds.salt, 10000, 64, 'sha512').toString('hex');
        
        if (hashedPassword !== userCreds.hashedPassword) {
            this.recordFailedAttempt(ipAddress);
            return null;
        }

        // Create session
        const sessionId = crypto.randomUUID();
        const session: UserSession = {
            userId: username,
            sessionId,
            createdAt: new Date(),
            lastActivity: new Date(),
            permissions: new Set(userCreds.permissions),
            ipAddress,
            userAgent,
            isAuthenticated: true,
            failedAttempts: 0
        };

        this.sessions.set(sessionId, session);
        return session;
    }

    validateSession(sessionId: string): UserSession | null {
        const session = this.sessions.get(sessionId);
        if (!session) return null;

        // Check session timeout
        const timeoutMs = this.config.sessionTimeoutMinutes * 60 * 1000;
        if (Date.now() - session.lastActivity.getTime() > timeoutMs) {
            this.sessions.delete(sessionId);
            return null;
        }

        // Update last activity
        session.lastActivity = new Date();
        return session;
    }

    invalidateSession(sessionId: string): boolean {
        return this.sessions.delete(sessionId);
    }

    private checkRateLimit(ipAddress: string): boolean {
        const now = new Date();
        const limit = this.rateLimiter.get(ipAddress);

        if (!limit || now > limit.resetTime) {
            this.rateLimiter.set(ipAddress, {
                count: 1,
                resetTime: new Date(now.getTime() + 60000) // 1 minute
            });
            return true;
        }

        if (limit.count >= this.config.rateLimitRequestsPerMinute) {
            return false;
        }

        limit.count++;
        return true;
    }

    private recordFailedAttempt(ipAddress: string): void {
        // In production, implement IP blocking after too many failed attempts
        console.warn(chalk.yellow(`⚠️ Failed authentication attempt from ${ipAddress}`));
    }

    private startSessionCleanup(): void {
        setInterval(() => {
            const now = Date.now();
            const timeoutMs = this.config.sessionTimeoutMinutes * 60 * 1000;

            for (const [sessionId, session] of this.sessions) {
                if (now - session.lastActivity.getTime() > timeoutMs) {
                    this.sessions.delete(sessionId);
                }
            }
        }, 60000); // Check every minute
    }

    getActiveSessions(): UserSession[] {
        return Array.from(this.sessions.values());
    }
}

/**
 * Audit logging system
 */
class AuditLogger {
    private auditEvents: SecurityAuditEvent[] = [];
    private eventIdCounter: number = 1;

    logEvent(
        userId: string,
        sessionId: string,
        eventType: SecurityAuditEvent['eventType'],
        resource: string,
        ipAddress: string,
        userAgent: string,
        details: any = {},
        severity: SecurityAuditEvent['severity'] = 'info'
    ): void {
        const event: SecurityAuditEvent = {
            eventId: `audit_${this.eventIdCounter++}`,
            timestamp: new Date(),
            userId,
            sessionId,
            eventType,
            resource,
            ipAddress,
            userAgent,
            details,
            severity
        };

        this.auditEvents.push(event);

        // Keep audit log size manageable
        if (this.auditEvents.length > 10000) {
            this.auditEvents.shift();
        }

        // Log security violations immediately
        if (severity === 'error' || severity === 'critical') {
            console.error(chalk.red(`🚨 SECURITY EVENT: ${eventType} - ${userId} - ${resource}`));
        }
    }

    getAuditEvents(hours: number = 24): SecurityAuditEvent[] {
        const cutoff = new Date(Date.now() - (hours * 60 * 60 * 1000));
        return this.auditEvents.filter(e => e.timestamp >= cutoff);
    }

    getSecurityViolations(): SecurityAuditEvent[] {
        return this.auditEvents.filter(e => e.severity === 'error' || e.severity === 'critical');
    }

    exportAuditLog(): string {
        return JSON.stringify(this.auditEvents, null, 2);
    }
}

/**
 * Compliance-aware PII redaction
 */
class CompliancePIIRedactor {
    private compliancePatterns: Map<string, RegExp[]> = new Map();

    constructor(complianceMode: SecurityConfig['complianceMode']) {
        this.initializeCompliancePatterns(complianceMode);
    }

    private initializeCompliancePatterns(mode: SecurityConfig['complianceMode']): void {
        const basePatterns = [
            /\b\d{3}-\d{2}-\d{4}\b/g,           // SSN
            /\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b/g, // Email
            /\b(?:password|pwd|token|key|secret)[\s:=]+\S+/gi        // Credentials
        ];

        switch (mode) {
            case 'gdpr':
                this.compliancePatterns.set('gdpr', [
                    ...basePatterns,
                    /\b(?:name|firstname|lastname)[\s:=]+\S+/gi,     // Names
                    /\b(?:address|street|city)[\s:=]+.+/gi,         // Addresses
                    /\b(?:phone|mobile|tel)[\s:=]*[\+]?[\d\s\-\(\)]{10,}/g, // Phone
                    /\b(?:ip|address)[\s:=]*\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}/gi // IP
                ]);
                break;

            case 'hipaa':
                this.compliancePatterns.set('hipaa', [
                    ...basePatterns,
                    /\b(?:patient|medical|health)[\s:=]+.+/gi,       // Medical info
                    /\b(?:diagnosis|treatment|medication)[\s:=]+.+/gi, // Health data
                    /\b\d{2}\/\d{2}\/\d{4}\b/g,                     // Dates (potential DOB)
                    /\b(?:mrn|medical[_-]?record)[\s:=]+\S+/gi      // Medical record numbers
                ]);
                break;

            case 'pci':
                this.compliancePatterns.set('pci', [
                    ...basePatterns,
                    /\b\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b/g,  // Credit cards
                    /\b\d{3,4}\b/g,                                 // CVV codes
                    /\b(?:card|payment|billing)[\s:=]+.+/gi        // Payment info
                ]);
                break;

            case 'sox':
                this.compliancePatterns.set('sox', [
                    ...basePatterns,
                    /\b(?:financial|revenue|profit|loss)[\s:=]+.+/gi, // Financial data
                    /\b\$[\d,]+\.?\d*\b/g,                          // Currency amounts
                    /\b(?:account|transaction)[\s:=]+\S+/gi         // Account info
                ]);
                break;

            default:
                this.compliancePatterns.set('default', basePatterns);
        }
    }

    redactLogEntry(logEntry: string, complianceMode: string = 'default'): string {
        const patterns = this.compliancePatterns.get(complianceMode) || 
                        this.compliancePatterns.get('default')!;
        
        let sanitized = logEntry;
        patterns.forEach(pattern => {
            sanitized = sanitized.replace(pattern, '[REDACTED]');
        });

        return sanitized;
    }
}

/**
 * Main security layer
 */
export class SecurityLayer extends EventEmitter {
    private config: SecurityConfig;
    private authManager: AuthenticationManager;
    private auditLogger: AuditLogger;
    private piiRedactor: CompliancePIIRedactor;
    private httpsServer: any = null;

    constructor(config: SecurityConfig) {
        super();
        this.config = config;
        this.authManager = new AuthenticationManager(config);
        this.auditLogger = new AuditLogger();
        this.piiRedactor = new CompliancePIIRedactor(config.complianceMode);
    }

    /**
     * Create secure WebSocket server with TLS
     */
    createSecureWebSocketServer(port: number): WebSocketServer {
        if (this.config.enableTLS && this.config.tlsCertPath && this.config.tlsKeyPath) {
            console.log(chalk.green('🔒 Creating secure WebSocket server with TLS...'));
            
            const serverOptions = {
                cert: readFileSync(this.config.tlsCertPath),
                key: readFileSync(this.config.tlsKeyPath)
            };

            this.httpsServer = createHttpsServer(serverOptions);
            
            const wss = new WebSocketServer({ 
                server: this.httpsServer,
                perMessageDeflate: {
                    threshold: 1024,
                    concurrencyLimit: 10
                }
            });

            this.httpsServer.listen(port, () => {
                console.log(chalk.green(`🔒 Secure WebSocket server listening on port ${port}`));
            });

            return wss;
        } else {
            console.log(chalk.yellow('⚠️ Creating insecure WebSocket server (TLS disabled)'));
            return new WebSocketServer({ port });
        }
    }

    /**
     * Authenticate user and create session
     */
    async authenticateUser(username: string, password: string, ipAddress: string, userAgent: string): Promise<UserSession | null> {
        try {
            const session = await this.authManager.authenticateUser(username, password, ipAddress, userAgent);
            
            if (session) {
                this.auditLogger.logEvent(
                    username, session.sessionId, 'LOGIN', 'authentication',
                    ipAddress, userAgent, { success: true }, 'info'
                );
                console.log(chalk.green(`✅ User authenticated: ${username}`));
            } else {
                this.auditLogger.logEvent(
                    username, 'none', 'LOGIN', 'authentication',
                    ipAddress, userAgent, { success: false }, 'warning'
                );
                console.warn(chalk.yellow(`⚠️ Authentication failed: ${username}`));
            }

            return session;
        } catch (error: any) {
            this.auditLogger.logEvent(
                username, 'none', 'LOGIN', 'authentication',
                ipAddress, userAgent, { error: error.message }, 'error'
            );
            throw error;
        }
    }

    /**
     * Validate session and check permissions
     */
    validateAccess(sessionId: string, resource: string): { valid: boolean; session?: UserSession } {
        const session = this.authManager.validateSession(sessionId);
        
        if (!session) {
            return { valid: false };
        }

        const hasPermission = session.permissions.has(resource);
        
        this.auditLogger.logEvent(
            session.userId, sessionId, hasPermission ? 'ACCESS_GRANTED' : 'ACCESS_DENIED',
            resource, session.ipAddress, session.userAgent,
            { resource, hasPermission }, hasPermission ? 'info' : 'warning'
        );

        return { valid: hasPermission, session };
    }

    /**
     * Process log entry with security filtering
     */
    processLogEntry(logEntry: string, sessionId: string): string {
        if (!this.config.enablePiiRedaction) {
            return logEntry;
        }

        const session = this.authManager.validateSession(sessionId);
        if (!session) {
            throw new Error('Invalid session for log processing');
        }

        return this.piiRedactor.redactLogEntry(logEntry, this.config.complianceMode);
    }

    /**
     * Get security metrics
     */
    getSecurityMetrics(): any {
        const activeSessions = this.authManager.getActiveSessions();
        const auditEvents = this.auditLogger.getAuditEvents(1); // Last hour
        const violations = this.auditLogger.getSecurityViolations();

        return {
            activeSessions: activeSessions.length,
            recentAuditEvents: auditEvents.length,
            securityViolations: violations.length,
            tlsEnabled: this.config.enableTLS,
            complianceMode: this.config.complianceMode,
            sessionsByUser: this.groupSessionsByUser(activeSessions),
            recentViolations: violations.slice(-10) // Last 10 violations
        };
    }

    private groupSessionsByUser(sessions: UserSession[]): Record<string, number> {
        const grouped: Record<string, number> = {};
        sessions.forEach(session => {
            grouped[session.userId] = (grouped[session.userId] || 0) + 1;
        });
        return grouped;
    }

    /**
     * Export audit log for compliance
     */
    exportAuditLog(): string {
        return this.auditLogger.exportAuditLog();
    }

    /**
     * Logout user and invalidate session
     */
    logout(sessionId: string): boolean {
        const session = this.authManager.validateSession(sessionId);
        if (session) {
            this.auditLogger.logEvent(
                session.userId, sessionId, 'LOGOUT', 'authentication',
                session.ipAddress, session.userAgent, {}, 'info'
            );
        }
        
        return this.authManager.invalidateSession(sessionId);
    }

    /**
     * Shutdown security layer
     */
    shutdown(): void {
        if (this.httpsServer) {
            this.httpsServer.close();
        }
        console.log(chalk.blue('🔒 Security layer shutdown complete'));
    }
}
