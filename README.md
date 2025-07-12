# 🏗️ Tycoon AI-BIM Platform

AI-powered construction automation platform with live Revit integration for steel framing workflows.

## 🎯 Overview

The Tycoon AI-BIM Platform connects AI assistants with Autodesk Revit to enable intelligent automation of steel framing construction workflows. Developed specifically for F.L. Crane & Sons' prefabricated light gauge steel construction using FrameCAD standards.

## 📋 Current Version: 0.16.0.5

### **🏗️ Core Architecture**
- ✅ **MCP Server Integration** - Model Context Protocol for AI communication
- ✅ **Revit Add-in Framework** - Ribbon interface with specialized construction tools
- ✅ **Real-time Selection Sharing** - Automatic context sharing with AI assistants
- ✅ **WebSocket Communication** - Bidirectional data flow between Revit and AI
- ✅ **Enhanced Process Management** - Graceful shutdown handling and singleton enforcement
- ✅ **Version Management System** - Automated version synchronization across all components

### **⚡ Performance Features**
- ✅ **Large Selection Handling** - Processes 100,000+ elements efficiently
- ✅ **Chunked Processing** - Intelligent batching prevents memory issues
- ✅ **Background Processing** - Non-blocking operations with progress updates
- ✅ **Memory Optimization** - Dynamic garbage collection and caching

### **🔧 Process Management & Version Control (Latest Enhancements)**
- ✅ **Graceful Shutdown** - Proper SIGTERM/SIGINT signal handling
- ✅ **PID File Management** - Singleton process enforcement
- ✅ **Auto-reconnection** - Revit automatically reconnects after server restart
- ✅ **VS Code Integration** - Clean startup/shutdown with development environments
- ✅ **Enhanced Version Management** - Automated version synchronization with validation
- ✅ **Build System Improvements** - Fixed XML corruption and dependency issues

### **📊 Performance Characteristics**
- **Large selections** - Handles 100,000+ elements without timeouts
- **Memory efficient** - Chunked processing prevents memory overflow
- **Stable operation** - Robust error handling and recovery mechanisms
- **Fast response** - Optimized data serialization and transfer

## ✨ Features

- 🤖 **AI-Revit Integration** - Direct communication between AI assistants and Revit models
- 🏗️ **Steel Framing Automation** - FLC-specific workflows and standards
- 📋 **Element Selection Context** - AI understands current Revit selection
- 🔧 **Dynamic Scripting** - AI-generated scripts based on model context
- 📊 **Panel Management** - Automated panel numbering and validation
- 🎯 **Multi-user Support** - Dynamic port discovery for team environments
- 🔄 **Process Management** - Reliable startup/shutdown with singleton enforcement

## 🚀 Quick Start

### Installation

1. **Download** the latest `TycoonAI-BIM-Platform-Setup.exe` from `src/installer/bin/Release/`
2. **Run installer as Administrator** - All components install automatically including Node.js prerequisites
3. **Open Revit** - Look for the "Tycoon AI-BIM" tab in the ribbon
4. **Copy MCP Config** - Click the button to copy JSON configuration
5. **Configure AI Assistant** - Paste into your AI assistant's MCP settings

> **Note**: The installer is a WiX bootstrapper (1.68 MB) that includes the MSI package, MCP server, and Node.js dependencies.

### AI Assistant Setup

#### For Augment Users
1. Open Augment settings
2. Navigate to MCP configuration section
3. Paste the copied JSON configuration
4. Restart Augment to load the new server
5. Test connection: "What Revit elements do I have selected?"

#### For VS Code Users
1. Install the MCP extension in VS Code
2. Add Tycoon configuration to VS Code settings
3. Restart VS Code to activate the connection
4. Test integration with your Revit model

## 📦 What's Included

- **Revit Add-in** - Integration with Autodesk Revit 2022-2025 (TycoonRevitAddin.dll)
- **MCP Server** - TypeScript-based Model Context Protocol server for AI communication
- **WiX Bootstrapper** - Complete installer with Node.js prerequisites (TycoonAI-BIM-Platform-Setup.exe)
- **Setup Wizard** - Custom UI for guided installation (TycoonSetupWizard.exe)
- **Download Manager** - MCP component installer (DownloadMCP.exe)
- **Documentation** - Setup guides and troubleshooting procedures
- **Build System** - Enhanced PowerShell-based build automation with version management

## 🔧 System Requirements

- **Revit** 2022, 2023, 2024, or 2025
- **Windows** 10/11 (x64)
- **Node.js** 18+ (auto-installed if needed)
- **AI Assistant** with MCP support (Augment, VS Code with MCP extension)
- **.NET Framework** 4.8+ (typically pre-installed)

## 🏗️ Steel Framing Features

