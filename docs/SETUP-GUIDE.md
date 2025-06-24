# 🚀 Tycoon AI-BIM Platform Setup Guide

Complete installation and configuration guide for the Tycoon AI-BIM Platform.

## 📦 Installation

### Step 1: Download and Install

1. **Download** the latest `TycoonAI-BIM-Platform.msi` from the [Releases](../releases/) page
2. **Run the installer** as Administrator (right-click → "Run as administrator")
3. **Follow the installation wizard** - all components install automatically:
   - Revit add-in files
   - MCP server components
   - Node.js dependencies (if needed)
   - All required libraries

### Step 2: Verify Installation

1. **Open Autodesk Revit** (2022, 2023, 2024, or 2025)
2. **Look for the "Tycoon AI-BIM" tab** in the ribbon
3. **If the tab appears** - installation successful!

## 🤖 AI Assistant Configuration

### For Augment Users

1. **Open Revit** and click the **"Tycoon AI-BIM"** tab
2. **Click "Copy MCP Config"** - JSON configuration copied to clipboard
3. **Open Augment** settings
4. **Navigate to MCP configuration** section
5. **Paste the JSON** into the MCP servers field
6. **Restart Augment** to load the new configuration
7. **Test the connection** by asking: *"What Revit elements do I have selected?"*

### For VS Code Users

1. **Install the MCP extension** in VS Code
2. **Copy the MCP configuration** from Revit
3. **Add to VS Code settings** under MCP servers
4. **Restart VS Code** to activate

### For Other AI Assistants

The Tycoon MCP server uses the standard Model Context Protocol. Any AI assistant that supports MCP can connect using the generated configuration.

## 🔧 Configuration Details

### Generated MCP Configuration
```json
{
  "mcpServers": {
    "tycoon-ai-bim": {
      "command": "node",
      "args": ["%APPDATA%\\Tycoon\\mcp-server\\dist\\index.js"],
      "env": {
        "NODE_ENV": "production"
      }
    }
  }
}
```

### File Locations
- **Revit Add-in**: `%APPDATA%\Autodesk\Revit\Addins\2024\`
- **MCP Server**: `%APPDATA%\Tycoon\mcp-server\`
- **Logs**: `%APPDATA%\Tycoon\logs\`

## ✅ Testing the Integration

### Basic Connection Test
1. **Open Revit** with any model
2. **Select some elements** (walls, beams, etc.)
3. **Ask your AI**: *"What do I have selected in Revit?"*
4. **Expected response**: List of selected elements with details

### Steel Framing Test
1. **Open a model** with steel framing elements
2. **Select a wall** or panel
3. **Ask your AI**: *"Analyze this wall for steel framing"*
4. **Expected response**: Analysis of framing elements, dimensions, etc.

## 🛠️ Troubleshooting

### Revit Tab Not Appearing
- **Check installation**: Verify files in `%APPDATA%\Autodesk\Revit\Addins\2024\`
- **Restart Revit**: Close completely and reopen
- **Check Revit version**: Ensure you're using 2022-2025

### AI Not Connecting
- **Verify MCP config**: Ensure JSON is correctly pasted
- **Check file paths**: Verify MCP server files exist
- **Restart AI assistant**: Close and reopen after configuration
- **Check Node.js**: Ensure Node.js is installed and accessible

### Connection Errors
- **Check logs**: Look in `%APPDATA%\Tycoon\logs\` for error details
- **Port conflicts**: MCP server auto-discovers available ports
- **Firewall**: Ensure local connections are allowed

## 🏗️ Steel Framing Features

### Supported Workflows
- **Panel Management**: Automated numbering and validation
- **Element Selection**: Context-aware AI responses
- **Script Generation**: Custom automation based on your model
- **FLC Standards**: Built-in support for F.L. Crane & Sons workflows

### Example AI Queries
- *"Renumber the selected panels using FLC standards"*
- *"Generate a script to frame this wall"*
- *"Validate the selected assemblies against ticket requirements"*
- *"What's the total linear footage of studs in this selection?"*

## 📞 Support

### Getting Help
- **GitHub Issues**: Report bugs and request features
- **Documentation**: Check the [docs](.) folder for detailed guides
- **Contact**: Joseph Randolph, F.L. Crane & Sons

### Common Issues
- **Performance**: Large models may take longer to analyze
- **Compatibility**: Ensure Revit version is 2022 or newer
- **Updates**: Check for new releases regularly

---

**Ready to revolutionize your steel framing workflows with AI!** 🚀
