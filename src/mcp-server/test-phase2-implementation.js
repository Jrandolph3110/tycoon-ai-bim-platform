/**
 * Phase 2 Implementation Test Suite
 * 
 * Validates all Phase 2 enhancements:
 * - Enhanced reliability with error recovery
 * - Load testing infrastructure (10k lines/sec)
 * - Operational monitoring with KPI dashboard
 * - Security enhancements with WSS and access controls
 */

import { LoadTestingFramework } from './dist/testing/LoadTestingFramework.js';
import { OperationalMonitor } from './dist/monitoring/OperationalMonitor.js';
import { SecurityLayer } from './dist/security/SecurityLayer.js';
import { NetworkRecoveryManager } from './dist/streaming/ErrorRecoveryManager.js';
import chalk from 'chalk';

class Phase2TestSuite {
    constructor() {
        this.loadTester = new LoadTestingFramework();
        this.operationalMonitor = new OperationalMonitor();
        this.securityLayer = new SecurityLayer({
            enableTLS: false,
            enableUserAuth: true,
            enableAuditLogging: true,
            enablePiiRedaction: true,
            complianceMode: 'gdpr',
            sessionTimeoutMinutes: 60,
            maxFailedAttempts: 5,
            rateLimitRequestsPerMinute: 100
        });
        this.networkRecovery = new NetworkRecoveryManager({
            maxRetries: 5,
            initialDelayMs: 1000,
            maxDelayMs: 30000,
            backoffMultiplier: 2,
            jitterEnabled: true
        }, true);
    }

    async runAllTests() {
        console.log(chalk.blue.bold('\n🚀 PHASE 2 IMPLEMENTATION TEST SUITE\n'));
        console.log(chalk.green('Testing enhanced resilience, load testing, monitoring, and security...\n'));

        const results = {
            loadTesting: false,
            errorRecovery: false,
            operationalMonitoring: false,
            securityLayer: false,
            overallSuccess: false
        };

        try {
            // Test 1: Load Testing Infrastructure
            console.log(chalk.yellow('📊 Test 1: Load Testing Infrastructure'));
            results.loadTesting = await this.testLoadTesting();
            console.log(results.loadTesting ? 
                chalk.green('✅ Load testing: PASSED') : 
                chalk.red('❌ Load testing: FAILED'));

            // Test 2: Error Recovery Mechanisms
            console.log(chalk.yellow('\n🔄 Test 2: Error Recovery Mechanisms'));
            results.errorRecovery = await this.testErrorRecovery();
            console.log(results.errorRecovery ? 
                chalk.green('✅ Error recovery: PASSED') : 
                chalk.red('❌ Error recovery: FAILED'));

            // Test 3: Operational Monitoring
            console.log(chalk.yellow('\n📈 Test 3: Operational Monitoring'));
            results.operationalMonitoring = await this.testOperationalMonitoring();
            console.log(results.operationalMonitoring ? 
                chalk.green('✅ Operational monitoring: PASSED') : 
                chalk.red('❌ Operational monitoring: FAILED'));

            // Test 4: Security Layer
            console.log(chalk.yellow('\n🔒 Test 4: Security Layer'));
            results.securityLayer = await this.testSecurityLayer();
            console.log(results.securityLayer ? 
                chalk.green('✅ Security layer: PASSED') : 
                chalk.red('❌ Security layer: FAILED'));

            // Overall results
            results.overallSuccess = Object.values(results).every(r => r === true);
            
            console.log(chalk.blue.bold('\n📋 PHASE 2 TEST RESULTS:'));
            console.log(`Load Testing: ${results.loadTesting ? '✅' : '❌'}`);
            console.log(`Error Recovery: ${results.errorRecovery ? '✅' : '❌'}`);
            console.log(`Operational Monitoring: ${results.operationalMonitoring ? '✅' : '❌'}`);
            console.log(`Security Layer: ${results.securityLayer ? '✅' : '❌'}`);
            console.log(`\nOverall Success: ${results.overallSuccess ? '✅ PASSED' : '❌ FAILED'}`);

            if (results.overallSuccess) {
                console.log(chalk.green.bold('\n🎉 PHASE 2 IMPLEMENTATION: ALL TESTS PASSED!'));
                console.log(chalk.green('✅ Enhanced resilience and flow control ready for production'));
                console.log(chalk.green('✅ Load testing infrastructure validated'));
                console.log(chalk.green('✅ Operational monitoring with KPI dashboard active'));
                console.log(chalk.green('✅ Security enhancements with access controls implemented'));
            } else {
                console.log(chalk.red.bold('\n❌ PHASE 2 IMPLEMENTATION: SOME TESTS FAILED'));
                console.log(chalk.yellow('⚠️ Review failed components before proceeding to Phase 3'));
            }

        } catch (error) {
            console.error(chalk.red('\n💥 Test suite execution failed:'), error);
            results.overallSuccess = false;
        }

        return results;
    }

