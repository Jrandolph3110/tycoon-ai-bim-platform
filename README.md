# 🏗️ Tycoon AI-BIM Platform

Revolutionary AI-powered construction automation platform with live Revit integration for steel framing workflows.

## 🎯 Overview

Tycoon AI-BIM Platform bridges the gap between AI assistants and Autodesk Revit, enabling intelligent automation of steel framing construction workflows. Built specifically for F.L. Crane & Sons' prefabricated light gauge steel construction using FrameCAD standards.

## ✨ Features

- 🤖 **AI-Revit Integration** - Direct communication between AI assistants and Revit models
- 🏗️ **Steel Framing Automation** - FLC-specific workflows and standards
- 📋 **Element Selection Context** - AI understands what you have selected in Revit
- 🔧 **Script Generation** - AI creates custom scripts based on your model
- 📊 **Panel Management** - Automated panel numbering and validation
- 🎯 **Dynamic Port Discovery** - Seamless multi-user environments

## 🚀 Quick Start

### Installation

1. **Download** the latest `TycoonAI-BIM-Platform.msi` from [Releases](releases/)
2. **Run installer** - Everything installs automatically
3. **Open Revit** - Look for the "Tycoon AI-BIM" tab
4. **Copy MCP Config** - Click the button to copy JSON configuration
5. **Configure AI** - Paste into your AI assistant's MCP settings

### AI Assistant Setup

#### For Augment Users
1. Open Augment settings
2. Navigate to MCP configuration
3. Paste the copied JSON
4. Restart Augment
5. Ask: "What Revit elements do I have selected?"

#### For VS Code Users
1. Install MCP extension
2. Add Tycoon configuration to settings
3. Connect to your Revit model

## 📦 What's Included

- **Revit Add-in** - Seamless integration with Autodesk Revit 2022-2025
- **MCP Server** - Model Context Protocol server for AI communication
- **Auto-installer** - One-click deployment with all dependencies
- **Documentation** - Complete setup and usage guides

## 🔧 System Requirements

- **Revit** 2022, 2023, 2024, or 2025
- **Windows** 10/11
- **Node.js** (auto-installed if needed)
- **AI Assistant** with MCP support (Augment, VS Code, etc.)

## 🏗️ Steel Framing Features

### FLC Standards Support
- **Wall Types** - FLC_[thickness]_[Int/Ext]_[options] naming convention
- **Panel Management** - BIMSF_Container and BIMSF_Id parameter handling
- **Element Sequencing** - Left-to-right stud numbering regardless of orientation
- **Assembly Logic** - Jamb, header, and sill detection with proper dimensioning

### Automation Workflows
- **Panel Renumbering** - Automated sequence generation
- **Opening Detection** - Smart assembly recognition
- **Dimension Placement** - Face-based dimensioning for structural accuracy
- **Quality Validation** - FLC ticket requirement compliance

## 🤝 Contributing

This project is developed by F.L. Crane & Sons for the construction industry. We welcome contributions that improve steel framing automation and AI-BIM integration.

## 📄 License

Copyright © 2025 F.L. Crane & Sons. All rights reserved.

## 🆘 Support

- **Issues** - Report bugs and feature requests
- **Documentation** - Check the [docs](docs/) folder
- **Contact** - Joseph Randolph, F.L. Crane & Sons

---

**Built with ❤️ for the future of construction automation**
