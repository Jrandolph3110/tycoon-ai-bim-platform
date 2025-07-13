#!/usr/bin/env node
/**
 * Tycoon AI-BIM MCP Server
 * 
 * Revolutionary AI-powered construction automation platform
 * Built on Temporal Neural Nexus foundation with live Revit integration
 * 
 * Features:
 * - Real-time AI-Revit communication
 * - FLC steel framing workflows
 * - Live selection context
 * - Script generation with immediate feedback
 * - Advanced memory and learning system
 * 
 * Created by: F.L. Crane & Sons Development Team
 * Version: 1.0.0
 */

import chalk from 'chalk';
import { TycoonServer } from './TycoonServer.js';
import * as fs from 'fs';
import * as path from 'path';
import { execSync } from 'child_process';

// Global server instance for cleanup
let globalServer: TycoonServer | null = null;
let isShuttingDown = false;

// PID file management for singleton enforcement
const APPDATA_DIR = process.env.APPDATA || path.join(require('os').homedir(), '.config');
const PID_DIR = path.join(APPDATA_DIR, 'Tycoon');
const PID_FILE_PATH = path.join(PID_DIR, 'mcp-server.pid');
const GRACEFUL_SHUTDOWN_TIMEOUT = 2000; // ms

// Enhanced shutdown handler
async function gracefulShutdown(signal: string): Promise<void> {
    if (isShuttingDown) {
        console.log(chalk.yellow(`⚠️ Already shutting down, ignoring ${signal}`));
        return;
    }

    isShuttingDown = true;
    console.log(chalk.yellow(`\n🛑 Received ${signal}, shutting down Tycoon AI-BIM Server...`));

    try {
        // 1. Shutdown the server gracefully
        if (globalServer) {
            await globalServer.shutdown();
        }

        // 2. Clean up PID file
        try {
            if (fs.existsSync(PID_FILE_PATH)) {
                const pidInFile = parseInt(fs.readFileSync(PID_FILE_PATH, 'utf8'), 10);
                if (pidInFile === process.pid) {
                    fs.unlinkSync(PID_FILE_PATH);
                    console.log(chalk.gray('🗑️ PID file cleaned up'));
                }
            }
        } catch (pidError) {
            console.error(chalk.yellow('⚠️ Error cleaning up PID file:'), pidError);
        }

        console.log(chalk.green('✅ Graceful shutdown completed'));
        process.exit(0);
    } catch (error) {
        console.error(chalk.red('❌ Error during shutdown:'), error);

        // Try to clean up PID file even on error
        try {
            if (fs.existsSync(PID_FILE_PATH)) {
                const pidInFile = parseInt(fs.readFileSync(PID_FILE_PATH, 'utf8'), 10);
                if (pidInFile === process.pid) {
                    fs.unlinkSync(PID_FILE_PATH);
                }
            }
        } catch (pidError) {
            // Suppress PID cleanup errors during forced shutdown
        }

        // Force exit after 2 seconds if graceful shutdown fails
        setTimeout(() => {
            console.log(chalk.red('🚨 Forcing process exit...'));
            process.exit(1);
        }, 2000);
    }
}

// Enhanced signal handling
process.on('SIGINT', () => gracefulShutdown('SIGINT'));
process.on('SIGTERM', () => gracefulShutdown('SIGTERM'));
process.on('SIGQUIT', () => gracefulShutdown('SIGQUIT'));

