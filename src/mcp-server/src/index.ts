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

    try {
        console.log(chalk.cyan('🚀 Starting Tycoon AI-BIM Server...'));
        
        const server = new TycoonServer();
        
        // Initialize and start the server
        await server.initialize();
        await server.start();
        
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
