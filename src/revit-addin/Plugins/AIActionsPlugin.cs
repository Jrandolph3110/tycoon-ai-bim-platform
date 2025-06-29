using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using TycoonRevitAddin.AIActions.Events;
using TycoonRevitAddin.AIActions.Commands;
using TycoonRevitAddin.AIActions.Validation;
using TycoonRevitAddin.AIActions.Workflow;
using TycoonRevitAddin.AIActions.Autonomy;
using TycoonRevitAddin.AIActions.Threading;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.Plugins
{
    /// <summary>
    /// AI Actions Plugin - Revolutionary AI-driven Revit automation
    /// Implements event-sourced commands with Preview-Apply-Commit workflow
    /// Features three-phase validation and user autonomy levels
    /// </summary>
    public class AIActionsPlugin : PluginBase
    {
        private IEventStore _eventStore;
        private IValidationEngine _validationEngine;
        private IWorkflowEngine _workflowEngine;
        private IAutonomyManager _autonomyManager;
        private ITaskQueue _taskQueue;

        public override string Id => "ai-actions";
        public override string Name => "AI Actions";
        public override string Description => "Revolutionary AI-driven Revit automation with enterprise-grade safety and control";
        public override string Version => "1.0.0";
        public override string IconPath => "Resources/AIActionsIcon.png";

        public AIActionsPlugin(Logger logger) : base(logger)
        {
        }

        protected override void CreatePanels()
        {
            // Initialize AI Actions system
            InitializeAIActionsSystem();

            // Create AI Control panel
            var aiControlPanel = CreatePanel("AI Control");
            CreateAIControlButtons(aiControlPanel);

            // Create AI Commands panel
            var aiCommandsPanel = CreatePanel("AI Commands");
            CreateAICommandButtons(aiCommandsPanel);

            // Create AI Monitoring panel
            var aiMonitoringPanel = CreatePanel("AI Monitoring");
            CreateAIMonitoringButtons(aiMonitoringPanel);
        }

        /// <summary>
        /// Initialize the AI Actions system components
        /// </summary>
        private void InitializeAIActionsSystem()
        {
            try
            {
                _logger.Log("🤖 Initializing AI Actions system...");

                // Initialize event store
                _eventStore = new RevitEventStore(_logger);
                _logger.Log("📝 Event store initialized");

                // Initialize validation engine
                _validationEngine = new ValidationEngine();
                _logger.Log("✅ Validation engine initialized");

                // Initialize workflow engine
                _workflowEngine = new WorkflowEngine(_eventStore, _logger);
                _logger.Log("🔄 Workflow engine initialized");

                // Initialize autonomy manager
                _autonomyManager = new AutonomyManager(_workflowEngine, _logger);
                _logger.Log("🎛️ Autonomy manager initialized");

                // Initialize task queue (will be set up when UI application is available)
                _logger.Log("⏳ Task queue will be initialized when UI application is available");

                _logger.Log("🚀 AI Actions system initialization complete!");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize AI Actions system", ex);
                throw;
            }
        }

        /// <summary>
        /// Create AI Control buttons for autonomy and settings
        /// </summary>
        private void CreateAIControlButtons(RibbonPanel panel)
        {
            // Autonomy Level Selector
            var autonomyData = new ComboBoxData("AutonomyLevel");
            var autonomyCombo = panel.AddItem(autonomyData) as ComboBox;
            
            // Add autonomy levels
            var advisorData = new ComboBoxMemberData("Advisor", "Advisor");
            advisorData.ToolTip = "Analysis only - AI cannot modify the model";
            autonomyCombo.AddItem(advisorData);

            var assistantData = new ComboBoxMemberData("Assistant", "Assistant");
            assistantData.ToolTip = "AI can stage changes but requires user approval";
            autonomyCombo.AddItem(assistantData);

            var autopilotData = new ComboBoxMemberData("Autopilot", "Autopilot");
            autopilotData.ToolTip = "AI can execute changes within predefined budgets";
            autonomyCombo.AddItem(autopilotData);

            // Set default to Assistant
            autonomyCombo.Current = autonomyCombo.GetItems().FirstOrDefault(item => item.Name == "Assistant");
            autonomyCombo.CurrentChanged += OnAutonomyLevelChanged;

            // AI Settings button
            AddPushButton(
                panel,
                "AISettings",
                "AI\nSettings",
                "TycoonRevitAddin.Commands.AISettingsCommand",
                "Configure AI Actions settings and constraints",
                "AISettingsIcon.png"
            );

            // Emergency Stop button
            AddPushButton(
                panel,
                "EmergencyStop",
                "Emergency\nStop",
                "TycoonRevitAddin.Commands.EmergencyStopCommand",
                "Immediately stop all AI actions and cancel pending operations",
                "EmergencyStopIcon.png"
            );
        }

        /// <summary>
        /// Create AI Command buttons for common operations
        /// </summary>
        private void CreateAICommandButtons(RibbonPanel panel)
        {
            // AI Create Wall button
            AddPushButton(
                panel,
                "AICreateWall",
                "AI Create\nWall",
                "TycoonRevitAddin.Commands.AICreateWallCommand",
                "AI-powered wall creation with FLC standards",
                "AICreateWallIcon.png"
            );

            // AI Frame Walls button
            AddPushButton(
                panel,
                "AIFrameWalls",
                "AI Frame\nWalls",
                "TycoonRevitAddin.Commands.AIFrameWallsCommand",
                "AI-powered steel framing for selected walls",
                "AIFrameWallsIcon.png"
            );

            // AI Optimize Layout button
            AddPushButton(
                panel,
                "AIOptimizeLayout",
                "AI Optimize\nLayout",
                "TycoonRevitAddin.Commands.AIOptimizeLayoutCommand",
                "AI-powered layout optimization for material efficiency",
                "AIOptimizeIcon.png"
            );

            // AI Fix Errors button
            AddPushButton(
                panel,
                "AIFixErrors",
                "AI Fix\nErrors",
                "TycoonRevitAddin.Commands.AIFixErrorsCommand",
                "AI-powered automatic error detection and correction",
                "AIFixErrorsIcon.png"
            );
        }

        /// <summary>
        /// Create AI Monitoring buttons for tracking and control
        /// </summary>
        private void CreateAIMonitoringButtons(RibbonPanel panel)
        {
            // Activity Monitor button
            AddPushButton(
                panel,
                "ActivityMonitor",
                "Activity\nMonitor",
                "TycoonRevitAddin.Commands.ActivityMonitorCommand",
                "View AI actions history and real-time activity",
                "ActivityMonitorIcon.png"
            );

            // Event Viewer button
            AddPushButton(
                panel,
                "EventViewer",
                "Event\nViewer",
                "TycoonRevitAddin.Commands.EventViewerCommand",
                "Browse and analyze AI action events and audit trail",
                "EventViewerIcon.png"
            );

            // AI Undo button
            AddPushButton(
                panel,
                "AIUndo",
                "AI\nUndo",
                "TycoonRevitAddin.Commands.AIUndoCommand",
                "Undo AI actions with granular control",
                "AIUndoIcon.png"
            );

            // Performance Stats button
            AddPushButton(
                panel,
                "PerformanceStats",
                "Performance\nStats",
                "TycoonRevitAddin.Commands.PerformanceStatsCommand",
                "View AI Actions performance statistics and metrics",
                "PerformanceStatsIcon.png"
            );
        }

        /// <summary>
        /// Handle autonomy level changes
        /// </summary>
        private void OnAutonomyLevelChanged(object sender, EventArgs e)
        {
            try
            {
                var comboBox = sender as ComboBox;
                var selectedLevel = comboBox?.Current?.Name;

                if (Enum.TryParse<TycoonRevitAddin.AIActions.Autonomy.UserAutonomyLevel>(selectedLevel, out var autonomyLevel))
                {
                    _autonomyManager?.SetAutonomyLevel(autonomyLevel);
                    _logger.Log($"🎛️ Autonomy level changed to: {autonomyLevel}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error changing autonomy level", ex);
            }
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            
            // Initialize task queue with UI application
            // Note: Task queue will be initialized when we have access to UIApplication
            // For now, we'll defer this to when commands are actually executed
            _logger.Log("⚡ Task queue initialization deferred until command execution");

            _logger.Log("🤖 AI Actions plugin activated - Ready for intelligent automation!");
        }

        protected override void OnDeactivated()
        {
            base.OnDeactivated();
            
            // Stop task queue
            if (_taskQueue != null)
            {
                _taskQueue.Stop();
                _logger.Log("⏹️ Task queue stopped");
            }

            _logger.Log("🤖 AI Actions plugin deactivated");
        }

        protected override void OnCleanup()
        {
            base.OnCleanup();
            
            try
            {
                // Cleanup AI Actions components
                if (_taskQueue is IDisposable disposableQueue)
                {
                    disposableQueue.Dispose();
                }

                _logger.Log("🧹 AI Actions system cleaned up");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during AI Actions cleanup", ex);
            }
        }

        /// <summary>
        /// Get the event store for external access
        /// </summary>
        public IEventStore GetEventStore() => _eventStore;

        /// <summary>
        /// Get the validation engine for external access
        /// </summary>
        public IValidationEngine GetValidationEngine() => _validationEngine;

        /// <summary>
        /// Get the workflow engine for external access
        /// </summary>
        public IWorkflowEngine GetWorkflowEngine() => _workflowEngine;

        /// <summary>
        /// Get the autonomy manager for external access
        /// </summary>
        public IAutonomyManager GetAutonomyManager() => _autonomyManager;

        /// <summary>
        /// Get the task queue for external access
        /// </summary>
        public ITaskQueue GetTaskQueue() => _taskQueue;

        /// <summary>
        /// Execute an AI command with full autonomy and safety controls
        /// </summary>
        public async Task<WorkflowResult> ExecuteAICommandAsync(IAICommand command, CommandContext context)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (context == null) throw new ArgumentNullException(nameof(context));

            try
            {
                _logger.Log($"🎯 Executing AI command: {command.CommandType}");

                // Execute with autonomy controls
                var result = await _autonomyManager.ExecuteWithAutonomyAsync(command, context);

                if (result.Success)
                {
                    _logger.Log($"✅ AI command completed: {command.CommandType} - {result.Message}");
                }
                else
                {
                    _logger.LogError($"❌ AI command failed: {command.CommandType} - {result.Message}");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error executing AI command: {command.CommandType}", ex);
                return WorkflowResult.CreateFailure(WorkflowStage.Failed, $"Execution error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get comprehensive AI Actions statistics
        /// </summary>
        public AIActionsStatistics GetStatistics()
        {
            return new AIActionsStatistics
            {
                EventStore = (_eventStore as RevitEventStore)?.GetStatistics(),
                Validation = _validationEngine?.GetStatistics(),
                Autonomy = _autonomyManager?.GetStatistics(),
                TaskQueue = _taskQueue?.GetStatistics(),
                IsActive = IsActive,
                PluginVersion = Version
            };
        }
    }

    /// <summary>
    /// Comprehensive AI Actions statistics
    /// </summary>
    public class AIActionsStatistics
    {
        public EventStoreStatistics EventStore { get; set; }
        public ValidationStatistics Validation { get; set; }
        public AutonomyStatistics Autonomy { get; set; }
        public TaskQueueStatistics TaskQueue { get; set; }
        public bool IsActive { get; set; }
        public string PluginVersion { get; set; }
    }
}
