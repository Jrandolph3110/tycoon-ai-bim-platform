using System;
using System.Collections.Generic;
using Autodesk.Revit.UI;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.Plugins
{
    /// <summary>
    /// Tycoon Pro FrAimer Plugin - Advanced FLC steel framing tools and workflows
    /// Contains all the professional steel framing tools for F.L. Crane & Sons
    /// </summary>
    public class TycoonProFrAimerPlugin : PluginBase
    {
        public override string Id => "tycoon-pro-fraimer";
        public override string Name => "Tycoon Pro FrAimer";
        public override string Description => "Advanced FLC steel framing tools and workflows";
        public override string Version => "1.0.0";
        public override string IconPath => "Resources/FramerIcon.png";

        public TycoonProFrAimerPlugin(Logger logger) : base(logger)
        {
        }

        protected override void CreatePanels()
        {
            // Create Steel Framing panel
            var steelFramingPanel = CreatePanel("Steel Framing");
            CreateSteelFramingButtons(steelFramingPanel);

            // Create Panel Management panel
            var panelManagementPanel = CreatePanel("Panel Management");
            CreatePanelManagementButtons(panelManagementPanel);

            // Create Quality Control panel
            var qualityControlPanel = CreatePanel("Quality Control");
            CreateQualityControlButtons(qualityControlPanel);
        }

        /// <summary>
        /// Create steel framing buttons
        /// </summary>
        private void CreateSteelFramingButtons(RibbonPanel panel)
        {
            // Frame Walls button
            AddPushButton(
                panel,
                "FrameWalls",
                "Frame\nWalls",
                "TycoonRevitAddin.Commands.FrameWallsCommand",
                "Create steel framing for selected walls using FLC standards",
                "FrameWallsIcon.png"
            );

            // Auto Frame button (future enhancement)
            AddPushButton(
                panel,
                "AutoFrame",
                "Auto\nFrame",
                "TycoonRevitAddin.Commands.AutoFrameCommand",
                "Automatically frame all walls in the project with AI optimization",
                "AutoFrameIcon.png"
            );

            // Frame Openings button
            AddPushButton(
                panel,
                "FrameOpenings",
                "Frame\nOpenings",
                "TycoonRevitAddin.Commands.FrameOpeningsCommand",
                "Create framing for door and window openings",
                "FrameOpeningsIcon.png"
            );
        }

        /// <summary>
        /// Create panel management buttons
        /// </summary>
        private void CreatePanelManagementButtons(RibbonPanel panel)
        {
            // Renumber Elements button
            AddPushButton(
                panel,
                "RenumberElements",
                "Renumber\nElements",
                "TycoonRevitAddin.Commands.RenumberCommand",
                "Renumber elements using FLC sequential standards",
                "RenumberIcon.png"
            );

            // Panel Sequencer button
            AddPushButton(
                panel,
                "PanelSequencer",
                "Panel\nSequencer",
                "TycoonRevitAddin.Commands.PanelSequencerCommand",
                "Sequence panels for optimal manufacturing workflow",
                "PanelSequencerIcon.png"
            );

            // BOM Generator button
            AddPushButton(
                panel,
                "BOMGenerator",
                "BOM\nGenerator",
                "TycoonRevitAddin.Commands.BOMGeneratorCommand",
                "Generate Bill of Materials for FrameCAD manufacturing",
                "BOMIcon.png"
            );
        }

        /// <summary>
        /// Create quality control buttons
        /// </summary>
        private void CreateQualityControlButtons(RibbonPanel panel)
        {
            // Validate Panels button
            AddPushButton(
                panel,
                "ValidatePanels",
                "Validate\nPanels",
                "TycoonRevitAddin.Commands.ValidateCommand",
                "Validate panel ticket requirements for FLC standards",
                "ValidateIcon.png"
            );

            // Quality Check button
            AddPushButton(
                panel,
                "QualityCheck",
                "Quality\nCheck",
                "TycoonRevitAddin.Commands.QualityCheckCommand",
                "Comprehensive quality check for steel framing compliance",
                "QualityCheckIcon.png"
            );

            // Clash Detection button
            AddPushButton(
                panel,
                "ClashDetection",
                "Clash\nDetection",
                "TycoonRevitAddin.Commands.ClashDetectionCommand",
                "Detect clashes in steel framing elements",
                "ClashDetectionIcon.png"
            );
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            _logger.Log("🏗️ Tycoon Pro FrAimer tools are now active - Ready for steel framing workflows!");
        }

        protected override void OnDeactivated()
        {
            base.OnDeactivated();
            _logger.Log("🏗️ Tycoon Pro FrAimer tools deactivated");
        }
    }
}