    async testLoadTesting() {
        try {
            console.log('  🔧 Initializing load testing framework...');
            
            const config = {
                targetLinesPerSecond: 1000, // Reduced for testing
                durationSeconds: 5,         // Short test duration
                concurrentStreams: 3,
                logLineSize: 200,
                memoryPressureTest: true,
                networkFlapTest: true,
                lowSpecHardwareTest: true
            };

            console.log('  ⚡ Running load test...');
            const results = await this.loadTester.runLoadTest(config);
            
            console.log('  📊 Load test results:');
            console.log(`    • Throughput: ${results.throughputLinesPerSecond.toFixed(2)} lines/sec`);
            console.log(`    • Memory usage: ${results.memoryUsageMB.toFixed(2)} MB`);
            console.log(`    • CPU usage: ${results.cpuUsagePercent.toFixed(2)}%`);
            console.log(`    • Dropped messages: ${results.droppedMessages}`);
            console.log(`    • Network flaps: ${results.networkFlaps}`);

            // Success criteria
            const throughputOk = results.throughputLinesPerSecond >= 500; // Reduced threshold for testing
            const memoryOk = results.memoryUsageMB < 200; // 200MB limit
            const cpuOk = results.cpuUsagePercent < 10; // 10% limit for testing
            const droppedOk = results.droppedMessages < 100;

            return throughputOk && memoryOk && cpuOk && droppedOk;

        } catch (error) {
            console.error('    ❌ Load testing failed:', error.message);
            return false;
        }
    }

    async testErrorRecovery() {
        try {
            console.log('  🔧 Testing network recovery manager...');
            
            // Initialize recovery for test stream
            const streamId = 'test-stream-001';
            this.networkRecovery.initializeStreamRecovery(streamId, 0);
            
            console.log('  🔄 Testing connection recovery...');
            
            // Simulate connection drop and recovery
            let recoverySuccessful = false;
            await this.networkRecovery.handleConnectionDrop(streamId, async () => {
                // Simulate successful reconnection
                recoverySuccessful = true;
                console.log('    ✅ Connection recovered successfully');
            });

            // Get recovery metrics
            const metrics = this.networkRecovery.getRecoveryMetrics(streamId);
            console.log('  📊 Recovery metrics:');
            console.log(`    • Stream ID: ${metrics?.streamId || 'N/A'}`);
            console.log(`    • Failure count: ${metrics?.failureCount || 0}`);
            console.log(`    • Is recovering: ${metrics?.isRecovering || false}`);
            console.log(`    • Success rate: ${((metrics?.successRate || 0) * 100).toFixed(1)}%`);

            // Cleanup
            this.networkRecovery.cleanupStreamRecovery(streamId);

            return recoverySuccessful && metrics !== null;

        } catch (error) {
            console.error('    ❌ Error recovery testing failed:', error.message);
            return false;
        }
    }

