# 🎯 Tycoon AI-BIM Platform Script Template

## Overview

This comprehensive script template serves as the definitive reference guide for developing scripts in the **Tycoon AI-BIM Platform**. It demonstrates proper configuration, coding patterns, error handling, and integration with platform services.

## 📁 File Structure

```
ScriptTemplate/
├── script.json          # Complete configuration reference
├── ScriptTemplate.cs    # C# source code template
├── ScriptTemplate.csproj # Project file with dependencies
├── README.md           # This documentation file
└── icon.png           # Optional custom icon (32x32 PNG)
```

## 🔧 Configuration Reference

### script.json Properties

The `script.json` file supports the following properties:

#### **Basic Information**
- `name`: Display name in ribbon
- `description`: Brief description of functionality
- `author`: Script author/team
- `version`: Semantic version (e.g., "1.0.0")

#### **Assembly Configuration**
- `assemblyPath`: Path to compiled DLL (relative to script folder)
- `className`: Fully qualified class name for execution

#### **Ribbon Placement**
- `panel`: Target panel ("Production", "SmartTools", "Management")
- `stack`: Group name for stacking related scripts
- `stackOrder`: Order within stack (1, 2, 3...)
- `stackType`: Display type ("stacked" or "dropdown")

#### **Behavior Settings**
- `requiresSelection`: Whether script needs selected elements
- `allowZeroDoc`: Can run without active document
- `requiresTransaction`: Needs transaction for model changes

#### **Metadata**
- `tags`: Array of keywords for categorization
- `category`: Script category for organization
- `minRevitVersion`: Minimum Revit version required
- `maxRevitVersion`: Maximum Revit version supported

#### **UI Customization**
- `toolTip`: Custom tooltip text
- `longDescription`: Detailed help text
- `iconPath`: Custom icon file path
- `hidden`: Hide from ribbon (utility scripts)

#### **Platform Integration**
- `usesAI`: Integrates with AI services
- `usesNeuralNexus`: Uses memory system
- `usesBimGpu`: Requires GPU acceleration
- `requiresMcp`: Needs MCP server connection

#### **Development**
- `isDevelopment`: Development/template script
- `debugMode`: Enable debug features
- `logLevel`: Logging verbosity level

## 💻 Code Architecture

### Class Structure

```csharp
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class ScriptTemplate
{
    // Platform integration properties
    private Application _app;
    private UIApplication _uiApp;
    private Document _doc;
    private UIDocument _uidoc;
    
    // Main execution method
    public Result Execute(ExternalCommandData commandData, 
                         ref string message, 
                         ElementSet elements)
    {
        // Implementation
    }
}
```

### Execution Flow

1. **Initialize Platform Integration**
   - Set up Revit API references
   - Validate platform services
   - Initialize logging

2. **Validate Prerequisites**
   - Check active document
   - Verify permissions
   - Validate requirements

3. **Execute Main Logic**
   - Implement script functionality
   - Handle transactions
   - Process elements

4. **Handle Results**
   - Log outcomes
   - Show user feedback
   - Clean up resources

## 🔨 Build Configuration

### Prerequisites

- Visual Studio 2019 or later
- .NET Framework 4.8
- Revit 2022-2025 installed
- Tycoon AI-BIM Platform

### Build Steps

1. **Debug Build**
   ```bash
   dotnet build --configuration Debug
   ```

2. **Release Build**
   ```bash
   dotnet build --configuration Release
   ```

3. **Output Location**
   - Debug: `bin/Debug/ScriptTemplate.dll`
   - Release: `bin/Release/ScriptTemplate.dll`

## 🚀 Integration with Platform

### UnifiedScriptCommand Integration

Scripts are executed through the `UnifiedScriptCommand` system:

1. Button click triggers `UnifiedScriptCommand.Execute()`
2. Script name retrieved from `button.ToolTip`
3. Assembly loaded dynamically
4. Script class instantiated and executed

### Platform Services

Access platform services through static references:

```csharp
// Logging
TycoonLogger.LogInfo("Message");

// AI Services
TycoonAI.ProcessRequest(request);

// Neural Nexus
NeuralNexus.StoreMemory(memory);
```

## 📊 Best Practices

### Error Handling

```csharp
try
{
    // Script logic
}
catch (Exception ex)
{
    LogError($"Script failed: {ex.Message}", ex);
    message = ex.Message;
    return Result.Failed;
}
```

### Transaction Management

```csharp
using (var transaction = new Transaction(_doc, "Operation Name"))
{
    transaction.Start();
    try
    {
        // Modify model
        transaction.Commit();
    }
    catch
    {
        transaction.RollBack();
        throw;
    }
}
```

### Element Processing

```csharp
var elements = GetSelectedElements();
foreach (var element in elements)
{
    try
    {
        ProcessElement(element);
    }
    catch (Exception ex)
    {
        LogError($"Failed to process element {element.Id}", ex);
    }
}
```

## 🔍 Testing and Debugging

### Local Testing

1. Build script in Debug mode
2. Copy to test-scripts directory
3. Use "Reload Scripts" in Tycoon ribbon
4. Test functionality

### GitHub Integration

1. Commit script to repository
2. Use "Refresh GitHub Scripts" button
3. Verify script appears in ribbon
4. Test remote functionality

## 📚 Additional Resources

- [Revit API Documentation](https://www.revitapidocs.com/)
- [Tycoon Platform Documentation](../docs/)
- [F.L. Crane & Sons Coding Standards](../docs/coding-standards.md)

## 🤝 Contributing

When creating new scripts based on this template:

1. Copy template folder
2. Rename files and classes
3. Update script.json configuration
4. Implement specific functionality
5. Test thoroughly
6. Document changes

## 📝 License

Copyright © F.L. Crane & Sons 2025. All rights reserved.