### FLC Standards Support
- **Wall Types** - FLC_[thickness]_[Int/Ext]_[options] naming convention
- **Panel Management** - BIMSF_Container and BIMSF_Id parameter handling
- **Element Sequencing** - Left-to-right stud numbering regardless of wall orientation
- **Assembly Logic** - Jamb, header, and sill detection with proper dimensioning

### Automation Workflows
- **Panel Renumbering** - Automated sequence generation following FLC standards
- **Opening Detection** - Smart assembly recognition for doors and windows
- **Dimension Placement** - Face-based dimensioning for structural accuracy
- **Quality Validation** - FLC panel ticket requirement compliance checking

## 🔧 Development & Build

### Building from Source
```powershell
# Clone the repository
git clone https://github.com/Jrandolph3110/tycoon-ai-bim-platform.git
cd tycoon-ai-bim-platform

# Build the complete installer with bootstrapper
cd src/installer
.\Build.ps1 -BuildBootstrapper

# For development builds
.\Build.ps1 -Configuration Debug

# Version management
.\UpdateVersion.ps1 -Increment Revision  # Increment version
.\SyncVersions.ps1 -DryRun              # Check version consistency
```

### Version Management Tools
- **UpdateVersion.ps1** - Enhanced script with Bundle.wxs support and validation
- **SyncVersions.ps1** - New tool for version consistency checking and synchronization
- **Automated validation** - Pre-update consistency checks prevent version drift

### Troubleshooting
- **Multiple MCP instances**: Restart VS Code, check process count
- **Revit connection issues**: Verify WebSocket port 8765, restart add-in
- **Build failures**: Clean all artifacts, restore dependencies, rebuild
- **Version inconsistencies**: Run `.\SyncVersions.ps1` to fix synchronization issues
- **XML compilation errors**: Check for corrupted XML declarations in .wxs files

## 🆕 Recent Improvements (January 2025)

### **Version Management System Overhaul**
- ✅ **Enhanced UpdateVersion.ps1** - Added Bundle.wxs support and comprehensive validation
- ✅ **New SyncVersions.ps1** - Version consistency checking and automated synchronization
- ✅ **Fixed Version Inconsistencies** - Resolved Bundle.wxs (0.15.0.0 → 0.16.0.5) and other drift issues
- ✅ **Improved Regex Patterns** - Prevents XML declaration corruption during version updates

### **Build System Improvements**
- ✅ **Fixed Runtime Identifier Issues** - Resolved NuGet restore problems in .NET Framework projects
- ✅ **Corrected Assembly Names** - Fixed DownloadMCP.exe vs InstallMCP.exe naming conflicts
- ✅ **Enhanced Error Handling** - Better diagnostics for WiX compilation and MSBuild issues
- ✅ **Automated Component Building** - SetupWizard and DownloadMCP now build automatically

### **Installer Enhancements**
- ✅ **Complete Bootstrapper Build** - Successfully generates TycoonAI-BIM-Platform-Setup.exe (1.68 MB)
- ✅ **Node.js Integration** - Automatic prerequisite installation with node-v20.11.0-x64.msi
- ✅ **Component Packaging** - MCP server properly packaged as mcp-server.zip (86 files)
- ✅ **Build Verification** - All components compile and link successfully

### **Codebase Cleanup**
- ✅ **Legacy Content Removal** - Cleaned 634 MB of Archive/ and duplicate files
- ✅ **Dependency Optimization** - Removed unused packages (cors, helmet, express, natural)
- ✅ **Documentation Updates** - Accurate cleanup reports and verification processes

## 🤝 Contributing

This project is developed by F.L. Crane & Sons for the construction industry. Contributions that improve steel framing automation and AI-BIM integration are welcome.

## 📄 License

Copyright © 2025 F.L. Crane & Sons. All rights reserved.

## 🆘 Support

- **Documentation** - See the [docs](docs/) folder for detailed guides
- **Build Instructions** - Check [BUILD_INSTRUCTIONS.md](BUILD_INSTRUCTIONS.md)
- **Setup Guide** - See [docs/SETUP-GUIDE.md](docs/SETUP-GUIDE.md)
- **Version Management** - Use `SyncVersions.ps1 -DryRun` to check consistency
- **Build Issues** - Ensure all prerequisites are installed and run `dotnet restore`
- **Contact** - Joseph Randolph, F.L. Crane & Sons

### **Current Build Status**
- ✅ **Installer**: Successfully builds TycoonAI-BIM-Platform-Setup.exe
- ✅ **Version Sync**: All components synchronized to v0.16.0.5
- ✅ **Dependencies**: All NuGet packages restored and functional
- ✅ **Git Status**: All improvements committed and pushed to GitHub

---

**Professional construction automation for the modern era**
