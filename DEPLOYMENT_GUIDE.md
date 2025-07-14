# 🚀 Tycoon AI-BIM Platform Deployment Guide

## 📋 **Pre-Deployment Checklist**

### **System Requirements**
- ✅ Windows 10/11 (64-bit)
- ✅ Autodesk Revit 2022-2024
- ✅ .NET Framework 4.8 or higher
- ✅ Visual Studio Build Tools (for script development)
- ✅ Node.js 20.x (for MCP server)

### **Build Verification**
- ✅ TycoonRevitAddin.dll built successfully
- ✅ Tycoon.Scripting.Contracts.dll available
- ✅ ElementCounter test script compiled
- ✅ MCP server packaged
- ✅ MSI installer created

## 🎯 **Deployment Steps**

### **Step 1: Install the Platform**
```powershell
# Navigate to installer directory
cd "C:\RevitAI\tycoon-ai-bim-platform\src\installer\bin\Release"

# Run the MSI installer
.\TycoonAI-BIM-Platform.msi
```

**What the installer does:**
- Deploys TycoonRevitAddin.dll to Revit add-ins directory
- Installs MCP server components
- Registers Revit add-in (.addin file)
- Sets up script development environment
- Configures all dependencies

### **Step 2: Verify Installation**
1. **Check Revit Add-ins Directory:**
   ```
   %APPDATA%\Autodesk\Revit\Addins\2024\
   ├── TycoonRevitAddin.addin
   ├── TycoonRevitAddin.dll
   ├── Tycoon.Scripting.Contracts.dll
   └── [dependencies...]
   ```

2. **Check Script Development Directory:**
   ```
   C:\RevitAI\tycoon-ai-bim-platform\test-scripts\
   └── ElementCounter\
       ├── ElementCounter.cs
       ├── ElementCounter.csproj
       ├── script.json
       └── bin\Release\
           ├── ElementCounter.dll
           └── script.json
   ```

### **Step 3: Test in Revit**
1. **Start Revit** with any project
2. **Verify Tycoon Ribbon** appears with panels:
   - AI Integration
   - Plugin Control
   - Scripts Control
   - **Production** (where scripts appear)
   - Smart Tools
   - Management

3. **Check for ElementCounter** in Production panel
4. **Execute ElementCounter** to test basic functionality

## 🔥 **Testing Hot-Reload Workflow**

### **Test Scenario 1: Basic Script Execution**
```csharp
// In ElementCounter.cs, verify this code works:
public class ElementCounter : IScript
{
    public string Name => "Element Counter";
    public string Description => "Counts elements in the active document";
    
    public void Execute(ScriptContext context)
    {
        var doc = context.Document;
        var collector = new FilteredElementCollector(doc);
        var count = collector.WhereElementIsNotElementType().GetElementCount();
        
        TaskDialog.Show("Element Counter", $"Total elements: {count}");
    }
}
```

### **Test Scenario 2: Hot-Reload Functionality**
1. **Modify the script:**
   ```csharp
   TaskDialog.Show("Element Counter", $"🎯 UPDATED: Total elements: {count}");
   ```

2. **Build the script:**
   ```powershell
   cd "C:\RevitAI\tycoon-ai-bim-platform\test-scripts\ElementCounter"
   dotnet build ElementCounter.csproj --configuration Release
   ```

3. **Save and test** - Script should reload automatically
4. **Execute in Revit** - Should show updated message

### **Test Scenario 3: Error Handling**
1. **Introduce an error:**
   ```csharp
   var invalidOperation = null.ToString(); // This will cause an error
   ```

2. **Build and test** - Should show proper error message
3. **Fix the error** and rebuild - Should work normally again

## 🛠️ **Development Environment Setup**

### **For Script Development**
1. **Development Path:** `C:\RevitAI\tycoon-ai-bim-platform\test-scripts`
2. **FileSystemWatcher** monitors this directory automatically
3. **Build Command:** `dotnet build --configuration Release`
4. **Manifest Required:** Each script needs `script.json`

### **Script Template Structure**
```
MyScript/
├── MyScript.cs          # Main script implementation
├── MyScript.csproj      # Project file with Contracts reference
├── script.json          # Manifest for discovery
└── bin/Release/
    ├── MyScript.dll     # Compiled script
    ├── script.json      # Copied manifest
    └── [dependencies...]
```

### **Example script.json**
```json
{
  "name": "My Script",
  "description": "Description of what the script does",
  "author": "Your Name",
  "version": "1.0.0",
  "assemblyPath": "MyScript.dll",
  "className": "MyScript",
  "category": "Utilities"
}
```

## 🔍 **Troubleshooting**

### **Common Issues**

**1. Script doesn't appear in ribbon:**
- Check if script.json is in bin/Release directory
- Verify ElementCounter.dll was built successfully
- Check Revit console for ScriptEngine initialization messages

**2. Hot-reload not working:**
- Ensure FileSystemWatcher is monitoring correct directory
- Check if build actually succeeded (no compilation errors)
- Verify script.json was copied to output directory

**3. Script execution errors:**
- Check transaction management - host handles all transactions
- Verify all dependencies are available
- Check AppDomain isolation is working correctly

### **Debug Information**
- **ScriptEngine logs** appear in Revit console
- **Build errors** shown in command line output
- **Runtime errors** displayed in TaskDialog with rollback

### **Expected Log Messages**
```
🎯 ScriptEngine initializing in Development mode
✅ ScriptEngine initialized successfully in Development mode
📁 FileSystemWatcher monitoring: C:\RevitAI\tycoon-ai-bim-platform\test-scripts
🔄 Script change detected: ElementCounter
✅ Script reloaded: Element Counter
```

## 🎯 **Success Criteria**

### **Deployment Success**
- ✅ Revit starts without errors
- ✅ Tycoon ribbon appears with all panels
- ✅ ElementCounter appears in Production panel
- ✅ Script executes successfully

### **Hot-Reload Success**
- ✅ Script modification detected automatically
- ✅ Rebuild updates script without Revit restart
- ✅ Updated functionality works immediately
- ✅ Error handling and recovery works properly

### **Development Workflow Success**
- ✅ Edit → Build → Save → Test cycle works smoothly
- ✅ No Revit crashes from script errors
- ✅ Transaction management prevents database corruption
- ✅ AppDomain isolation provides stability

## 🚀 **Next Steps After Deployment**

1. **Validate Core Functionality** - Test all basic operations
2. **Test Hot-Reload Extensively** - Verify the development workflow
3. **Create Additional Scripts** - Expand the script library
4. **Performance Testing** - Monitor AppDomain and FileSystemWatcher performance
5. **User Training** - Document the new development workflow for your team

---

**The unified script architecture is ready for production use!** 🎉

This deployment guide ensures a smooth transition to the new hot-reload development environment, replacing the legacy conflicting script systems with a modern, efficient, and safe architecture.