// Handle Windows-specific signals and console events
if (process.platform === 'win32') {
    process.on('SIGBREAK', () => gracefulShutdown('SIGBREAK'));

    // Windows-specific: Handle console close events
    // This catches when VS Code closes the console window
    process.on('beforeExit', () => {
        console.log(chalk.yellow('🪟 Process beforeExit event - VS Code may be closing'));
        gracefulShutdown('beforeExit');
    });

    // Handle process exit events
    process.on('exit', (code) => {
        console.log(chalk.gray(`🚪 Process exiting with code: ${code}`));
        // Clean up PID file on any exit
        try {
            if (fs.existsSync(PID_FILE_PATH)) {
                const pidInFile = parseInt(fs.readFileSync(PID_FILE_PATH, 'utf8'), 10);
                if (pidInFile === process.pid) {
                    fs.unlinkSync(PID_FILE_PATH);
                }
            }
        } catch (e) {
            // Ignore cleanup errors during exit
        }
    });

    console.log(chalk.gray('🪟 Windows-specific shutdown handlers registered'));
}

/**
 * Waits for a process to exit by polling.
 */
const waitForProcessExit = (pid: number, timeoutMs: number): Promise<void> => {
    return new Promise((resolve, reject) => {
        const startTime = Date.now();
        const interval = setInterval(() => {
            try {
                process.kill(pid, 0); // Throws an error if the process doesn't exist.
                if (Date.now() - startTime > timeoutMs) {
                    clearInterval(interval);
                    reject(new Error(`Process ${pid} did not terminate within ${timeoutMs}ms.`));
                }
            } catch (e) {
                // Error (ESRCH) means the process is gone.
                clearInterval(interval);
                resolve();
            }
        }, 100); // Poll every 100ms.
    });
};

/**
 * Monitor parent process (VS Code) and shutdown if it exits
 */
const setupParentProcessMonitoring = () => {
    if (process.platform !== 'win32') {
        return; // Only implement for Windows where the issue occurs
    }

    try {
        const parentPid = process.ppid;
        if (!parentPid) {
            console.log(chalk.yellow('⚠️ No parent process ID available'));
            return;
        }

        console.log(chalk.gray(`👁️ Monitoring parent process (VS Code): ${parentPid}`));

        // Check parent process every 2 seconds
        const monitorInterval = setInterval(() => {
            try {
                // Check if parent process still exists
                process.kill(parentPid, 0);
                // If we get here, parent is still alive
            } catch (error) {
                // Parent process is gone (VS Code closed)
                console.log(chalk.yellow(`🚪 Parent process ${parentPid} has exited - VS Code closed`));
                clearInterval(monitorInterval);
                gracefulShutdown('Parent process exit');
            }
        }, 2000);

        // Clean up monitor on shutdown
        process.on('exit', () => {
            clearInterval(monitorInterval);
        });

    } catch (error) {
        console.error(chalk.yellow('⚠️ Failed to set up parent process monitoring:'), error);
    }
};

/**
 * Finds and terminates a previous server instance if it exists.
 */
async function cleanupOldInstance(): Promise<void> {
    if (!fs.existsSync(PID_FILE_PATH)) {
        return;
    }

    const pidStr = fs.readFileSync(PID_FILE_PATH, 'utf8');
    const pid = parseInt(pidStr, 10);

    if (isNaN(pid)) {
        console.log(chalk.yellow('[MCP-Startup] Stale PID file is invalid. Deleting.'));
        fs.unlinkSync(PID_FILE_PATH);
        return;
    }

    try {
        process.kill(pid, 0); // Check if process exists.
        console.log(chalk.yellow(`[MCP-Startup] Found running instance with PID ${pid}. Attempting graceful shutdown...`));
        process.kill(pid, 'SIGTERM');

        await waitForProcessExit(pid, GRACEFUL_SHUTDOWN_TIMEOUT);
        console.log(chalk.green(`[MCP-Startup] Old instance ${pid} terminated gracefully.`));
    } catch (e: any) {
        if (e.code === 'ESRCH') {
            console.log(chalk.gray(`[MCP-Startup] Found stale PID for non-running process ${pid}.`));
        } else if (e.message.includes('did not terminate')) {
            console.warn(chalk.red(`[MCP-Startup] Old instance ${pid} did not terminate gracefully. Force killing...`));
            try {
                // Force kill, platform-specific.
                if (process.platform === 'win32') {
                    execSync(`taskkill /PID ${pid} /F`);
                } else {
                    process.kill(pid, 'SIGKILL');
                }
                console.log(chalk.green(`[MCP-Startup] Old instance ${pid} force-killed.`));
            } catch (killErr) {
                console.error(chalk.red(`[MCP-Startup] Failed to force-kill process ${pid}:`), killErr);
            }
        } else {
            console.error(chalk.red(`[MCP-Startup] Error during cleanup of PID ${pid}:`), e);
        }
    } finally {
        // Now that we've handled the process, we can safely remove the file.
        if (fs.existsSync(PID_FILE_PATH)) {
            fs.unlinkSync(PID_FILE_PATH);
        }
    }
}

