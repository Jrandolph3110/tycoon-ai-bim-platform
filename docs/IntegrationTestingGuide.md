# 🎯 Integration Testing Guide - Unified Script Architecture

## 🎉 **Integration Complete: Ready for Testing**

The unified script architecture has been successfully integrated into the main Tycoon Revit add-in. You now have a complete hot-reload development environment ready for testing.

## 🏗️ **What's Been Integrated:**

### ✅ **ScriptEngine Integration:**
- **ScriptsPlugin** now initializes ScriptEngine in Development mode
- **FileSystemWatcher** monitors `C:\RevitAI\tycoon-ai-bim-platform\test-scripts`
- **AppDomain isolation** with ScriptProxy for safe execution
- **Automatic transaction management** with rollback on errors

### ✅ **Ribbon System Connection:**
- **UnifiedScriptCommand** executes scripts through ScriptEngine
- **Dynamic button creation** when scripts are discovered
- **Scripts appear in Production panel** automatically
- **Hot-reload updates** ribbon without Revit restart

### ✅ **Test Environment:**
- **ElementCounter script** ready for testing
- **Built and deployed** to test-scripts directory
- **Manifest-based discovery** with script.json

## 🚀 **Testing Instructions:**

### **Step 1: Build the Main Application**
```powershell
# Navigate to project directory
cd C:\RevitAI\tycoon-ai-bim-platform

# Build the main Revit add-in
dotnet build src/revit-addin/TycoonRevitAddin.csproj --configuration Release
```

### **Step 2: Install/Update the Add-in**
1. **Copy built files** to Revit add-ins directory
2. **Ensure manifest file** points to correct DLL
3. **Restart Revit** to load updated add-in

### **Step 3: Test Initial Script Discovery**
1. **Start Revit** with Tycoon add-in loaded
2. **Check Production panel** - ElementCounter button should appear
3. **Verify logs** for ScriptEngine initialization messages
4. **Click ElementCounter** to test execution

### **Step 4: Test Hot-Reload Functionality**

#### **Modify Existing Script:**
1. **Edit** `test-scripts/ElementCounter/ElementCounter.cs`
2. **Change message text** or add new functionality
3. **Build script:** `dotnet build test-scripts/ElementCounter/ElementCounter.csproj --configuration Release`
4. **Save changes** - FileSystemWatcher should detect
5. **Check Revit** - script should reload automatically (no restart)
6. **Test updated script** by clicking button

#### **Add New Script:**
1. **Create new folder** in `test-scripts/`
2. **Add script.cs** implementing `IScript`
3. **Add script.json** manifest
4. **Build script** to generate DLL
5. **Save** - new button should appear in ribbon automatically

## 🔧 **Configuration Details:**

### **Development Mode Configuration:**
```csharp
// In ScriptsPlugin.InitializeScriptEngineAsync()
var config = new ScriptEngineConfig
{
    DevelopmentPath = @"C:\RevitAI\tycoon-ai-bim-platform\test-scripts",
    GitHubConfig = new GitHubScriptConfig
    {
        RepositoryUrl = "https://github.com/your-org/scripts",
        CachePath = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData), 
            "Tycoon", "UnifiedScriptCache")
    }
};
```

### **Script Directory Structure:**
```
C:\RevitAI\tycoon-ai-bim-platform\test-scripts\
├── ElementCounter\
│   ├── bin\Release\
│   │   ├── ElementCounter.dll      ← Built assembly
│   │   ├── script.json            ← Manifest
│   │   └── Tycoon.Scripting.Contracts.dll
│   ├── ElementCounter.cs          ← Source code
│   └── ElementCounter.csproj      ← Project file
└── YourNewScript\
    ├── bin\Release\
    │   ├── YourNewScript.dll
    │   └── script.json
    └── source files...
```

## 🧪 **Testing Scenarios:**

### **1. Basic Functionality Test:**
- ✅ Script appears in ribbon
- ✅ Script executes without errors
- ✅ Transaction management works
- ✅ Error handling displays properly

### **2. Hot-Reload Test:**
- ✅ Edit script source code
- ✅ Build script
- ✅ FileSystemWatcher detects change
- ✅ AppDomain unloads/reloads
- ✅ Updated script works without Revit restart

### **3. Error Handling Test:**
- ✅ Script with compilation error
- ✅ Script with runtime exception
- ✅ Transaction rollback on error
- ✅ Proper error messages displayed

### **4. Multiple Scripts Test:**
- ✅ Multiple scripts in different folders
- ✅ All scripts appear in ribbon
- ✅ Each script executes independently
- ✅ Hot-reload works for all scripts

## 🐛 **Troubleshooting:**

### **Script Not Appearing:**
1. Check ScriptEngine initialization logs
2. Verify script.json manifest format
3. Ensure DLL is built and in correct location
4. Check FileSystemWatcher is monitoring correct path

### **Script Execution Fails:**
1. Check ScriptProxy initialization
2. Verify Revit context is passed correctly
3. Check transaction management
4. Review script implementation for IScript interface

### **Hot-Reload Not Working:**
1. Verify FileSystemWatcher is active
2. Check debounce timer settings
3. Ensure AppDomain unload/reload cycle
4. Verify file permissions and locks

## 📊 **Expected Log Messages:**

### **Successful Initialization:**
```
🚀 Initializing ScriptEngine with unified architecture
✅ ScriptEngine initialized successfully in Development mode
👁️ FileSystemWatcher enabled for hot-reload
📜 Discovered 1 local scripts
✅ Updated ribbon with 1 unified script buttons
```

### **Hot-Reload Cycle:**
```
📝 File change detected: ElementCounter.dll (Changed)
⏰ Debounce timer elapsed - refreshing scripts for hot-reload
🗑️ Unloading script AppDomain for hot-reload
🏗️ Creating new script AppDomain
✅ Script AppDomain created with proxy
```

### **Script Execution:**
```
🚀 Executing script: Element Counter
🔄 Transaction started for script: Element Counter
✅ Transaction committed for script: Element Counter
```

## 🎯 **Success Criteria:**

Your unified script architecture is working correctly when:

1. ✅ **Scripts appear automatically** in ribbon on startup
2. ✅ **Scripts execute successfully** with proper transaction management
3. ✅ **Hot-reload works** - edit → build → save → test (no Revit restart)
4. ✅ **Error handling** displays proper messages and rolls back transactions
5. ✅ **Multiple scripts** can coexist and execute independently

## 🚀 **Next Steps After Testing:**

1. **Switch to Production Mode** for end-user distribution
2. **Configure GitHub repository** for script sharing
3. **Add more IRevitHost methods** as needed
4. **Optimize performance** for larger script collections
5. **Add advanced features** (debugging, profiling, etc.)

**Your efficient script development workflow is ready for testing!** 🎉