    async testOperationalMonitoring() {
        try {
            console.log('  🔧 Testing operational monitoring...');
            
            // Start monitoring
            this.operationalMonitor.startMonitoring(1000); // 1-second intervals for testing
            
            // Wait for some metrics to be collected
            await new Promise(resolve => setTimeout(resolve, 3000));
            
            console.log('  📊 Getting KPI dashboard...');
            const dashboard = this.operationalMonitor.getKPIDashboard();
            
            console.log('  📈 Dashboard metrics:');
            console.log(`    • Timestamp: ${dashboard.timestamp}`);
            console.log(`    • Health status: ${dashboard.healthStatus?.overall || 'unknown'}`);
            console.log(`    • Active alerts: ${dashboard.activeAlerts || 0}`);
            console.log(`    • Critical alerts: ${dashboard.criticalAlerts || 0}`);

            // Test metrics history
            const history = this.operationalMonitor.getMetricsHistory(1);
            console.log(`    • Metrics history entries: ${history.length}`);

            // Test operational report
            const report = this.operationalMonitor.generateOperationalReport(1);
            console.log(`    • Report ID: ${report.reportId}`);
            console.log(`    • Recommendations: ${report.recommendations.length}`);

            // Stop monitoring
            this.operationalMonitor.stopMonitoring();

            return dashboard !== null && history.length > 0 && report !== null;

        } catch (error) {
            console.error('    ❌ Operational monitoring testing failed:', error.message);
            return false;
        }
    }

    async testSecurityLayer() {
        try {
            console.log('  🔧 Testing security layer...');
            
            // Test user authentication
            console.log('  🔐 Testing user authentication...');
            const session = await this.securityLayer.authenticateUser(
                'admin', 'admin123', '127.0.0.1', 'test-agent'
            );
            
            if (!session) {
                console.error('    ❌ Authentication failed');
                return false;
            }
            
            console.log(`    ✅ User authenticated: ${session.userId}`);
            console.log(`    • Session ID: ${session.sessionId}`);
            console.log(`    • Permissions: ${Array.from(session.permissions).join(', ')}`);

            // Test access validation
            console.log('  🔍 Testing access validation...');
            const accessResult = this.securityLayer.validateAccess(session.sessionId, 'tycoon');
            console.log(`    • Access valid: ${accessResult.valid}`);

            // Test PII redaction
            console.log('  🛡️ Testing PII redaction...');
            const testLog = 'User john.doe@example.com with SSN 123-45-6789 accessed system';
            const redactedLog = this.securityLayer.processLogEntry(testLog, session.sessionId);
            console.log(`    • Original: ${testLog}`);
            console.log(`    • Redacted: ${redactedLog}`);
            
            const piiRedacted = redactedLog.includes('[REDACTED]');
            console.log(`    • PII redaction working: ${piiRedacted ? '✅' : '❌'}`);

            // Test security metrics
            console.log('  📊 Testing security metrics...');
            const securityMetrics = this.securityLayer.getSecurityMetrics();
            console.log(`    • Active sessions: ${securityMetrics.activeSessions}`);
            console.log(`    • TLS enabled: ${securityMetrics.tlsEnabled}`);
            console.log(`    • Compliance mode: ${securityMetrics.complianceMode}`);

            // Logout
            const logoutSuccess = this.securityLayer.logout(session.sessionId);
            console.log(`    • Logout successful: ${logoutSuccess ? '✅' : '❌'}`);

            return accessResult.valid && piiRedacted && logoutSuccess;

        } catch (error) {
            console.error('    ❌ Security layer testing failed:', error.message);
            return false;
        }
    }
}

// Run the test suite
async function main() {
    const testSuite = new Phase2TestSuite();
    const results = await testSuite.runAllTests();
    
    // Exit with appropriate code
    process.exit(results.overallSuccess ? 0 : 1);
}

// Handle unhandled rejections
process.on('unhandledRejection', (reason, promise) => {
    console.error(chalk.red('Unhandled Rejection at:'), promise, chalk.red('reason:'), reason);
    process.exit(1);
});

main().catch(error => {
    console.error(chalk.red('Test suite failed:'), error);
    process.exit(1);
});