/**
 * Writes the current process ID to the PID file.
 */
function registerCurrentInstance(): void {
    try {
        fs.mkdirSync(PID_DIR, { recursive: true });
        fs.writeFileSync(PID_FILE_PATH, process.pid.toString(), { encoding: 'utf8' });

        process.on('exit', () => {
            try {
                const pidInFile = parseInt(fs.readFileSync(PID_FILE_PATH, 'utf8'), 10);
                if (fs.existsSync(PID_FILE_PATH) && pidInFile === process.pid) {
                    fs.unlinkSync(PID_FILE_PATH);
                }
            } catch (e) { /* suppress errors on exit */ }
        });
    } catch (err) {
        console.error(chalk.red('[MCP-Startup] CRITICAL: Failed to write PID file. Server cannot start.'), err);
        process.exit(1);
    }
}

async function main() {
    console.log(chalk.blue.bold(`
╔══════════════════════════════════════════════════════════════╗
║                    🎯 TYCOON AI-BIM PLATFORM                 ║
║              Revolutionary Construction Automation            ║
║                                                              ║
║  🧠 Temporal Neural Nexus Memory System                     ║
║  🔗 Live Revit Integration                                   ║
║  🏗️  FLC Steel Framing Workflows                            ║
║  🤖 AI-Powered Script Generation                            ║
║                                                              ║
║  Built by F.L. Crane & Sons Development Team                ║
╚══════════════════════════════════════════════════════════════╝
    `));

    // Set process title for easy identification in Task Manager
    process.title = 'Tycoon-AI-BIM-MCP-Server';

    try {
        // Step 1: Clean up any existing instances
        await cleanupOldInstance();

        // Step 2: Register this instance
        registerCurrentInstance();

        console.log(chalk.cyan('🚀 Starting Tycoon AI-BIM Server...'));
        console.log(chalk.gray(`📋 Process title: ${process.title}`));
        console.log(chalk.gray(`📋 Process PID: ${process.pid}`));

        globalServer = new TycoonServer();

        // Initialize and start the server
        await globalServer.initialize();
        await globalServer.start();

        // Start parent process monitoring (VS Code shutdown detection)
        setupParentProcessMonitoring();

        console.log(chalk.green.bold('✅ Tycoon AI-BIM Server is running!'));
        console.log(chalk.yellow('📋 Ready for AI-Revit integration'));
        console.log(chalk.gray('   Press Ctrl+C to stop the server'));
        
    } catch (error) {
        console.error(chalk.red.bold('❌ Failed to start Tycoon server:'));
        console.error(chalk.red(error instanceof Error ? error.message : String(error)));
        process.exit(1);
    }
}

// Handle unhandled promise rejections
process.on('unhandledRejection', (reason, promise) => {
    console.error(chalk.red('Unhandled Rejection at:'), promise, chalk.red('reason:'), reason);
    process.exit(1);
});

// Handle uncaught exceptions
process.on('uncaughtException', (error) => {
    console.error(chalk.red('Uncaught Exception:'), error);
    process.exit(1);
});

// Start the server
main().catch((error) => {
    console.error(chalk.red('Fatal error:'), error);
    process.exit(1);
});
