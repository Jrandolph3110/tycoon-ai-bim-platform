# 🧪 Tycoon Plugin System Testing Guide

## 📋 **Pre-Testing Setup**

### **1. Build the Project**
```bash
# Navigate to the Revit add-in directory
cd tycoon-ai-bim-platform/src/revit-addin

# Build the project (using Visual Studio or MSBuild)
msbuild TycoonRevitAddin.csproj /p:Configuration=Release
```

### **2. Install the Add-in**
- Copy the built DLL and .addin file to Revit's add-in directory
- Ensure the .addin file points to the correct DLL location

### **3. Expected Ribbon Layout**
```
┌─────────────────────────────────────────────────────────────────┐
│                        Tycoon AI-BIM Tab                        │
├─────────────────┬─────────────────┬─────────────────────────────┤
│ AI Integration  │ Plugin Control  │ [Dynamic Plugin Panels]    │
│                 │                 │                             │
│ • Copy MCP      │ • Plugin        │ Currently Selected:         │
│   Config        │   Selector ▼    │ • Scripts OR                │
│ • Connect       │ • Plugin Info   │ • Tycoon Pro FrAimer        │
│   to AI         │                 │                             │
└─────────────────┴─────────────────┴─────────────────────────────┘
```

## 🎯 **Testing Scenarios**

### **Test 1: Plugin System Initialization**
**Expected Behavior:**
- [ ] Tycoon AI-BIM tab appears in Revit ribbon
- [ ] AI Integration panel is visible with 2 buttons
- [ ] Plugin Control panel is visible with dropdown and info button
- [ ] Plugin selector dropdown contains "Scripts" and "Tycoon Pro FrAimer"
- [ ] One plugin is automatically activated (first in list)

**Check Log Output:**
```
🚀 Starting Tycoon AI-BIM Platform v1.2.0.0...
🔌 PluginManager created
🔌 Initializing Plugin Manager
📦 Registered plugin: Scripts v1.0.0
📦 Registered plugin: Tycoon Pro FrAimer v1.0.0
🔧 Initialized plugin scripts with X panels
🔧 Initialized plugin tycoon-pro-fraimer with Y panels
✅ Plugin Manager initialized with 2 plugins
✅ Plugin-based ribbon interface created
```

### **Test 2: Plugin Switching**
**Steps:**
1. Open Revit with the add-in loaded
2. Note which plugin is initially active
3. Click the Plugin Selector dropdown
4. Select the other plugin
5. Observe panel changes

**Expected Behavior:**
- [ ] Dropdown shows both plugins
- [ ] Selecting a plugin immediately switches the visible panels
- [ ] Previous plugin panels disappear
- [ ] New plugin panels appear
- [ ] Plugin Info button shows correct active plugin information

**Check Log Output:**
```
🎯 Plugin selector changed to: [plugin-id]
🔄 Deactivated plugin: [previous-plugin]
✅ Activated plugin: [new-plugin]
🟢 Plugin [name] activated
```

### **Test 3: Scripts Plugin Functionality**
**When Scripts Plugin is Active:**
- [ ] "Script Management" panel visible with 3 buttons
- [ ] "Dynamic Scripts" panel visible (may be empty initially)
- [ ] "Development Tools" panel visible with 3 buttons
- [ ] "Open Scripts Folder" button creates and opens folder
- [ ] Other buttons show "Coming Soon" messages

**Test Script Folder Creation:**
1. Click "Open Scripts Folder" button
2. Verify folder opens at: `%APPDATA%\Tycoon\Scripts`
3. Check for sample scripts: `ElementCounter.py` and `WallAnalyzer.cs`

### **Test 4: Tycoon Pro FrAimer Plugin Functionality**
**When Tycoon Pro FrAimer Plugin is Active:**
- [ ] "Steel Framing" panel visible with 3 buttons
- [ ] "Panel Management" panel visible with 3 buttons  
- [ ] "Quality Control" panel visible with 3 buttons
- [ ] Existing FLC commands (Frame Walls, Renumber, Validate) work
- [ ] New placeholder commands show "Coming Soon" messages

### **Test 5: Plugin Info Command**
**Steps:**
1. Activate any plugin
2. Click "Plugin Info" button in Plugin Control panel

**Expected Behavior:**
- [ ] Dialog shows current plugin information
- [ ] Displays: Name, ID, Version, Description, Status, Panel count
- [ ] Information matches the active plugin

### **Test 6: Error Handling**
**Test Scenarios:**
- [ ] Plugin switching with no selection (should handle gracefully)
- [ ] Plugin Info with no active plugin (should show appropriate message)
- [ ] Missing script folder (should create automatically)

## 🐛 **Common Issues and Solutions**

### **Issue: Plugins Not Loading**
**Symptoms:** Dropdown is empty or shows errors
**Solutions:**
- Check log for plugin registration errors
- Verify plugin classes inherit from PluginBase correctly
- Ensure plugin constructors don't throw exceptions

### **Issue: Panels Not Switching**
**Symptoms:** Panels don't hide/show when switching plugins
**Solutions:**
- Verify RibbonPanel.Visible property is being set
- Check for exceptions in Activate/Deactivate methods
- Ensure panels are properly stored in plugin _panels list

### **Issue: Commands Not Working**
**Symptoms:** Buttons don't respond or show errors
**Solutions:**
- Verify command classes exist and are properly attributed
- Check assembly location in PushButtonData
- Ensure command classes implement IExternalCommand

## 📊 **Performance Verification**

### **Memory Usage**
- [ ] Plugin switching doesn't cause memory leaks
- [ ] Panels are properly disposed when deactivated
- [ ] Plugin manager cleanup works correctly

### **Startup Time**
- [ ] Plugin initialization doesn't significantly slow Revit startup
- [ ] Large numbers of scripts don't impact performance
- [ ] Plugin switching is responsive

## ✅ **Success Criteria**

**The plugin system is working correctly if:**
1. ✅ All plugins load without errors
2. ✅ Plugin switching works smoothly
3. ✅ Panels show/hide correctly
4. ✅ Existing FLC tools continue to work
5. ✅ Script folder management works
6. ✅ No memory leaks or performance issues
7. ✅ Error handling is graceful
8. ✅ Log output is clean and informative

## 🚀 **Next Steps After Successful Testing**

1. **Implement Real Script Execution** - Replace placeholders with actual functionality
2. **Add Visual Enhancements** - Icons, better UI, status indicators
3. **Create Additional Plugins** - Sheathing, Clashing, etc.
4. **Add Configuration System** - Plugin settings and preferences
5. **Implement Hot-Reload** - Automatic script reloading
6. **Documentation** - User guides and developer documentation

## 📞 **Support**

If you encounter issues during testing:
1. Check the Revit console for error messages
2. Review the log output for plugin-specific errors
3. Verify all dependencies are properly referenced
4. Test with a minimal Revit model first

**Happy Testing!** 🦝💨✨
